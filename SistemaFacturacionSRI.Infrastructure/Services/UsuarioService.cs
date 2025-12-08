using BCrypt.Net; // <<< NECESARIO PARA HASHING
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Usuarios;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly AppDbContext _context;

        public UsuarioService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Usuario>> GetAllUsuariosAsync()
        {
            return await _context.Usuarios.ToListAsync();
        }

        public async Task<Usuario?> GetUsuarioByIdAsync(int id)
        {
            return await _context.Usuarios.FindAsync(id);
        }

        // ============================================================
        //  CREAR USUARIO (CON CONTRASEÑA ENCRIPTADA)
        // ============================================================
        public async Task<Usuario> CreateUsuarioAsync(CreateUsuario dto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario))
                throw new InvalidOperationException("El nombre de usuario ya existe.");

            var nuevo = new Usuario
            {
                NombreUsuario = dto.NombreUsuario,

                // CONTRASEÑA ENCRIPTADA
                Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),

                Rol = dto.Rol,
                Estado = true
            };

            _context.Usuarios.Add(nuevo);
            await _context.SaveChangesAsync();
            return nuevo;
        }

        // ============================================================
        //  ACTUALIZAR USUARIO (HASH SI CAMBIA CONTRASEÑA)
        // ============================================================
        public async Task<bool> UpdateUsuarioAsync(UpdateUsuario dto)
        {
            var user = await _context.Usuarios.FindAsync(dto.Id);
            if (user == null) return false;

            user.NombreUsuario = dto.NombreUsuario;
            user.Rol = dto.Rol;
            user.Estado = dto.Estado;

            // SOLO si se envía una nueva contraseña
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // ============================================================
        //  CAMBIAR ESTADO (ACTIVO / INACTIVO)
        // ============================================================
        public async Task<bool> ToggleEstadoAsync(int id)
        {
            var user = await _context.Usuarios.FindAsync(id);
            if (user == null) return false;

            user.Estado = !user.Estado;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
