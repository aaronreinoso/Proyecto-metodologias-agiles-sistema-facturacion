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
            // CA-002: Validaciones básicas
            if (loteDto.Cantidad <= 0 || loteDto.CostoTotalLote <= 0)
                throw new InvalidOperationException("La cantidad y el costo total deben ser mayores a cero.");

            var productoAsociado = await _context.Productos.FindAsync(loteDto.ProductoId);

            if (productoAsociado == null)
                throw new InvalidOperationException($"El producto con ID {loteDto.ProductoId} no existe.");

            // --- CALCULAR COSTOS ---
            decimal costoUnitarioCalculado;
            decimal costoTotalCalculado;

            bool isTotalInput = loteDto.CostoTotalLote.HasValue;
            bool isUnitarioInput = loteDto.PrecioCompraUnitario.HasValue;

            if (isTotalInput)
            {
                costoTotalCalculado = loteDto.CostoTotalLote!.Value;
                costoUnitarioCalculado = costoTotalCalculado / loteDto.Cantidad;
            }
            else if (isUnitarioInput)
            {
                costoUnitarioCalculado = loteDto.PrecioCompraUnitario!.Value;
                costoTotalCalculado = costoUnitarioCalculado * loteDto.Cantidad;
            }
            else
            {
                throw new InvalidOperationException("Debe ingresar costo total o unitario.");
            }

            // CA-003: Validar caducidad si es perecible
            if (productoAsociado.EsPerecible && !loteDto.FechaCaducidad.HasValue)
            {
                throw new InvalidOperationException("Este producto es perecible y requiere una fecha de caducidad.");
            }

           

            // CA-005: Estado "Activo"
            var nuevoLote = new LoteProducto
            {
                ProductoId = loteDto.ProductoId,
                CantidadInicial = loteDto.Cantidad,
                CantidadActual = loteDto.Cantidad,
                CostoTotalLote = costoTotalCalculado,
                PrecioCompraUnitario = costoUnitarioCalculado,
                FechaCaducidad = loteDto.FechaCaducidad,
                FechaRegistro = DateTime.UtcNow,
                //Estado = "Activo"
            };

            _context.LotesProducto.Add(nuevoLote);

            // Actualizar stock del producto
            productoAsociado.Stock += loteDto.Cantidad;

            await _context.SaveChangesAsync();

            return nuevoLote;
        }

        public async Task<LoteProducto?> GetLoteByIdAsync(int id)
        {
            return await _context.LotesProducto.FindAsync(id);
        }

        public async Task<IEnumerable<LoteProducto>> GetLotesByProductoIdAsync(int productoId)
        {
            var producto = await _context.Productos.FindAsync(productoId);

            if (producto == null)
                return new List<LoteProducto>();

            // CA-004: Orden FIFO/FEFO
            return await _context.LotesProducto
                .Where(l => l.ProductoId == productoId)
                .OrderBy(l =>
                    producto.EsPerecible ?
                    (l.FechaCaducidad ?? DateTime.MaxValue)
                    :
                    l.FechaRegistro)
                .ToListAsync();
        }

        public async Task<bool> RealizarAjusteManualAsync(CreateAjusteInventarioDto ajusteDto)
        {
            // Usamos una transacción para garantizar que o se guarda todo (ajuste y stock) o nada.
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lote = await _context.LotesProducto.FindAsync(ajusteDto.LoteProductoId);
                if (lote == null)
                    throw new InvalidOperationException("Lote no encontrado.");

                // 1. Validar que la nueva cantidad no cause stock negativo en el lote.
                if (ajusteDto.CantidadNueva < 0)
                    throw new InvalidOperationException("La nueva cantidad para el lote no puede ser negativa.");

                // 2. Calcular la diferencia y el nuevo stock total del producto.
                int diferencia = ajusteDto.CantidadNueva - lote.CantidadActual;

                var producto = await _context.Productos.FindAsync(lote.ProductoId);
                if (producto == null)
                    throw new InvalidOperationException("Producto asociado no encontrado.");

                // 3. Actualizar el stock total del Producto (CA-003, CA-004)
                producto.Stock += diferencia;

                // 4. Actualizar la cantidad del Lote
                int cantidadAnterior = lote.CantidadActual;
                lote.CantidadActual = ajusteDto.CantidadNueva;

                _context.Entry(lote).State = EntityState.Modified;
                _context.Entry(producto).State = EntityState.Modified;

                // 5. Crear el registro de Auditoría (CA-002)
                var ajuste = new AjusteInventario
                {
                    LoteProductoId = ajusteDto.LoteProductoId,
                    CantidadAnterior = cantidadAnterior,
                    CantidadNueva = ajusteDto.CantidadNueva,
                    Diferencia = diferencia,
                    Justificacion = ajusteDto.Justificacion, // CA-001
                    FechaAjuste = DateTime.Now,
                    UsuarioId = ajusteDto.UsuarioId
                };

                _context.AjustesInventario.Add(ajuste);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<AjusteInventario>> GetAjustesByLoteIdAsync(int loteId)
        {
            return await _context.AjustesInventario
                .Where(a => a.LoteProductoId == loteId)
                .OrderByDescending(a => a.FechaAjuste)
                .ToListAsync();
        }



    }
}
