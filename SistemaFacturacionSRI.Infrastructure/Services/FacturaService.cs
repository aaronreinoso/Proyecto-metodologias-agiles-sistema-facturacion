using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

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
                    var producto = await _context.Productos.FindAsync(item.ProductoId);
                    if (producto == null) throw new Exception($"Producto ID {item.ProductoId} no existe.");

                    if (producto.Stock < item.Cantidad)
                        throw new Exception($"Stock insuficiente para el producto: {producto.Descripcion}");

                    // --- LÓGICA FIFO (First-In, First-Out) PARA LOTES ---
                    // Obtenemos los lotes con stock, ordenados por fecha de caducidad (los más viejos primero)
                    var lotesDisponibles = await _context.LotesProducto
                        .Where(l => l.ProductoId == item.ProductoId && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaCaducidad.HasValue ? l.FechaCaducidad.Value : l.FechaRegistro)
                        .ToListAsync();

                    int cantidadPorDescontar = item.Cantidad;

                    foreach (var lote in lotesDisponibles)
                    {
                        Console.WriteLine(lote.CantidadActual);
                        if (cantidadPorDescontar == 0) break;

                        int aTomar = Math.Min(cantidadPorDescontar, lote.CantidadActual);

                        // Actualizamos el lote
                        lote.CantidadActual -= aTomar;
                        cantidadPorDescontar -= aTomar;

                        // Actualizamos el lote en el contexto (EF Core lo detecta, pero es buena práctica ser explícito si hay lógica compleja)
                        _context.Entry(lote).State = EntityState.Modified;
                    }

                    if (cantidadPorDescontar > 0)
                    {
                        // Esto no debería pasar si validamos producto.Stock < item.Cantidad, pero es una doble seguridad
                        throw new Exception($"Inconsistencia en lotes para el producto {producto.Descripcion}. Falta stock físico.");
                    }

                    // 3. Actualizar Stock Global del Producto
                    producto.Stock -= item.Cantidad;

                    // 4. Calcular Valores Monetarios
                    decimal subtotalLinea = item.Cantidad * producto.PrecioUnitario;
                    // Cálculo de IVA: (Precio * Cantidad) * (Porcentaje / 100)
                    decimal ivaLinea = subtotalLinea * (producto.TarifaIva.Porcentaje / 100m);


                    subtotalFactura += subtotalLinea;
                    totalIvaFactura += ivaLinea;

                    // 5. Crear Detalle
                    nuevaFactura.Detalles.Add(new DetalleFactura
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.PrecioUnitario, // Precio histórico
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