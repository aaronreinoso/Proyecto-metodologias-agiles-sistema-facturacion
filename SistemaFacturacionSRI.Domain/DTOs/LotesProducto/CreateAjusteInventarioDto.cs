using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.LotesProducto
{
    // DTO para el ajuste manual de la cantidad de un lote específico
    public class CreateAjusteInventarioDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Debe especificar el ID del lote a ajustar.")]
        public int LoteProductoId { get; set; }

        [Required(ErrorMessage = "La nueva cantidad es obligatoria.")]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
        public int CantidadNueva { get; set; } // CA-001 (El nuevo valor de existencias)

        [Required(ErrorMessage = "La justificación es obligatoria.")]
        [MinLength(10, ErrorMessage = "La justificación debe tener al menos 10 caracteres.")]
        [MaxLength(500)]
        public string Justificacion { get; set; } = string.Empty; // CA-001

        // Se llenará en el API Controller con el ID del usuario autenticado
        public int? UsuarioId { get; set; }
    }
}