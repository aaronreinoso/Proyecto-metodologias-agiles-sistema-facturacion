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
            // 1. Validar que el producto asociado exista y obtener su bandera EsPerecible.
            // Ya no incluimos CategoriaProducto
            var productoAsociado = await _context.Productos.FindAsync(loteDto.ProductoId);

            if (productoAsociado == null)
            {
                throw new InvalidOperationException($"No se puede registrar el lote porque el producto con ID {loteDto.ProductoId} no existe.");
            }

            // --- LÓGICA DE CÁLCULO DE COSTO UNITARIO ---
            decimal costoUnitarioCalculado;
            decimal costoTotalCalculado;

            bool isTotalInput = loteDto.CostoTotalLote.HasValue;
            bool isUnitarioInput = loteDto.PrecioCompraUnitario.HasValue;

            if (isTotalInput)
            {
                if (loteDto.Cantidad <= 0)
                    throw new InvalidOperationException("La cantidad debe ser mayor a cero para calcular el costo unitario.");

                // Usamos el CostoTotalLote para calcular el unitario
                costoTotalCalculado = loteDto.CostoTotalLote!.Value;
                costoUnitarioCalculado = costoTotalCalculado / loteDto.Cantidad;
            }
            else if (isUnitarioInput)
            {
                // Usamos el PrecioUnitarioInput para calcular el total
                costoUnitarioCalculado = loteDto.PrecioCompraUnitario!.Value;
                costoTotalCalculado = costoUnitarioCalculado * loteDto.Cantidad;
            }
            else
            {
                throw new InvalidOperationException("Debe ingresar el costo total del lote o el costo unitario.");
            }

            // --- LÓGICA DE VALIDACIÓN DE CADUCIDAD ---
            // CORREGIDO: Leemos la bandera EsPerecible directamente del producto
            if (productoAsociado.EsPerecible && !loteDto.FechaCaducidad.HasValue)
            {
                throw new InvalidOperationException("Este producto es perecible y requiere registrar la fecha de caducidad del lote.");
            }

            // 2. Mapear el DTO a la entidad de dominio.
            var nuevoLote = new LoteProducto
            {
                ProductoId = loteDto.ProductoId,
                CantidadInicial = loteDto.Cantidad,
                CantidadActual = loteDto.Cantidad,

                // Asignación de los campos calculados
                CostoTotalLote = costoTotalCalculado,
                PrecioCompraUnitario = costoUnitarioCalculado,

                FechaCaducidad = loteDto.FechaCaducidad,
                FechaRegistro = DateTime.UtcNow
            };

            // 3. Agregar el nuevo lote y actualizar el stock del producto.
            _context.LotesProducto.Add(nuevoLote);
            productoAsociado.Stock += loteDto.Cantidad;

            // 4. Guardar todos los cambios.
            await _context.SaveChangesAsync();

            return nuevoLote;
        }

        // Se corrigió el tipo de retorno para ser nullable Task<LoteProducto?>
        public async Task<LoteProducto?> GetLoteByIdAsync(int id)
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