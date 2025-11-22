using SistemaFacturacionSRI.Domain.DTOs.Usuarios;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IUsuarioService
    {
        Task<List<Usuario>> GetAllUsuariosAsync();
        Task<Usuario?> GetUsuarioByIdAsync(int id);
        Task<Usuario> CreateUsuarioAsync(CreateUsuario usuarioDto);
        Task<bool> UpdateUsuarioAsync(UpdateUsuario usuarioDto);
        Task<bool> ToggleEstadoAsync(int id); // Para activar/desactivar
    }
}