using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Necesario para leer appsettings
using System.Text;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.SRI.Services; // Importar tu servicio SRI

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;
        private readonly FacturaElectronicaService _sriService; // Inyección del servicio SRI
        private readonly IConfiguration _configuration;         // Para leer ruta y clave de firma

        public FacturaService(
            AppDbContext context,
            FacturaElectronicaService sriService,
            IConfiguration configuration)
        {
            _context = context;
            _sriService = sriService;
            _configuration = configuration;
        }

        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            // USAR TRANSACCIÓN: Si falla el descuento de stock o la firma, no se guarda nada
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // --- 1. VALIDACIONES Y CREACIÓN INICIAL ---

                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null) throw new Exception("Cliente no encontrado.");

                var nuevaFactura = new Factura
                {
                    ClienteId = dto.ClienteId,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente",
                    Detalles = new List<DetalleFactura>()
                };

                decimal subtotalFactura = 0;
                decimal totalIvaFactura = 0;

                // --- 2. PROCESAR DETALLES Y STOCK (FIFO) ---

                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(item.ProductoId);
                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    if (producto.Stock < item.Cantidad)
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}");

                    // Lógica FIFO
                    var lotesDisponibles = await _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaCaducidad.HasValue ? l.FechaCaducidad.Value : l.FechaRegistro)
                        .ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar == 0) break;
                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);

                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;
                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                        throw new Exception($"Inconsistencia en lotes para el producto {producto.Descripcion}.");

                    // Actualizar Stock Global
                    producto.Stock -= item.Cantidad;

                    // Cálculos
                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;
                    // Cálculo de IVA: (Precio * Cantidad) * (Porcentaje / 100)
                    decimal ivaLinea = subtotalLinea * (producto.TarifaIva.Porcentaje / 100m);


                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario,
                        Subtotal = subtotalLinea
                    });
                }

                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;

                // --- 3. GUARDADO PREVIO (Para obtener el ID/Secuencial) ---
                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync(); // Aquí se genera el nuevaFactura.Id

                // --- 4. INTEGRACIÓN SRI (Generación XML y Firma) ---

                // Obtener configuración desde appsettings.json
                string rutaFirma = _configuration["FirmaElectronica:Ruta"];
                string claveFirma = _configuration["FirmaElectronica:Clave"];

                if (string.IsNullOrEmpty(rutaFirma) || string.IsNullOrEmpty(claveFirma))
                    throw new Exception("No se ha configurado la firma electrónica en el sistema.");

                // Generar XML firmado (Esto devuelve bytes)
                // Nota: El servicio SRI usa el nuevaFactura.Id que se acaba de generar para el secuencial
                byte[] xmlBytes = _sriService.GenerarXmlFirmado(nuevaFactura, rutaFirma, claveFirma);

                // Convertir bytes a string para guardar en BD
                nuevaFactura.XmlGenerado = Encoding.UTF8.GetString(xmlBytes);

                // El servicio _sriService ya actualizó nuevaFactura.ClaveAcceso internamente por referencia,
                // pero aseguramos actualizando el estado de la entidad.
                _context.Facturas.Update(nuevaFactura);
                await _context.SaveChangesAsync();

                // --- 5. CONFIRMAR TRANSACCIÓN ---
                await transaction.CommitAsync();

                return nuevaFactura;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}