using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Rol { get; set; } = "Empleado";

        public bool Estado { get; set; } = true;
    }
}