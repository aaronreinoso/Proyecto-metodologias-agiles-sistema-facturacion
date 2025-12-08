using Microsoft.EntityFrameworkCore;
using System.Text;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.Helpers;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _facturaElectronicaService;
        private readonly SriSoapClient _sriClient;

        // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
        // NUEVOS CAMPOS AGREGADOS (PDF + EMAIL)
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService facturaElectronicaService,
            SriSoapClient sriClient,
            IPdfService pdfService,        // <<< NUEVO
            IEmailService emailService     // <<< NUEVO
        )
        {
            _context = context;
            _facturaElectronicaService = facturaElectronicaService;
            _sriClient = sriClient;

            // <<< NUEVAS ASIGNACIONES >>>
            _pdfService = pdfService;
            _emailService = emailService;
        }

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

                // =====================================================================
                // PASO 2: DETALLES, LÓGICA FIFO Y CÁLCULOS
                // =====================================================================
                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos
                        .Include(p => p.TarifaIva)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

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

                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync();

                // =====================================================================
                // PASO 4: XML -> FIRMA -> ENVÍO SRI
                // =====================================================================

                string claveFirmaDesencriptada = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);
                byte[] xmlBytes = _facturaElectronicaService.GenerarXmlFirmado(
                    nuevaFactura, configEmpresa, claveFirmaDesencriptada);

                nuevaFactura.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

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

                        // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
                        // NUEVO BLOQUE: GENERAR PDF Y ENVIAR CORREO AL CLIENTE
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
                        // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
                    }
                    else
                    {
                        nuevaFactura.EstadoSRI = "RECHAZADA";
                        nuevaFactura.Estado = "Rechazada";
                        nuevaFactura.MensajeErrorSRI = "El SRI rechazó la autorización.";
                    }
                }
                else
                {
                    nuevaFactura.EstadoSRI = "DEVUELTA";
                    nuevaFactura.Estado = "Devuelta";
                    nuevaFactura.MensajeErrorSRI = "Error en recepción: " + respuestaRecepcion;
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

        // ================================================================
        // HISTORIAL DE FACTURAS (NO SE MODIFICÓ NADA)
        // ================================================================
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




        public async Task ProcesarFacturasPendientesSriAsync()
        {
            // 1. Buscar facturas que quedaron en 'RECIBIDA' (Falta autorización)
            //    o 'PENDIENTE' con XML generado pero sin respuesta (Error de red inicial)
            var facturasEstancadas = await _context.Facturas
                .Where(f => (f.EstadoSRI == "RECIBIDA" || (f.EstadoSRI == "PENDIENTE" && f.XmlGenerado != null))
                            && f.Estado != "Autorizada"
                            && f.Estado != "Rechazada")
                .ToListAsync();

            foreach (var factura in facturasEstancadas)
            {
                try
                {
                    // CASO A: Si está PENDIENTE, intentamos enviarla primero (Recepción)
                    if (factura.EstadoSRI == "PENDIENTE")
                    {
                        byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(factura.XmlGenerado);
                        string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                        if (respuestaRecepcion.Contains("RECIBIDA"))
                        {
                            factura.EstadoSRI = "RECIBIDA";
                            // Guardamos el avance y continuamos inmediatamente a autorización
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                        }
                        else if (respuestaRecepcion.Contains("CLAVE ACCESO REGISTRADA"))
                        {
                            // Regla de Negocio 2: Ya la tenían, pasamos a RECIBIDA para pedir autorización
                            factura.EstadoSRI = "RECIBIDA";
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            // Si falla con otro error (Devuelta), lo guardamos y saltamos a la siguiente
                            factura.EstadoSRI = "DEVUELTA";
                            factura.MensajeErrorSRI = respuestaRecepcion;
                            _context.Facturas.Update(factura);
                            await _context.SaveChangesAsync();
                            continue;
                        }
                    }

                    // CASO B: Si ya está RECIBIDA (o acabamos de pasarla), pedimos Autorización
                    if (factura.EstadoSRI == "RECIBIDA")
                    {
                        // Pequeña pausa de cortesía al SRI
                        await Task.Delay(1000);

                        string respuestaAuth = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);

                        if (respuestaAuth.Contains("AUTORIZADO"))
                        {
                            factura.EstadoSRI = "AUTORIZADO";
                            factura.Estado = "Autorizada";
                            factura.FechaAutorizacion = DateTime.Now;
                            factura.MensajeErrorSRI = null; // Limpiar errores previos

                            // Opcional: Aquí podrías disparar el envío de correo nuevamente si tienes el servicio inyectado
                        }
                        else if (respuestaAuth.Contains("RECHAZADA"))
                        {
                            factura.EstadoSRI = "RECHAZADA";
                            factura.Estado = "Rechazada";
                            factura.MensajeErrorSRI = "Rechazada por el SRI en reintento.";
                        }
                        // Si sigue "EN PROCESO", no hacemos nada, se intentará en el siguiente ciclo
                    }

                    // Guardamos los cambios finales de esta factura
                    _context.Facturas.Update(factura);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Si falla el reintento (ej. sin internet), no rompemos el bucle, solo logueamos
                    Console.WriteLine($"Error reintentando factura {factura.Id}: {ex.Message}");
                }
            }
        }


        // ... (otros métodos existentes) ...

        public async Task ReintentarFacturaAsync(int id)
        {
            // 1. Buscar la factura
            var factura = await _context.Facturas.FindAsync(id);

            if (factura == null)
                throw new Exception("Factura no encontrada.");

            if (factura.EstadoSRI == "AUTORIZADO")
                throw new Exception("Esta factura ya está autorizada.");

            // Validar que tengamos lo mínimo necesario
            if (string.IsNullOrEmpty(factura.XmlGenerado))
                throw new Exception("La factura no tiene XML generado. No se puede reenviar.");

            try
            {
                // =========================================================================
                // ETAPA 1: RECEPCIÓN (Si está Pendiente, Devuelta o nunca se envió)
                // =========================================================================
                if (factura.EstadoSRI != "RECIBIDA")
                {
                    byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(factura.XmlGenerado);
                    string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                    if (respuestaRecepcion.Contains("RECIBIDA"))
                    {
                        factura.EstadoSRI = "RECIBIDA";
                        factura.MensajeErrorSRI = null; // Limpiamos errores previos
                    }
                    else if (respuestaRecepcion.Contains("CLAVE ACCESO REGISTRADA"))
                    {
                        // Ya la tenían, avanzamos forzosamente a RECIBIDA para pedir autorización
                        factura.EstadoSRI = "RECIBIDA";
                        factura.MensajeErrorSRI = null;
                    }
                    else
                    {
                        // Falló la recepción de nuevo
                        factura.EstadoSRI = "DEVUELTA";
                        factura.Estado = "Devuelta";
                        factura.MensajeErrorSRI = respuestaRecepcion;

                        // Guardamos y salimos, no tiene caso intentar autorizar
                        _context.Facturas.Update(factura);
                        await _context.SaveChangesAsync();
                        throw new Exception($"Error en Recepción SRI: {respuestaRecepcion}");
                    }

                    // Guardamos el avance intermedio
                    _context.Facturas.Update(factura);
                    await _context.SaveChangesAsync();
                }

                // =========================================================================
                // ETAPA 2: AUTORIZACIÓN (Si ya está RECIBIDA, pedimos la respuesta)
                // =========================================================================
                if (factura.EstadoSRI == "RECIBIDA")
                {
                    // Pequeña espera para asegurar que el SRI procesó la recepción (si acabamos de enviar)
                    await Task.Delay(1000);

                    string respuestaAuth = await _sriClient.EnviarAutorizacionAsync(factura.ClaveAcceso);

                    if (respuestaAuth.Contains("AUTORIZADO"))
                    {
                        factura.EstadoSRI = "AUTORIZADO";
                        factura.Estado = "Autorizada";
                        factura.FechaAutorizacion = DateTime.Now;
                        factura.MensajeErrorSRI = null;
                    }
                    else if (respuestaAuth.Contains("RECHAZADA"))
                    {
                        factura.EstadoSRI = "RECHAZADA";
                        factura.Estado = "Rechazada";
                        // Aquí podrías parsear el XML de respuesta para sacar el motivo exacto, 
                        // por ahora guardamos un mensaje genérico o el XML crudo si es corto.
                        factura.MensajeErrorSRI = "El SRI rechazó el comprobante. Revise el portal o intente más tarde.";
                    }
                    else
                    {
                        // Sigue en procesamiento o error de conexión en autorización
                        // No cambiamos el estado, se queda en RECIBIDA para intentar luego
                        throw new Exception($"Respuesta SRI no definitiva: {respuestaAuth}");
                    }
                }

                // Guardado Final
                _context.Facturas.Update(factura);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Relanzamos la excepción para que el controlador sepa que falló y le avise al usuario
                throw;
            }
        }

    }
}
