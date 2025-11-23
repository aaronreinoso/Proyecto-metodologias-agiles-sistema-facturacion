using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Productos;
using SistemaFacturacionSRI.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductosController : ControllerBase
    {
        private readonly IProductoService _productoService;

        public ProductosController(IProductoService productoService)
        {
            _productoService = productoService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            var productos = await _productoService.GetAllProductosAsync();
            return Ok(productos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            var producto = await _productoService.GetProductoByIdAsync(id);
            if (producto == null)
                return NotFound();

            return Ok(producto);
        }

        [HttpPost]
        public async Task<ActionResult<Producto>> PostProducto(CreateProducto producto)
        {
            // 1. Validar el modelo (data annotations).
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 2. ELIMINAMOS la línea obsoleta que causaba el error CS1061.
            //    La validación de unicidad de código y campos desglosados se hace en ProductoService.

            try
            {
                var nuevo = await _productoService.AddProductoAsync(producto);
                return CreatedAtAction(nameof(GetProducto), new { id = nuevo.Id }, nuevo);
            }
            catch (InvalidOperationException ex)
            {
                // Conflict 409: Ya existe el código principal o la combinación (Nombre+Marca+Presentación)
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Error 500: Error interno del servidor
                return StatusCode(500, new { message = "Error interno al crear el producto: " + ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProducto(int id, UpdateProducto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID de la ruta no coincide con el ID del producto.");

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var actualizado = await _productoService.UpdateProductoAsync(producto);
                if (!actualizado)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var eliminado = await _productoService.DeleteProductoAsync(id);
            if (!eliminado)
                return NotFound();

            return NoContent();
        }

        


        [HttpPatch("{id}/precio")]
        public async Task<IActionResult> ActualizarPrecio(int id, UpdatePrecioProducto dto)
        {
            if (dto.PrecioUnitario <= 0)
                return BadRequest("El precio debe ser mayor que cero.");

            try
            {
                var actualizado = await _productoService.UpdatePrecioProductoAsync(id, dto.PrecioUnitario);

                if (!actualizado)
                    return NotFound();

                return Ok(new { message = "Precio actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al actualizar precio: " + ex.Message });
            }
        }



    }
}