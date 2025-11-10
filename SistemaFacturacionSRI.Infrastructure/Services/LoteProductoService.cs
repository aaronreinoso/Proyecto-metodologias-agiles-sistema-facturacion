using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.LotesProducto;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class LoteProductoService : ILoteProductoService
    {
        private readonly AppDbContext _context;

        public LoteProductoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LoteProducto> AddLoteAsync(CreateLoteProducto loteDto)
        {
            // 1. Validar que el producto asociado exista.
            var productoAsociado = await _context.Productos.FindAsync(loteDto.ProductoId);
            if (productoAsociado == null)
            {
                throw new InvalidOperationException($"No se puede registrar el lote porque el producto con ID {loteDto.ProductoId} no existe.");
            }

            // 2. Mapear el DTO a la entidad de dominio.
            var nuevoLote = new LoteProducto
            {
                ProductoId = loteDto.ProductoId,
                CantidadInicial = loteDto.Cantidad,
                CantidadActual = loteDto.Cantidad,
                PrecioCompra = loteDto.PrecioCompra,
                FechaCaducidad = loteDto.FechaCaducidad,
                FechaRegistro = DateTime.UtcNow
            };

            // 3. Agregar el nuevo lote y actualizar el stock del producto.
            _context.LotesProducto.Add(nuevoLote);
            productoAsociado.Stock += loteDto.Cantidad;

            // 4. Guardar todos los cambios en una sola transacción.
            await _context.SaveChangesAsync();

            return nuevoLote;
        }

        public async Task<LoteProducto> GetLoteByIdAsync(int id)
        {
            return await _context.LotesProducto.FindAsync(id);
        }

        public async Task<IEnumerable<LoteProducto>> GetLotesByProductoIdAsync(int productoId)
        {
            return await _context.LotesProducto
                .Where(lote => lote.ProductoId == productoId)
                .OrderByDescending(lote => lote.FechaRegistro)
                .ToListAsync();
        }
    }
}