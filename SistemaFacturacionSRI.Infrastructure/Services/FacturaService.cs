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
        private readonly FacturaElectronicaService _facturaElectronicaService; // Servicio de Generación XML y Firma
        private readonly SriSoapClient _sriClient;                             // Servicio de Envío SOAP al SRI

        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService facturaElectronicaService,
            SriSoapClient sriClient)
        {
            _context = context;
            _facturaElectronicaService = facturaElectronicaService;
            _sriClient = sriClient;
        }

        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            // USAR TRANSACCIÓN: Todo o nada para proteger el stock y la integridad de la factura
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // =============================================================================
                // PASO 0: RECUPERAR CONFIGURACIÓN SRI DE LA BD
                // =============================================================================
                var configEmpresa = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();

                if (configEmpresa == null)
                    throw new Exception("No se han configurado los datos de la empresa ni la firma electrónica en el sistema.");

                if (configEmpresa.FirmaElectronica == null || string.IsNullOrEmpty(configEmpresa.ClaveFirma))
                    throw new Exception("La firma electrónica no ha sido cargada en la configuración.");

                // =============================================================================
                // PASO 1: VALIDACIONES Y CREACIÓN INICIAL
                // =============================================================================
                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null) throw new Exception("Cliente no encontrado.");

                var nuevaFactura = new Factura
                {
                    ClienteId = dto.ClienteId,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente",       // Estado interno del sistema
                    EstadoSRI = "PENDIENTE",    // Estado del ciclo SRI
                    Detalles = new List<DetalleFactura>()
                };

                decimal subtotalFactura = 0;
                decimal totalIvaFactura = 0; 

                // =============================================================================
                // PASO 2: PROCESAR DETALLES, LÓGICA FIFO Y CÁLCULOS
                // =============================================================================
                foreach (var item in dto.Detalles)
                {
                    // Incluimos TarifaIva para calcular impuestos correctamente
                    var producto = await _context.Productos
                        .Include(p => p.TarifaIva)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    // Validación de Stock Global
                    if (producto.Stock < item.Cantidad)
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}. Stock actual: {producto.Stock}");

                    decimal costoTotalLotesConsumidos = 0;

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

                        // ⭐ AGREGADO: Acumular el costo de las unidades tomadas del lote actual
                        costoTotalLotesConsumidos += aTomar * lote.PrecioCompraUnitario;


                        // Descontar del lote
                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;

                        // Marcar lote como modificado
                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                    {
                        throw new Exception($"Inconsistencia crítica: El stock global indicaba disponibilidad, pero no hay suficientes lotes para el producto {producto.Descripcion}.");
                    }


                    // ⭐ NUEVO: Calcular el Costo Unitario Promedio Ponderado de la venta
                    // Esto promedia el costo si se consumieron unidades de lotes con diferentes precios de compra
                    decimal costoUnitarioPonderado = costoTotalLotesConsumidos / item.Cantidad;

                    // Actualizar Stock Global
                    producto.Stock -= item.Cantidad;



                    // --- CÁLCULOS MONETARIOS ---
                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;

                    // Verificar que tenga tarifa asignada, si no, asumir 0
                    decimal porcentajeIva = producto.TarifaIva?.Porcentaje ?? 0m;
                    decimal ivaLinea = subtotalLinea * (porcentajeIva / 100m);

                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    // Agregar Detalle a la entidad
                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario,
                        Subtotal = subtotalLinea,
                        CostoUnitario = costoUnitarioPonderado, // ⭐ ASIGNACIÓN CRÍTICA
                        // Guardamos referencia del producto para usarla en el mapeo XML sin consultar la BD de nuevo
                        Producto = producto
                    });
                }

                // Totales finales de cabecera
                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;

                // Asignamos cliente completo para el XML
                nuevaFactura.Cliente = cliente;

                // =============================================================================
                // PASO 3: GUARDADO PREVIO (NECESARIO PARA OBTENER EL ID/SECUENCIAL)
                // =============================================================================
                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync(); // Genera el ID (Secuencial)

                // =============================================================================
                // PASO 4: INTEGRACIÓN SRI (GENERACIÓN XML -> FIRMA -> ENVÍO)
                // =============================================================================

                // A. Desencriptar la clave de la firma guardada en BD
                string claveFirmaDesencriptada = EncryptionHelper.Decrypt(configEmpresa.ClaveFirma);

                // B. Generar XML Firmado
                // El servicio usa 'configEmpresa' para obtener los bytes de la firma (.p12) y los datos del emisor
                byte[] xmlBytes = _facturaElectronicaService.GenerarXmlFirmado(nuevaFactura, configEmpresa, claveFirmaDesencriptada);

                // Guardamos el XML final en la base de datos
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
                        nuevaFactura.Estado = "Rechazada"; // Estado interno
                        nuevaFactura.MensajeErrorSRI = "El SRI rechazó la autorización. Verifique el XML devuelto.";
                    }
                }
                else
                {
                    nuevaFactura.EstadoSRI = "DEVUELTA";
                    nuevaFactura.Estado = "Devuelta"; // Estado interno
                    nuevaFactura.MensajeErrorSRI = "Error en recepción: " + respuestaRecepcion;
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
                throw;
            }
        }





    



    // ... (resto del código anterior) ...

        public async Task<List<FacturaResumenDto>> ObtenerHistorialFacturasAsync()
        {
            // Recuperamos config para armar el número de factura visual (Establecimiento-Punto)
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            string serie = config != null
                ? $"{config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}"
                : "000-000";

            // Consultamos la BD
            var facturas = await _context.Facturas
                .Include(f => f.Cliente) // Importante para ver el nombre
                .OrderByDescending(f => f.FechaEmision) // Las más nuevas primero
                .Select(f => new FacturaResumenDto
                {
                    Id = f.Id,
                    // Formateamos secuencial a 9 dígitos
                    NumeroFactura = $"{serie}-{f.Id.ToString().PadLeft(9, '0')}",

                    ClienteNombre = f.Cliente != null ? f.Cliente.NombreCompleto : "Consumidor Final",
                    ClienteIdentificacion = f.Cliente != null ? f.Cliente.Identificacion : "9999999999999",
                    FechaEmision = f.FechaEmision,
                    Total = f.Total,

                    EstadoSRI = f.EstadoSRI ?? "PENDIENTE",
                    MensajeErrorSRI = f.MensajeErrorSRI, // Aquí cargamos el error si existe
                    ClaveAcceso = f.ClaveAcceso,
                    TieneXml = !string.IsNullOrEmpty(f.XmlGenerado)
                })
                .ToListAsync();

            return facturas;
        }
    } // Fin de la clase
}
