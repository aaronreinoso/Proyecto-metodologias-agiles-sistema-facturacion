using Microsoft.EntityFrameworkCore;
using System.Text;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.Helpers;
using SistemaFacturacionSRI.Infrastructure.SRI.Models; // Requerido para modelos
using System.Xml.Linq; // Requerido para parsing

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _facturaElectronicaService;
        private readonly SriSoapClient _sriClient;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;

        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService facturaElectronicaService,
            SriSoapClient sriClient,
            IPdfService pdfService,
            IEmailService emailService
        )
        {
            _context = context;
            _facturaElectronicaService = facturaElectronicaService;
            _sriClient = sriClient;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // ============================================================
        //  MÉTODO HÍBRIDO: Extraer detalle SOLO si hay error
        // ============================================================
        private string ExtraerMensajeErrorDetallado(string xmlRaw, string tipo)
        {
            try
            {
                if (string.IsNullOrEmpty(xmlRaw)) return "Respuesta vacía del SRI.";

                // Limpieza básica para asegurar que el XMLHelper lo lea
                var doc = XDocument.Parse(xmlRaw);
                var nodo = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == tipo);

                if (nodo == null) return "Formato SRI no reconocido: " + xmlRaw;

                string xmlLimpio = nodo.ToString()
                    .Replace("xmlns=\"http://ec.gob.sri.ws.recepcion\"", "")
                    .Replace("xmlns=\"http://ec.gob.sri.ws.autorizacion\"", "");

                if (tipo == "RespuestaRecepcionComprobante")
                {
                    var resp = XmlHelper.Deserialize<RespuestaSolicitud>(xmlLimpio);
                    if (resp?.Comprobantes != null)
                    {
                        var errores = resp.Comprobantes
                            .SelectMany(c => c.Mensajes)
                            .Select(m => $"{m.Identificador}: {m.Mensaje} ({m.InformacionAdicional})");

                        if (errores.Any()) return string.Join(" | ", errores);
                    }
                }
                else if (tipo == "RespuestaAutorizacionComprobante")
                {
                    var resp = XmlHelper.Deserialize<RespuestaAutorizacion>(xmlLimpio);
                    var auth = resp?.Autorizaciones?.FirstOrDefault();
                    if (auth?.Mensajes != null && auth.Mensajes.Any())
                    {
                        var errores = auth.Mensajes.Select(m => $"{m.Mensaje} ({m.InformacionAdicional})");
                        return string.Join(" | ", errores);
                    }
                }

                // Si no se pudo deserializar o no hubo mensajes claros, devolvemos el RAW limitado
                return xmlRaw.Length > 250 ? xmlRaw.Substring(0, 250) + "..." : xmlRaw;
            }
            catch
            {
                // Si falla el parsing, devolvemos el texto original
                return xmlRaw.Length > 250 ? xmlRaw.Substring(0, 250) + "..." : xmlRaw;
            }
        }

        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ... PASOS 0, 1, 2, 3 (Configuración, Cliente, Creación BD, Cálculos) ...
                var configEmpresa = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
                if (configEmpresa == null || string.IsNullOrEmpty(configEmpresa.ClaveFirma))
                    throw new Exception("Firma no configurada.");

                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null) throw new Exception("Cliente no encontrado.");

                var nuevaFactura = new Factura
                {
                    ClienteId = dto.ClienteId,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente",
                    EstadoSRI = "PENDIENTE",
                    Detalles = new List<DetalleFactura>()
                };

                decimal subtotalFactura = 0;
                decimal totalIvaFactura = 0;

                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos.Include(p => p.TarifaIva).FirstOrDefaultAsync(p => p.Id == item.ProductoId);
                    if (producto == null) throw new Exception($"Producto {item.ProductoId} no existe.");
                    if (producto.Stock < item.Cantidad) throw new Exception($"Stock insuficiente: {producto.Descripcion}");

                    // Lógica de Lotes (conservar tu lógica original)
                    var lotesDisponibles = await _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaCaducidad.HasValue ? l.FechaCaducidad.Value : l.FechaRegistro)
                        .ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;
                    decimal costoTotalLotes = 0;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar == 0) break;
                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);
                        costoTotalLotes += aTomar * lote.PrecioCompraUnitario;
                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;
                        _context.Entry(lote).State = EntityState.Modified;
                    }
                    if (cantidadPorDescontar > 0) throw new Exception("Inconsistencia en Lotes.");

                    producto.Stock -= item.Cantidad;
                    decimal costoUnitario = costoTotalLotes / item.Cantidad;

                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;
                    decimal ivaLinea = subtotalLinea * (producto.TarifaIva?.Porcentaje ?? 0m) / 100m;

                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario,
                        Subtotal = subtotalLinea,
                        CostoUnitario = costoUnitario,
                        Producto = producto
                    });
                }

                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;
                nuevaFactura.Cliente = cliente;

                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync();

                // FIRMA XML
                string claveFirma = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);
                byte[] xmlBytes = _facturaElectronicaService.GenerarXmlFirmado(nuevaFactura, configEmpresa, claveFirma);
                nuevaFactura.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                // =====================================================================
                // ENVÍO SRI (LÓGICA HÍBRIDA)
                // =====================================================================
                string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                // USAMOS TU LÓGICA PROBADA PARA ÉXITO
                if (respuestaRecepcion.Contains("RECIBIDA"))
                {
                    nuevaFactura.EstadoSRI = "RECIBIDA";
                    await Task.Delay(1500);

                    string respuestaAutorizacion = await _sriClient.EnviarAutorizacionAsync(nuevaFactura.ClaveAcceso);

                    if (respuestaAutorizacion.Contains("AUTORIZADO"))
                    {
                        nuevaFactura.EstadoSRI = "AUTORIZADO";
                        nuevaFactura.Estado = "Autorizada";
                        nuevaFactura.FechaAutorizacion = DateTime.Now;
                        nuevaFactura.MensajeErrorSRI = null;

                        // PDF + EMAIL
                        try
                        {
                            var pdfBytes = _pdfService.GenerarFacturaPdf(nuevaFactura, configEmpresa);
                            if (!string.IsNullOrEmpty(cliente.Email))
                            {
                                string numFac = $"{configEmpresa.CodigoEstablecimiento}-{configEmpresa.CodigoPuntoEmision}-{nuevaFactura.Id:D9}";
                                await _emailService.EnviarFacturaAsync(cliente.Email, nuevaFactura.XmlGenerado, pdfBytes, numFac);
                            }
                        }
                        catch (Exception ex) { Console.WriteLine("Email error: " + ex.Message); }
                    }
                    else
                    {
                        // FALLO EN AUTORIZACIÓN: Usamos el método nuevo para sacar el detalle
                        nuevaFactura.EstadoSRI = "RECHAZADA";
                        nuevaFactura.Estado = "Rechazada";
                        nuevaFactura.MensajeErrorSRI = ExtraerMensajeErrorDetallado(respuestaAutorizacion, "RespuestaAutorizacionComprobante");
                    }
                }
                else
                {
                    // FALLO EN RECEPCIÓN: Usamos el método nuevo para sacar el detalle
                    nuevaFactura.EstadoSRI = "DEVUELTA";
                    nuevaFactura.Estado = "Devuelta";
                    nuevaFactura.MensajeErrorSRI = ExtraerMensajeErrorDetallado(respuestaRecepcion, "RespuestaRecepcionComprobante");
                }

                _context.Facturas.Update(nuevaFactura);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return nuevaFactura;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        //  REINTENTAR (LÓGICA HÍBRIDA)
        // ============================================================
        public async Task ReintentarFacturaAsync(int id)
        {
            var factura = await _context.Facturas.FindAsync(id);
            if (factura == null) throw new Exception("Factura no encontrada.");
            if (factura.EstadoSRI == "AUTORIZADO") throw new Exception("Ya autorizada.");
            if (string.IsNullOrEmpty(factura.XmlGenerado)) throw new Exception("Sin XML.");

            try
            {
                if (factura.EstadoSRI != "RECIBIDA")
                {
                    byte[] xmlBytes = Encoding.UTF8.GetBytes(factura.XmlGenerado);
                    string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                    if (respuestaRecepcion.Contains("RECIBIDA") || respuestaRecepcion.Contains("CLAVE ACCESO REGISTRADA"))
                    {
                        factura.EstadoSRI = "RECIBIDA";
                        factura.MensajeErrorSRI = null;
                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Error Detallado
                        string msgError = ExtraerMensajeErrorDetallado(respuestaRecepcion, "RespuestaRecepcionComprobante");
                        factura.EstadoSRI = "DEVUELTA";
                        factura.Estado = "Devuelta";
                        factura.MensajeErrorSRI = msgError;

                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                        throw new Exception($"Error en Recepción SRI: {msgError}");
                    }
                }

                if (factura.EstadoSRI == "RECIBIDA")
                {
                    await Task.Delay(1000);
                    string respuestaAuth = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);

                    if (respuestaAuth.Contains("AUTORIZADO"))
                    {
                        factura.EstadoSRI = "AUTORIZADO";
                        factura.Estado = "Autorizada";
                        factura.FechaAutorizacion = DateTime.Now;
                        factura.MensajeErrorSRI = null;
                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                    }
                    else if (respuestaAuth.Contains("RECHAZADA"))
                    {
                        // Error Detallado
                        string msgError = ExtraerMensajeErrorDetallado(respuestaAuth, "RespuestaAutorizacionComprobante");
                        factura.EstadoSRI = "RECHAZADA";
                        factura.Estado = "Rechazada";
                        factura.MensajeErrorSRI = msgError;

                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                        throw new Exception($"Error en Autorización SRI: {msgError}");
                    }
                    else
                    {
                        // Respuesta ambigua (En proceso, etc.)
                        throw new Exception("Respuesta SRI no definitiva: " + respuestaAuth);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        // ... (ObtenerHistorialFacturasAsync y ProcesarFacturasPendientesSriAsync se mantienen igual que en tu versión funcional)
        public async Task<List<FacturaResumenDto>> ObtenerHistorialFacturasAsync()
        {
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            string serie = config != null ? $"{config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}" : "000-000";

            return await _context.Facturas
                .Include(f => f.Cliente)
                .OrderByDescending(f => f.FechaEmision)
                .Select(f => new FacturaResumenDto
                {
                    Id = f.Id,
                    NumeroFactura = $"{serie}-{f.Id.ToString().PadLeft(9, '0')}",
                    ClienteNombre = f.Cliente != null ? f.Cliente.NombreCompleto : "Consumidor Final",
                    ClienteIdentificacion = f.Cliente != null ? f.Cliente.Identificacion : "9999999999999",
                    FechaEmision = f.FechaEmision,
                    Total = f.Total,
                    EstadoSRI = f.EstadoSRI ?? "PENDIENTE",
                    MensajeErrorSRI = f.MensajeErrorSRI,
                    ClaveAcceso = f.ClaveAcceso,
                    TieneXml = !string.IsNullOrEmpty(f.XmlGenerado)
                })
                .ToListAsync();
        }

        public async Task ProcesarFacturasPendientesSriAsync()
        {
            // Usamos tu lógica original
            var facturasEstancadas = await _context.Facturas
                .Where(f => (f.EstadoSRI == "RECIBIDA" || (f.EstadoSRI == "PENDIENTE" && f.XmlGenerado != null))
                            && f.Estado != "Autorizada" && f.Estado != "Rechazada")
                .ToListAsync();

            foreach (var factura in facturasEstancadas)
            {
                try
                {
                    if (factura.EstadoSRI == "PENDIENTE")
                    {
                        byte[] xmlBytes = Encoding.UTF8.GetBytes(factura.XmlGenerado);
                        string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                        if (respuestaRecepcion.Contains("RECIBIDA") || respuestaRecepcion.Contains("CLAVE ACCESO REGISTRADA"))
                        {
                            factura.EstadoSRI = "RECIBIDA";
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            factura.EstadoSRI = "DEVUELTA";
                            factura.MensajeErrorSRI = ExtraerMensajeErrorDetallado(respuestaRecepcion, "RespuestaRecepcionComprobante");
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                            continue;
                        }
                    }

                    if (factura.EstadoSRI == "RECIBIDA")
                    {
                        await Task.Delay(1000);
                        string respuestaAuth = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);

                        if (respuestaAuth.Contains("AUTORIZADO"))
                        {
                            factura.EstadoSRI = "AUTORIZADO";
                            factura.Estado = "Autorizada";
                            factura.MensajeErrorSRI = null;
                        }
                        else if (respuestaAuth.Contains("RECHAZADA"))
                        {
                            factura.EstadoSRI = "RECHAZADA";
                            factura.MensajeErrorSRI = ExtraerMensajeErrorDetallado(respuestaAuth, "RespuestaAutorizacionComprobante");
                        }
                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                    }
                }
                catch { /* Log silencioso */ }
            }
        }
    }
}