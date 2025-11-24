using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Configuration; // YA NO NECESITAMOS ESTO
using System.Text;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.SRI.Services;
using SistemaFacturacionSRI.Infrastructure.Helpers; // Para desencriptar la clave

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _firmaService; // Servicio de Firma y XML
        private readonly SriSoapClient _sriClient;                // Servicio de Envío al SRI (Nuevo)
        private readonly IConfiguration _configuration;
        private readonly FacturaElectronicaService _sriService;

        // Eliminamos IConfiguration del constructor porque ya no leemos appsettings
        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService firmaService,
            SriSoapClient sriClient,
            IConfiguration configuration)
        {
            _context = context;
            _firmaService = firmaService;
            _sriClient = sriClient;
            _configuration = configuration;
        }

        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            // USAR TRANSACCIÓN: Todo o nada.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // --- 0. RECUPERAR CONFIGURACIÓN SRI DE LA BD ---
                // Asumimos que solo hay un registro de configuración activo (o el primero)
                var configEmpresa = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();

                if (configEmpresa == null)
                    throw new Exception("No se ha configurado los datos de la empresa ni la firma electrónica en el sistema.");

                if (configEmpresa.FirmaElectronica == null || string.IsNullOrEmpty(configEmpresa.ClaveFirma))
                    throw new Exception("La firma electrónica no ha sido cargada en la configuración.");


                // --- 1. VALIDACIONES Y CREACIÓN ---
                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null) throw new Exception("Cliente no encontrado.");

                var nuevaFactura = new Factura
                {
                    ClienteId = dto.ClienteId,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente", // Estado interno
                    EstadoSRI = "PENDIENTE", // Estado SRI inicial
                    Detalles = new List<DetalleFactura>()
                };

                decimal subtotalFactura = 0;
                decimal totalIvaFactura = 0;

                // =============================================================================
                // PASO 2: PROCESAR DETALLES, LÓGICA FIFO Y CÁLCULOS
                // =============================================================================

                foreach (var item in dto.Detalles)
                {
                    // Incluimos TarifaIva para poder calcular impuestos
                    var producto = await _context.Productos
                        .Include(p => p.TarifaIva)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    // Validación de Stock Global
                    if (producto.Stock < item.Cantidad)
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}. Stock actual: {producto.Stock}");

                    // --- LÓGICA FIFO (First-In, First-Out) PARA LOTES ---
                    var lotesDisponibles = await _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaCaducidad.HasValue ? l.FechaCaducidad.Value : l.FechaRegistro)
                        .ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar == 0) break;

                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);

                        // Descontar del lote
                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;

                        // Marcar lote como modificado
                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                    {
                        // Esto es una salvaguarda de integridad
                        throw new Exception($"Inconsistencia crítica: El stock global indicaba disponibilidad, pero no hay suficientes lotes para el producto {producto.Descripcion}.");
                    }

                    // Actualizar Stock Global
                    producto.Stock -= item.Cantidad;

                    // --- CÁLCULOS MONETARIOS ---
                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;

                    // Verificar que tenga tarifa asignada, si no, asumir 0 para evitar crash
                    decimal porcentajeIva = producto.TarifaIva?.Porcentaje ?? 0m;
                    decimal ivaLinea = subtotalLinea * (porcentajeIva / 100m);
                    decimal ivaLinea = subtotalLinea * (producto.TarifaIva.Porcentaje / 100m);

                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    // Agregar Detalle
                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario,
                        Subtotal = subtotalLinea,
                        // Nota: Es útil guardar una referencia del objeto producto para el mapeo XML posterior
                        Producto = producto
                    });
                }

                // Totales finales
                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;

                // Necesitamos el objeto Cliente completo para el XML
                nuevaFactura.Cliente = cliente;

                // Necesitamos el objeto Cliente completo para el XML
                nuevaFactura.Cliente = cliente;

                // =============================================================================
                // PASO 3: GUARDADO PREVIO (NECESARIO PARA OBTENER EL ID/SECUENCIAL)
                // =============================================================================
                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync();

                // --- 4. INTEGRACIÓN SRI (Generación XML y Firma desde BD) ---

                // Desencriptamos la clave guardada en BD
                string claveFirmaDesencriptada = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);
                // A. Obtener credenciales de firma
                string rutaFirma = _configuration["FirmaElectronica:Ruta"];
                string claveFirma = _configuration["FirmaElectronica:Clave"];

                if (string.IsNullOrEmpty(rutaFirma) || string.IsNullOrEmpty(claveFirma))
                    throw new Exception("Configuración de firma electrónica no encontrada.");

                // B. Generar XML Firmado
                // El servicio actualiza la entidad 'nuevaFactura' con la ClaveAcceso generada
                byte[] xmlBytes = _firmaService.GenerarXmlFirmado(nuevaFactura, rutaFirma, claveFirma);

                nuevaFactura.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                // C. Enviar al Web Service de RECEPCIÓN
                string respuestaRecepcion = await _sriClient.EnviarRecepcionAsync(xmlBytes);

                if (respuestaRecepcion.Contains("RECIBIDA"))
                {
                    nuevaFactura.EstadoSRI = "RECIBIDA";

                    // Pequeña pausa para dar tiempo al SRI de procesar antes de autorizar
                    await Task.Delay(1500);

                    // D. Enviar al Web Service de AUTORIZACIÓN
                    string respuestaAutorizacion = await _sriClient.EnviarAutorizacionAsync(nuevaFactura.ClaveAcceso);

                    if (respuestaAutorizacion.Contains("AUTORIZADO"))
                    {
                        nuevaFactura.EstadoSRI = "AUTORIZADO";
                        nuevaFactura.Estado = "Autorizada";
                        nuevaFactura.FechaAutorizacion = DateTime.Now;
                    }
                    else
                    {
                        nuevaFactura.EstadoSRI = "RECHAZADA";
                        nuevaFactura.Estado = "Rechazada";
                        nuevaFactura.MensajeErrorSRI = "Verificar XML de respuesta en logs o correo.";
                        // Opcional: Podrías guardar 'respuestaAutorizacion' en un log de la BD
                    }
                }
                else
                {
                    nuevaFactura.EstadoSRI = "DEVUELTA";
                    nuevaFactura.Estado = "Devuelta";
                    nuevaFactura.MensajeErrorSRI = "Error en recepción (Firma inválida o XML mal formado).";
                }

                // =============================================================================
                // PASO 5: ACTUALIZACIÓN FINAL Y COMMIT
                // =============================================================================

                _context.Facturas.Update(nuevaFactura);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return nuevaFactura;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // El controlador manejará el error y lo mostrará al usuario
            }
        }
    }
}