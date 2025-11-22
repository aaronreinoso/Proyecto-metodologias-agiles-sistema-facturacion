using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Usuarios;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuariosController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Usuario>>> Get()
        {
            return Ok(await _usuarioService.GetAllUsuariosAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> Get(int id)
        {
            var user = await _usuarioService.GetUsuarioByIdAsync(id);
            return user != null ? Ok(user) : NotFound();
        }

        [HttpPost]
        public async Task<ActionResult> Post(CreateUsuario dto)
        {
            try
            {
                var nuevo = await _usuarioService.CreateUsuarioAsync(dto);
                return Ok(nuevo);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, UpdateUsuario dto)
        {
            if (id != dto.Id) return BadRequest();
            var result = await _usuarioService.UpdateUsuarioAsync(dto);
            return result ? NoContent() : NotFound();
        }

        // Endpoint específico para desactivar/activar
        [HttpPatch("{id}/toggle-estado")]
        public async Task<IActionResult> ToggleEstado(int id)
        {
            var result = await _usuarioService.ToggleEstadoAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}