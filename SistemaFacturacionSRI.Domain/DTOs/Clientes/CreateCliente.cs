using SistemaFacturacionSRI.Domain.Enumss;
using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Clientes
{
    public class CreateCliente
    {
        public TipoIdentificacion TipoIdentificacion { get; set; } = TipoIdentificacion.Cedula;

        [Required(ErrorMessage = "⚠ Escribe la identificación.")]
        [MinLength(3, ErrorMessage = "Muy corto.")]
        public string Identificacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "⚠ El nombre es obligatorio.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "⚠ El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "⚠ Formato de correo inválido.")]
        public string Email { get; set; } = string.Empty;

        // Validación numérica para teléfono
        [RegularExpression(@"^\d{10}$", ErrorMessage = "⚠ El teléfono debe tener 10 números.")]
        public string Telefono { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;
        public string? Pais { get; set; }
    }
}