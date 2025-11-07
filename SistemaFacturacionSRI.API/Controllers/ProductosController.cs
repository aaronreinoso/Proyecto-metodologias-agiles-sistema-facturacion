using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Productos;
using SistemaFacturacionSRI.Domain.Entities;

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
            if (string.IsNullOrEmpty(producto.CodigoPrincipal) || string.IsNullOrEmpty(producto.Descripcion))
                return BadRequest("Código y descripción son obligatorios.");

            try
            {
                var nuevo = await _productoService.AddProductoAsync(producto);
                return CreatedAtAction(nameof(GetProducto), new { id = nuevo.Id }, nuevo);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProducto(int id, UpdateProducto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID no coincide.");

            var actualizado = await _productoService.UpdateProductoAsync(producto);
            if (!actualizado)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var eliminado = await _productoService.DeleteProductoAsync(id);
            if (!eliminado)
                return NotFound();

            return NoContent();
        }
    }
}
