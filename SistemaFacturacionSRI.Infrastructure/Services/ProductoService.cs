using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Productos;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class ProductoService : IProductoService
    {
        private readonly AppDbContext _context;

        public ProductoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Producto> AddProductoAsync(CreateProducto producto)
        {
            // Verificar duplicado
            if (await _context.Productos.AnyAsync(p => p.CodigoPrincipal == producto.CodigoPrincipal))
                throw new InvalidOperationException("Ya existe un producto con ese código principal.");

            var nuevo = new Producto
            {
                CodigoPrincipal = producto.CodigoPrincipal,
                CodigoAuxiliar = producto.CodigoAuxiliar,
                Descripcion = producto.Descripcion,
                TipoProducto = producto.TipoProducto,
                PrecioUnitario = producto.PrecioUnitario,
                PrecioCompra = producto.PrecioCompra,
                PorcentajeIVA = producto.PorcentajeIVA,
                CodigoICE = producto.CodigoICE,
                PorcentajeICE = producto.PorcentajeICE,
                CodigoIRBPNR = producto.CodigoIRBPNR,
                PorcentajeIRBPNR = producto.PorcentajeIRBPNR,
                Stock = producto.Stock,
                FechaRegistro = DateTime.Now
            };

            _context.Productos.Add(nuevo);
            await _context.SaveChangesAsync();
            return nuevo;
        }

        public async Task<List<Producto>> GetAllProductosAsync()
        {
            return await _context.Productos
                .OrderBy(p => p.CodigoPrincipal)
                .ToListAsync();
        }

        public async Task<Producto?> GetProductoByIdAsync(int id)
        {
            return await _context.Productos.FindAsync(id);
        }

        public async Task<bool> UpdateProductoAsync(UpdateProducto producto)
        {
            var existente = await _context.Productos.FindAsync(producto.Id);
            if (existente == null) return false;

            existente.CodigoPrincipal = producto.CodigoPrincipal;
            existente.CodigoAuxiliar = producto.CodigoAuxiliar;
            existente.Descripcion = producto.Descripcion;
            existente.TipoProducto = producto.TipoProducto;
            existente.PrecioUnitario = producto.PrecioUnitario;
            existente.PrecioCompra = producto.PrecioCompra;
            existente.PorcentajeIVA = producto.PorcentajeIVA;
            existente.CodigoICE = producto.CodigoICE;
            existente.PorcentajeICE = producto.PorcentajeICE;
            existente.CodigoIRBPNR = producto.CodigoIRBPNR;
            existente.PorcentajeIRBPNR = producto.PorcentajeIRBPNR;
            existente.Stock = producto.Stock;
            existente.Estado = producto.Estado;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProductoAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return false;

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
