using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ================================================================
        // 🚀 LOGIN CON SOPORTE A PASSWORD HASH O TEXTO PLANO (AUTO-UPGRADE)
        // ================================================================
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] Usuario loginRequest)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == loginRequest.NombreUsuario);

            if (usuario == null)
                return Unauthorized("Usuario o contraseña incorrectos.");

            bool passwordValido = false;
            bool necesitaActualizacion = false;

            // ------------------------------------------------------------
            // 1️⃣ Intento A: Verificar si la contraseña está en BCrypt
            // ------------------------------------------------------------
            try
            {
                if (BCrypt.Net.BCrypt.Verify(loginRequest.Password, usuario.Password))
                {
                    passwordValido = true;
                }
            }
            catch
            {
                // Si falla el formato → no es hash → pasamos a texto plano
            }

            // ------------------------------------------------------------
            // 2️⃣ Intento B: Verificar si era TEXTO PLANO (usuarios antiguos)
            // ------------------------------------------------------------
            if (!passwordValido)
            {
                if (usuario.Password == loginRequest.Password)
                {
                    passwordValido = true;
                    necesitaActualizacion = true; // Detectamos usuario antiguo
                }
            }

            // ------------------------------------------------------------
            // 3️⃣ Si sigue siendo falso → FAIL
            // ------------------------------------------------------------
            if (!passwordValido)
                return Unauthorized("Usuario o contraseña incorrectos.");

            if (!usuario.Estado)
                return Unauthorized("Su cuenta ha sido desactivada.");

            // ------------------------------------------------------------
            // 4️⃣ AUTO-MIGRACIÓN: Convertir texto plano → BCrypt
            // ------------------------------------------------------------
            if (necesitaActualizacion)
            {
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(loginRequest.Password);
                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();
            }

            // ------------------------------------------------------------
            // 5️⃣ Generar Token
            // ------------------------------------------------------------
            var tokenString = GenerarTokenJWT(usuario);

            return Ok(new
            {
                token = tokenString,
                usuario = usuario.NombreUsuario,
                rol = usuario.Rol
            });
        }

        // ================================================================
        // 🔐 GENERAR TOKEN JWT
        // ================================================================
        private string GenerarTokenJWT(Usuario usuario)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var keyBytes = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var claims = new ClaimsIdentity();
            claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()));
            claims.AddClaim(new Claim(ClaimTypes.Name, usuario.NombreUsuario));

            if (!string.IsNullOrEmpty(usuario.Rol))
                claims.AddClaim(new Claim(ClaimTypes.Role, usuario.Rol));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = claims,
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenConfig = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(tokenConfig);
        }
    }
}
