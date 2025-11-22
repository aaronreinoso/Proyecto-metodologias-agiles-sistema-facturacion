namespace SistemaFacturacionSRI.Domain.DTOs.Usuarios
{
    public class UpdateUsuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;

        // Opcional: Si viene vacío, no cambiamos la contraseña actual
        public string? Password { get; set; }

        public string Rol { get; set; } = "Empleado";
        public bool Estado { get; set; }
    }
}