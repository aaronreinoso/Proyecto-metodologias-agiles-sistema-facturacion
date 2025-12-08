using Microsoft.EntityFrameworkCore;
using System.Text;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.Helpers;
using SistemaFacturacionSRI.Infrastructure.SRI.Models;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class NotaCreditoService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _facturaElectronicaService;
        private readonly SriSoapClient _sriClient;

        public NotaCreditoService(
            AppDbContext context,
            FacturaElectronicaService facturaElectronicaService,
            SriSoapClient sriClient)
        {
            _context = context;
            _facturaElectronicaService = facturaElectronicaService;
            _sriClient = sriClient;
        }

        public async Task<NotaCredito> CrearNotaCreditoAsync(int facturaId, string motivo, List<DetalleNotaCredito> itemsDevolucion)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // =====================================================================
                // PASO 0: CONFIGURACIÓN
                // =====================================================================
                var configEmpresa = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
                if (configEmpresa == null)
                    throw new Exception("Configuración de empresa no encontrada.");

                if (string.IsNullOrEmpty(configEmpresa.ClaveFirma))
                    throw new Exception("Firma electrónica no configurada.");

                // =====================================================================
                // PASO 1: VALIDAR FACTURA
                // =====================================================================
                var factura = await _context.Facturas
                    .Include(f => f.NotasCredito)
                    .Include(f => f.Cliente)
                    .Include(f => f.Detalles) // Incluir detalles para validar precios si fuera necesario
                    .FirstOrDefaultAsync(f => f.Id == facturaId);

                if (factura == null) throw new Exception("Factura no encontrada.");
                if (factura.EstadoSRI != "AUTORIZADO") throw new Exception("Solo se puede emitir Nota de Crédito a facturas AUTORIZADAS.");

                // =====================================================================
                // PASO 2: CÁLCULOS Y CREACIÓN DE ENTIDAD
                // =====================================================================
                var nuevaNC = new NotaCredito
                {
                    FacturaId = facturaId,
                    Motivo = motivo,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente",
                    EstadoSRI = "PENDIENTE",

                    Detalles = new List<DetalleNotaCredito>()
                };

                decimal subtotalNC = 0;
                decimal totalIvaNC = 0;

                // Procesar ítems y recalcular valores con datos reales de la BD
                foreach (var item in itemsDevolucion)
                {
                    var producto = await _context.Productos
                        .Include(p => p.TarifaIva)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null)
                        throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    // Validamos que la cantidad a devolver sea lógica (opcional: validar contra cantidad facturada)
                    if (item.Cantidad <= 0)
                        throw new Exception($"La cantidad para el producto {producto.Descripcion} debe ser mayor a 0.");

                    // Usamos el precio del producto actual o podrías buscar el precio histórico en factura.Detalles
                    // Por simplicidad y consistencia, aquí usamos el precio actual del producto o el que viene en el item si se mapeó
                    decimal precioUnitario = producto.PrecioUnitario;

                    decimal subtotalLinea = item.Cantidad * precioUnitario;
                    decimal porcentajeIva = producto.TarifaIva?.Porcentaje ?? 0m;
                    decimal ivaLinea = subtotalLinea * (porcentajeIva / 100m);

                    subtotalNC += subtotalLinea;
                    totalIvaNC += ivaLinea;

                    nuevaNC.Detalles.Add(new DetalleNotaCredito
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = precioUnitario,
                        Subtotal = subtotalLinea,
                        Producto = producto // Vinculamos para que EF lo tenga disponible al generar XML
                    });
                }

                nuevaNC.Subtotal = subtotalNC;
                nuevaNC.TotalIVA = totalIvaNC;
                nuevaNC.Total = subtotalNC + totalIvaNC;

                // =====================================================================
                // PASO 3: VALIDAR SALDOS (CRÍTICO)
                // =====================================================================
                // Sumamos solo las NC que ya fueron autorizadas o están en proceso válido
                decimal totalYaDevuelto = factura.NotasCredito
                    .Where(n => n.EstadoSRI == "AUTORIZADO" || n.EstadoSRI == "RECIBIDA")
                    .Sum(n => n.Total);

                decimal saldoDisponible = factura.Total - totalYaDevuelto;

                // Redondeamos para evitar problemas de precisión flotante
                decimal totalNuevaNCRedondeado = Math.Round(nuevaNC.Total, 2);
                saldoDisponible = Math.Round(saldoDisponible, 2);

                if (totalNuevaNCRedondeado > saldoDisponible)
                {
                    throw new Exception($"El monto de la Nota de Crédito ({totalNuevaNCRedondeado:C}) supera el saldo disponible de la factura ({saldoDisponible:C}).");
                }

                // Guardamos preliminarmente para generar el ID (Secuencial)
                _context.NotasCredito.Add(nuevaNC);
                await _context.SaveChangesAsync();

                // =====================================================================
                // PASO 4: XML -> FIRMA -> SRI (REUTILIZANDO LÓGICA ROBUSTA)
                // =====================================================================

                // A. Firma
                string claveFirma = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);

                byte[] xmlBytes = _facturaElectronicaService.GenerarXmlNotaCreditoFirmado(
                    nuevaNC,
                    configEmpresa,
                    claveFirma);

                nuevaNC.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                // B. Envío Recepción
                string respuestaRecepcionRaw = await _sriClient.EnviarRecepcionAsync(xmlBytes);
                var respuestaRecepcion = XmlHelper.Deserialize<RespuestaSolicitud>(respuestaRecepcionRaw);

                if (respuestaRecepcion != null && respuestaRecepcion.Estado == "RECIBIDA")
                {
                    nuevaNC.EstadoSRI = "RECIBIDA";

                    // Espera prudencial
                    await Task.Delay(1500);

                    // C. Solicitud Autorización
                    string respuestaAuthRaw = await _sriClient.EnviarAutorizacionAsync(nuevaNC.ClaveAcceso);
                    var respuestaAuth = XmlHelper.Deserialize<RespuestaAutorizacion>(respuestaAuthRaw);
                    var autorizacion = respuestaAuth?.Autorizaciones?.FirstOrDefault();

                    if (autorizacion != null && autorizacion.Estado == "AUTORIZADO")
                    {
                        nuevaNC.EstadoSRI = "AUTORIZADO";
                       
                        nuevaNC.FechaAutorizacion = DateTime.Now;

                        // Aquí podrías sumar stock nuevamente si es política de la empresa
                        // DevolverStock(nuevaNC.Detalles); 
                    }
                    else
                    {
                        nuevaNC.EstadoSRI = "RECHAZADA";
                        

                        string msgError = "SRI No Autorizó.";
                        if (autorizacion?.Mensajes != null)
                        {
                            var detalles = autorizacion.Mensajes.Select(m => $"{m.Mensaje} ({m.InformacionAdicional})");
                            msgError = string.Join("; ", detalles);
                        }
                        else
                        {
                            string rawSnippet = respuestaAuthRaw.Substring(0, Math.Min(respuestaAuthRaw.Length, 300));
                            msgError = $"Error Autorización: {rawSnippet}...";
                        }
                        nuevaNC.MensajeErrorSRI = msgError;
                    }
                }
                else
                {
                    nuevaNC.EstadoSRI = "DEVUELTA";
                    

                    string msgError = "Error en Recepción.";
                    if (respuestaRecepcion?.Comprobantes != null)
                    {
                        var listaErrores = respuestaRecepcion.Comprobantes
                            .SelectMany(c => c.Mensajes)
                            .Select(m => $"{m.Identificador}: {m.Mensaje} - {m.InformacionAdicional}");

                        if (listaErrores.Any()) msgError = string.Join(" | ", listaErrores);
                    }
                    else
                    {
                        string rawSnippet = respuestaRecepcionRaw.Substring(0, Math.Min(respuestaRecepcionRaw.Length, 300));
                        msgError = $"Error Recepción RAW: {rawSnippet}...";
                    }
                    nuevaNC.MensajeErrorSRI = msgError;
                }

                _context.NotasCredito.Update(nuevaNC);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return nuevaNC;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}