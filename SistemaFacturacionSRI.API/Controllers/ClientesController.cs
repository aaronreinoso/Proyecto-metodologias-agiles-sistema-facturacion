using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain;
using SistemaFacturacionSRI.Domain.DTOs.Clientes;
using SistemaFacturacionSRI.Domain.Entities;
namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteService _clienteService;

        public ClientesController(IClienteService clienteService)
        {
            _clienteService = clienteService;
        }

        // GET: api/Clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            var clientes = await _clienteService.GetAllClientesAsync();
            return Ok(clientes);
        }

        // GET: api/Clientes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var cliente = await _clienteService.GetClienteByIdAsync(id);

            if (cliente == null)
            {
                return NotFound();
            }

            return Ok(cliente);
        }

        // POST: api/Clientes
        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente(CreateCliente cliente)
        {
            // Validaciones básicas: puedes expandir esto con FluentValidation
            if (string.IsNullOrEmpty(cliente.Identificacion) || string.IsNullOrEmpty(cliente.NombreCompleto))
            {
                return BadRequest("La identificación y el nombre son obligatorios.");
            }

            var newCliente = await _clienteService.AddClienteAsync(cliente);
            // Retorna 201 Created y la ubicación del nuevo recurso
            return CreatedAtAction(nameof(GetCliente), new { id = newCliente.Id }, newCliente);
        }

        // PUT: api/Clientes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, UpdateCliente cliente)
        {
            if (id != cliente.Id)
            {
                return BadRequest("El ID de la ruta no coincide con el ID del cliente.");
            }

            if (string.IsNullOrEmpty(cliente.Identificacion) || string.IsNullOrEmpty(cliente.NombreCompleto))
            {
                return BadRequest("La identificación y el nombre son obligatorios.");
            }

            var updated = await _clienteService.UpdateClienteAsync(cliente);

            if (!updated)
            {
                return NotFound(); // Cliente no encontrado para actualizar
            }

            return NoContent(); // 204 No Content para una actualización exitosa
        }

        // DELETE: api/Clientes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var deleted = await _clienteService.DeleteClienteAsync(id);

            if (!deleted)
            {
                return NotFound(); // Cliente no encontrado para eliminar
            }

            return NoContent(); // 204 No Content para una eliminación exitosa
        }
    }
}
