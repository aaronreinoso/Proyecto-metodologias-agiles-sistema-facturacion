using Microsoft.EntityFrameworkCore;
using System.Text;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.Helpers;
using SistemaFacturacionSRI.Infrastructure.SRI.Models;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _facturaElectronicaService;
        private readonly SriSoapClient _sriClient;

        // Servicios adicionales: PDF + Email
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;

        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService facturaElectronicaService,
            SriSoapClient sriClient,
            IPdfService pdfService,
            IEmailService emailService)
        {
            _context = context;
            _facturaElectronicaService = facturaElectronicaService;
            _sriClient = sriClient;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // ============================================================
        //  CREAR FACTURA + INTEGRACIÓN COMPLETA CON SRI
        // ============================================================
        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // =====================================================================
                // PASO 0: CONFIGURACIÓN DE EMPRESA
                // =====================================================================
                var configEmpresa = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();

                if (configEmpresa == null)
                    throw new Exception("No se han configurado los datos de la empresa ni la firma electrónica en el sistema.");

                if (configEmpresa.FirmaElectronica == null || string.IsNullOrEmpty(configEmpresa.ClaveFirma))
                    throw new Exception("La firma electrónica no ha sido cargada en la configuración.");

                // =====================================================================
                // PASO 1: VALIDACIONES Y CREACIÓN INICIAL
                // =====================================================================
                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null)
                    throw new Exception("Cliente no encontrado.");

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

                // =====================================================================
                // PASO 2: DETALLES, LÓGICA FIFO Y CÁLCULOS
                // =====================================================================
                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos
                        .Include(p => p.TarifaIva)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null)
                        throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    if (producto.Stock < item.Cantidad)
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}. Stock actual: {producto.Stock}");

                    decimal costoTotalLotesConsumidos = 0;

                    var lotesDisponibles = await _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaCaducidad.HasValue ? l.FechaCaducidad.Value : l.FechaRegistro)
                        .ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar == 0) break;

                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);

                        costoTotalLotesConsumidos += aTomar * lote.PrecioCompraUnitario;

                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;

                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                        throw new Exception($"Inconsistencia crítica: El stock global indicaba disponibilidad, pero no hay suficientes lotes.");

                    decimal costoUnitarioPonderado = costoTotalLotesConsumidos / item.Cantidad;

                    producto.Stock -= item.Cantidad;

                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;
                    decimal porcentajeIva = producto.TarifaIva?.Porcentaje ?? 0m;
                    decimal ivaLinea = subtotalLinea * (porcentajeIva / 100m);

                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario,
                        Subtotal = subtotalLinea,
                        CostoUnitario = costoUnitarioPonderado,
                        Producto = producto
                    });
                }

                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;
                nuevaFactura.Cliente = cliente;

                // Guardar para obtener Id/Secuencial
                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync();

                // =====================================================================
                // PASO 4: XML -> FIRMA DIGITAL -> SRI
                // =====================================================================

                // A. Desencriptar clave de la firma
                string claveFirmaDesencriptada = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);

                // B. Generar XML firmado
                byte[] xmlBytes = _facturaElectronicaService.GenerarXmlFirmado(
                    nuevaFactura,
                    configEmpresa,
                    claveFirmaDesencriptada);

                nuevaFactura.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                // =====================================================================
                // FASE A: ENVÍO A RECEPCIÓN (Validación de estructura y datos)
                // =====================================================================
                string respuestaRecepcionRaw = await _sriClient.EnviarRecepcionAsync(xmlBytes);
                var respuestaRecepcion = XmlHelper.Deserialize<RespuestaSolicitud>(respuestaRecepcionRaw);

                if (respuestaRecepcion != null && respuestaRecepcion.Estado == "RECIBIDA")
                {
                    // RECEPCIÓN CORRECTA
                    nuevaFactura.EstadoSRI = "RECIBIDA";

                    // Pausa breve para procesamiento interno del SRI
                    await Task.Delay(1500);

                    // =================================================================
                    // FASE B: SOLICITUD DE AUTORIZACIÓN
                    // =================================================================
                    string respuestaAuthRaw = await _sriClient.EnviarAutorizacionAsync(nuevaFactura.ClaveAcceso);
                    var respuestaAuth = XmlHelper.Deserialize<RespuestaAutorizacion>(respuestaAuthRaw);

                    var autorizacion = respuestaAuth?.Autorizaciones?.FirstOrDefault();

                    if (autorizacion != null && autorizacion.Estado == "AUTORIZADO")
                    {
                        nuevaFactura.EstadoSRI = "AUTORIZADO";
                        nuevaFactura.Estado = "Autorizada";

                        // Si tu modelo tiene fecha de autorización, puedes mapearla.
                        // Por simplicidad, usamos ahora:
                        nuevaFactura.FechaAutorizacion = DateTime.Now;

                        // -------------------------------------------------------------
                        //  PDF + ENVÍO DE CORREO (SE MANTIENE TU LÓGICA)
                        // -------------------------------------------------------------
                        try
                        {
                            var pdfBytes = _pdfService.GenerarFacturaPdf(nuevaFactura, configEmpresa);

                            if (!string.IsNullOrEmpty(cliente.Email))
                            {
                                string numFac = $"{configEmpresa.CodigoEstablecimiento}-{configEmpresa.CodigoPuntoEmision}-{nuevaFactura.Id:D9}";

                                await _emailService.EnviarFacturaAsync(
                                    cliente.Email,
                                    nuevaFactura.XmlGenerado,
                                    pdfBytes,
                                    numFac
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error enviando factura por correo: " + ex.Message);
                        }
                    }
                    else
                    {
                        // CASO: NO AUTORIZADO (Reglas de negocio, deudas SRI, etc.)
                        nuevaFactura.EstadoSRI = "RECHAZADA";
                        nuevaFactura.Estado = "Rechazada";

                        var msgError = "SRI No Autorizó.";
                        if (autorizacion?.Mensajes != null)
                        {
                            var detalles = autorizacion.Mensajes
                                .Select(m => $"{m.Mensaje} ({m.InformacionAdicional})");
                            msgError = string.Join("; ", detalles);
                        }

                        nuevaFactura.MensajeErrorSRI = msgError;
                    }
                }
                else
                {
                    // CASO: DEVUELTA EN RECEPCIÓN (XML mal formado, firma inválida, clave duplicada, etc.)
                    nuevaFactura.EstadoSRI = "DEVUELTA";
                    nuevaFactura.Estado = "Devuelta";

                    string msgError = "Error en Recepción.";

                    if (respuestaRecepcion?.Comprobantes != null)
                    {
                        var listaErrores = new List<string>();

                        foreach (var comp in respuestaRecepcion.Comprobantes)
                        {
                            if (comp.Mensajes != null)
                            {
                                foreach (var m in comp.Mensajes)
                                {
                                    listaErrores.Add($"{m.Identificador}: {m.Mensaje} - {m.InformacionAdicional}");
                                }
                            }
                        }

                        if (listaErrores.Any())
                            msgError = string.Join(" | ", listaErrores);
                    }
                    else
                    {
                        msgError = "Error de conexión o formato SRI desconocido.";
                    }

                    nuevaFactura.MensajeErrorSRI = msgError;
                }

                _context.Facturas.Update(nuevaFactura);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return nuevaFactura;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        //  HISTORIAL DE FACTURAS
        // ============================================================
        public async Task<List<FacturaResumenDto>> ObtenerHistorialFacturasAsync()
        {
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            string serie = config != null
                ? $"{config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}"
                : "000-000";

            var facturas = await _context.Facturas
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

            return facturas;
        }


        // ============================================================
        //  PROCESAR FACTURAS PENDIENTES CON EL SRI (JOB/MANUAL)
        // ============================================================
        public async Task ProcesarFacturasPendientesSriAsync()
        {
            var facturasEstancadas = await _context.Facturas
                .Where(f =>
                    (f.EstadoSRI == "RECIBIDA" || (f.EstadoSRI == "PENDIENTE" && f.XmlGenerado != null))
                    && f.Estado != "Autorizada"
                    && f.Estado != "Rechazada")
                .ToListAsync();

            foreach (var factura in facturasEstancadas)
            {
                // Usamos un bloque try/catch por factura para que si una falla, las demás sigan
                try
                {
                    // CASO A: Si está PENDIENTE, intentamos Recepción
                    if (factura.EstadoSRI == "PENDIENTE")
                    {
                        byte[] xmlBytes = Encoding.UTF8.GetBytes(factura.XmlGenerado);
                        string respuestaRecepcionRaw = await _sriClient.EnviarRecepcionAsync(xmlBytes);
                        var respuestaRecepcion = XmlHelper.Deserialize<RespuestaSolicitud>(respuestaRecepcionRaw);

                        if (respuestaRecepcion != null && respuestaRecepcion.Estado == "RECIBIDA")
                        {
                            factura.EstadoSRI = "RECIBIDA";
                            factura.MensajeErrorSRI = null;
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            // Error en Recepción
                            var msgError = "Error en Recepción (reintento).";
                            if (respuestaRecepcion?.Comprobantes != null)
                            {
                                var listaErrores = respuestaRecepcion.Comprobantes
                                    .SelectMany(c => c.Mensajes)
                                    .Select(m => $"{m.Identificador}: {m.Mensaje} - {m.InformacionAdicional}");
                                msgError = string.Join(" | ", listaErrores);
                            }

                            factura.EstadoSRI = "DEVUELTA";
                            factura.MensajeErrorSRI = msgError;
                            factura.Estado = "Devuelta";

                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                            continue; // Pasa a la siguiente factura
                        }
                    }

                    // CASO B: Si ya está RECIBIDA, intentamos autorización
                    if (factura.EstadoSRI == "RECIBIDA")
                    {
                        // Pausa para no saturar el SRI
                        await Task.Delay(1000);

                        string respuestaAuthRaw = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);
                        var respuestaAuth = XmlHelper.Deserialize<RespuestaAutorizacion>(respuestaAuthRaw);
                        var autorizacion = respuestaAuth?.Autorizaciones?.FirstOrDefault();

                        if (autorizacion != null && autorizacion.Estado == "AUTORIZADO")
                        {
                            factura.EstadoSRI = "AUTORIZADO";
                            factura.Estado = "Autorizada";
                            factura.FechaAutorizacion = DateTime.Now; // O mapear autorizacion.FechaAutorizacion
                            factura.MensajeErrorSRI = null;
                        }
                        else
                        {
                            // No Autorizado o Pendiente
                            var msgError = "SRI No Autorizó en reintento.";
                            if (autorizacion?.Mensajes != null)
                            {
                                var detalles = autorizacion.Mensajes
                                    .Select(m => $"{m.Mensaje} ({m.InformacionAdicional})");
                                msgError = string.Join("; ", detalles);
                            }

                            // Si NO está AUTORIZADO, actualizamos el estado y el mensaje.
                            // Si el estado es temporal (ej. EN PROCESO), lo dejamos como RECIBIDA para reintentar después.
                            if (autorizacion != null && autorizacion.Estado == "NO AUTORIZADO")
                            {
                                factura.EstadoSRI = "RECHAZADA";
                                factura.Estado = "Rechazada";
                            }
                            // Si el estado es nulo o temporal, solo actualizamos el mensaje, manteniendo EstadoSRI = RECIBIDA
                            factura.MensajeErrorSRI = msgError;
                        }
                    }

                    // Guardar los cambios si hubo actualización en la autorización
                    _context.Facturas.Update(factura);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Guardar un error genérico si falla la conexión o el código
                    factura.MensajeErrorSRI = $"Error interno en reintento: {ex.Message}";
                    _context.Facturas.Update(factura);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Error reintentando factura {factura.Id}: {ex.Message}");
                }
            }
        }

        // ============================================================
        //  REINTENTAR UNA FACTURA ESPECÍFICA
        // ============================================================
        public async Task ReintentarFacturaAsync(int id)
        {
            var factura = await _context.Facturas.FindAsync(id);

            if (factura == null)
                throw new Exception("Factura no encontrada.");

            if (factura.EstadoSRI == "AUTORIZADO")
                throw new Exception("Esta factura ya está autorizada.");

            if (string.IsNullOrEmpty(factura.XmlGenerado))
                throw new Exception("La factura no tiene XML generado. No se puede reenviar.");

            try
            {
                // ETAPA 1: RECEPCIÓN (si no está RECIBIDA)
                if (factura.EstadoSRI != "RECIBIDA")
                {
                    byte[] xmlBytes = Encoding.UTF8.GetBytes(factura.XmlGenerado);
                    string respuestaRecepcionRaw = await _sriClient.EnviarRecepcionAsync(xmlBytes);
                    var respuestaRecepcion = XmlHelper.Deserialize<RespuestaSolicitud>(respuestaRecepcionRaw);

                    if (respuestaRecepcion != null && respuestaRecepcion.Estado == "RECIBIDA")
                    {
                        factura.EstadoSRI = "RECIBIDA";
                        factura.MensajeErrorSRI = null;
                    }
                    else
                    {
                        // Manejo detallado de errores de recepción (DEVUELTA)
                        var msgError = "Error de Recepción. Deserialización fallida o respuesta incompleta.";

                        if (respuestaRecepcion != null && respuestaRecepcion.Comprobantes != null)
                        {
                            var listaErrores = respuestaRecepcion.Comprobantes
                                .SelectMany(c => c.Mensajes)
                                .Select(m => $"{m.Identificador}: {m.Mensaje} - {m.InformacionAdicional}");

                            if (listaErrores.Any())
                                msgError = string.Join(" | ", listaErrores);
                        }

                        factura.EstadoSRI = "DEVUELTA";
                        factura.Estado = "Devuelta";
                        factura.MensajeErrorSRI = msgError;

                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();

                        // 🌟 CORRECCIÓN CLAVE: Lanzar la excepción con el mensaje DETALLADO (msgError)
                        throw new Exception($"Error en Recepción SRI: {msgError}");
                    }

                    _context.Facturas.Update(factura);
                    await _context.SaveChangesAsync();
                }

                // ETAPA 2: AUTORIZACIÓN (si está RECIBIDA)
                if (factura.EstadoSRI == "RECIBIDA")
                {
                    await Task.Delay(1000);

                    string respuestaAuthRaw = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);
                    var respuestaAuth = XmlHelper.Deserialize<RespuestaAutorizacion>(respuestaAuthRaw);
                    var autorizacion = respuestaAuth?.Autorizaciones?.FirstOrDefault();

                    if (autorizacion != null && autorizacion.Estado == "AUTORIZADO")
                    {
                        factura.EstadoSRI = "AUTORIZADO";
                        factura.Estado = "Autorizada";
                        factura.FechaAutorizacion = DateTime.Now;
                        factura.MensajeErrorSRI = null;
                    }
                    else
                    {
                        // Error en Autorización
                        var msgError = "SRI No Autorizó el reintento.";
                        if (autorizacion?.Mensajes != null)
                        {
                            var detalles = autorizacion.Mensajes
                                .Select(m => $"{m.Mensaje} ({m.InformacionAdicional})");
                            msgError = string.Join("; ", detalles);
                        }

                        if (autorizacion != null && autorizacion.Estado == "NO AUTORIZADO")
                        {
                            factura.EstadoSRI = "RECHAZADA";
                            factura.Estado = "Rechazada";
                        }

                        factura.MensajeErrorSRI = msgError;

                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();

                        // 🌟 CORRECCIÓN CLAVE: Lanzar la excepción con el mensaje DETALLADO (msgError)
                        throw new Exception($"Error en Autorización SRI: {msgError}");
                    }
                }

                _context.Facturas.Update(factura);
                await _context.SaveChangesAsync();
            }
            catch
            {
                throw;
            }
        }

    }
}
