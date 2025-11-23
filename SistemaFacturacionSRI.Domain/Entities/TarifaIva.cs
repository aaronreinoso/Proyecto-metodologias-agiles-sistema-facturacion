using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    /// <summary>
    /// Maestro de tarifas de IVA basado en los códigos y porcentajes del SRI (Tabla 16/17).
    /// </summary>
    public class TarifaIva
    {
        [Key]
        public int Id { get; set; }

        // El código que se usa en el XML de SRI (ej: "0", "2", "4", "5", "8")
        [Required]
        [MaxLength(5)]
        public string CodigoSRI { get; set; } = string.Empty;

        // El valor porcentual para cálculos (ej: 0.00, 15.00, 5.00)
        [Required]
        [Column(TypeName = "decimal(5, 2)")]
        public decimal Porcentaje { get; set; }
    }
}