using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Productos;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class ProductoService : IProductoService
    {
        private readonly AppDbContext _context;

        public ProductoService(AppDbContext context)
        {
            _context = context;
        }

        private string GenerarDescripcionCompleta(CreateProducto producto)
        {
            // La descripción oficial para el SRI es la concatenación de los campos.
            return $"{producto.NombreGenerico.Trim()} {producto.Marca.Trim()} {producto.Presentacion.Trim()}";
        }

        // --- CREATE ---
        public async Task<Producto> AddProductoAsync(CreateProducto producto)
        {
            string descripcionCompleta = GenerarDescripcionCompleta(producto);

            // 1. Validar unicidad de Código Principal (si se ingresó)
            if (!string.IsNullOrWhiteSpace(producto.CodigoPrincipal) &&
                await _context.Productos.AnyAsync(p => p.CodigoPrincipal == producto.CodigoPrincipal))
            {
                throw new InvalidOperationException("Ya existe un producto con ese código principal.");
            }

            // 2. Validar unicidad de la combinación Nombre/Marca/Presentación
            if (await _context.Productos.AnyAsync(p =>
                p.NombreGenerico == producto.NombreGenerico &&
                p.Marca == producto.Marca &&
                p.Presentacion == producto.Presentacion))
            {
                throw new InvalidOperationException("Ya existe un producto con esa combinación de nombre, marca y presentación.");
            }

            // 3. Autogenerar Código Principal si es necesario
            string codigoPrincipal = string.IsNullOrWhiteSpace(producto.CodigoPrincipal)
                ? $"PROD-{DateTime.Now.Ticks}"
                : producto.CodigoPrincipal;

            var nuevo = new Producto
            {
                CodigoPrincipal = codigoPrincipal,
                CodigoAuxiliar = producto.CodigoAuxiliar,

                // Mapeo de campos desglosados
                NombreGenerico = producto.NombreGenerico,
                Marca = producto.Marca,
                Presentacion = producto.Presentacion,
                Descripcion = descripcionCompleta,

                // Mapeo de las banderas simples (TarifaIvaId y EsPerecible)
                TarifaIvaId = producto.TarifaIvaId, // CORREGIDO: Directo del DTO
                EsPerecible = producto.EsPerecible, // CORREGIDO: Directo del DTO

                
                PrecioUnitario = producto.PrecioUnitario,

                // Mapeo de códigos SRI opcionales
                CodigoICE = producto.CodigoICE,
                PorcentajeICE = producto.PorcentajeICE,
                CodigoIRBPNR = producto.CodigoIRBPNR,
                PorcentajeIRBPNR = producto.PorcentajeIRBPNR,

                Estado = true,
                FechaRegistro = DateTime.Now
            };

            _context.Productos.Add(nuevo);
            await _context.SaveChangesAsync();
            return nuevo;
        }

        // --- READ ALL ---
        public async Task<List<Producto>> GetAllProductosAsync()
        {
            // Incluir la relación TarifaIva para que la UI pueda obtener el porcentaje
            return await _context.Productos
                .Include(p => p.TarifaIva)
                .OrderBy(p => p.CodigoPrincipal)
                .ToListAsync();
        }

        // --- READ BY ID ---
        public async Task<Producto?> GetProductoByIdAsync(int id)
        {
            // Incluir la relación TarifaIva para la edición
            return await _context.Productos
                .Include(p => p.TarifaIva)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // --- UPDATE ---
        public async Task<bool> UpdateProductoAsync(UpdateProducto producto)
        {
            var existente = await _context.Productos.FindAsync(producto.Id);
            if (existente == null) return false;

            // 1. Validar unicidad (excluyendo el producto que se está editando)
            if (await _context.Productos.AnyAsync(p =>
                p.NombreGenerico == producto.NombreGenerico &&
                p.Marca == producto.Marca &&
                p.Presentacion == producto.Presentacion &&
                p.Id != producto.Id))
            {
                throw new InvalidOperationException("Ya existe un producto con esa combinación de nombre, marca y presentación.");
            }

            // 2. Actualizar campos
            existente.CodigoPrincipal = producto.CodigoPrincipal;
            existente.CodigoAuxiliar = producto.CodigoAuxiliar;

            // Actualizar campos desglosados y descripción oficial
            existente.NombreGenerico = producto.NombreGenerico;
            existente.Marca = producto.Marca;
            existente.Presentacion = producto.Presentacion;
            existente.Descripcion = $"{producto.NombreGenerico.Trim()} {producto.Marca.Trim()} {producto.Presentacion.Trim()}";

            // Actualizar banderas simples
            existente.TarifaIvaId = producto.TarifaIvaId; // CORREGIDO
            existente.EsPerecible = producto.EsPerecible; // CORREGIDO
           

            // Validar PVP (Regla de negocio: si hay stock, el precio debe ser positivo)
            if (existente.Stock > 0 && producto.PrecioUnitario <= 0)
            {
                throw new InvalidOperationException("El producto ya tiene stock. El precio de venta debe ser positivo.");
            }
            existente.PrecioUnitario = producto.PrecioUnitario;

            // Campos fiscales
            existente.CodigoICE = producto.CodigoICE;
            existente.PorcentajeICE = producto.PorcentajeICE;
            existente.CodigoIRBPNR = producto.CodigoIRBPNR;
            existente.PorcentajeIRBPNR = producto.PorcentajeIRBPNR;
            existente.Estado = producto.Estado;

            await _context.SaveChangesAsync();
            return true;
        }

        // --- DELETE ---
        public async Task<bool> DeleteProductoAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return false;

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePrecioProductoAsync(int id, decimal nuevoPrecio)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return false;

            producto.PrecioUnitario = nuevoPrecio;
            await _context.SaveChangesAsync();

            return true;
        }


    }
}