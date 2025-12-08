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
    }
}
