using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System.Linq; // Necesario para .OrderBy()
using System.Collections.Generic; // Necesario para List

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class FacturaService : IFacturaService
    {
        private readonly AppDbContext _context;

        public FacturaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Factura> CrearFacturaAsync(CreateFacturaDto dto)
        {
            // USAR TRANSACCIÓN: Si falla el descuento de stock, no se guarda la factura
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Validar Cliente
                var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
                if (cliente == null) throw new Exception("Cliente no encontrado.");

                var nuevaFactura = new Factura
                {
                    ClienteId = dto.ClienteId,
                    FechaEmision = DateTime.Now,
                    Estado = "Pendiente", // Estado inicial antes del SRI
                    Detalles = new List<DetalleFactura>()
                };

                decimal subtotalFactura = 0;
                decimal totalIvaFactura = 0;

                // 2. Procesar cada producto (Detalle)
                foreach (var item in dto.Detalles)
                {
                    // Necesitamos cargar el producto, su stock, y su TarifaIva asociada
                    var producto = await _context.Productos
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    // Validar Stock
                    if (producto.Stock < item.Cantidad)
                        // CORRECCIÓN: Se usa la nueva propiedad Descripcion
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}");

                    // --- LÓGICA DE ROTACIÓN (FEFO / FIFO) PARA LOTES ---
                    IQueryable<LoteProducto> queryLotes = _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0);

                    if (producto.EsPerecible)
                    {
                        // FEFO: Ordenar por la fecha de caducidad más próxima (más antigua)
                        // Esto garantiza que el inventario perecible rote primero lo que está por vencer.
                        queryLotes = queryLotes
                            .OrderBy(l => l.FechaCaducidad); // Los nulos van al final, los no nulos (fechas) se ordenan
                    }
                    else
                    {
                        // FIFO: Ordenar por la fecha de registro (el lote que entró primero)
                        queryLotes = queryLotes.OrderBy(l => l.FechaRegistro);
                    }

                    var lotesDisponibles = await queryLotes.ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar == 0) break;

                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);

                        // Actualizamos el lote
                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;

                        // Actualizamos el lote en el contexto 
                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                    {
                        // CORRECCIÓN: Se usa la nueva propiedad Descripcion
                        throw new Exception($"Inconsistencia en lotes para el producto {producto.Descripcion}. Falta stock físico.");
                    }

                    // 3. Actualizar Stock Global del Producto
                    producto.Stock -= item.Cantidad;

                    // 4. Calcular Valores Monetarios
                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;

                    // CORRECCIÓN: Usamos la relación TarifaIva para obtener el porcentaje
                    // Si por alguna razón la relación es nula, usamos 0% como fallback.
                    decimal ivaPorcentaje = producto.TarifaIva?.Porcentaje ?? 0.00m;
                    decimal ivaLinea = subtotalLinea * (ivaPorcentaje / 100m);

                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    // 5. Crear Detalle
                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario, // Precio histórico de venta
                        Subtotal = subtotalLinea
                    });
                }

                // 6. Asignar Totales a la Cabecera
                nuevaFactura.Subtotal = subtotalFactura;
                nuevaFactura.TotalIVA = totalIvaFactura;
                nuevaFactura.Total = subtotalFactura + totalIvaFactura;

                // 7. Guardar en Base de Datos
                _context.Facturas.Add(nuevaFactura);
                await _context.SaveChangesAsync();

                // Confirmar transacción
                await transaction.CommitAsync();

                return nuevaFactura;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // Relanzar error para que el Controller lo maneje
            }
        }
    }
}