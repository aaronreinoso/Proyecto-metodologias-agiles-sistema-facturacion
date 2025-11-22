using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult<Usuario>> Login([FromBody] Usuario loginRequest)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == loginRequest.NombreUsuario
                                       && u.Password == loginRequest.Password);

            if (usuario == null)
            {
                return Unauthorized("Usuario o contraseña incorrectos.");
            }

            if (usuario.Estado == false)
            {
                return Unauthorized("Su cuenta ha sido desactivada. Contacte al administrador.");
            }
            usuario.Password = "";
            return Ok(usuario);
        }
    }
}