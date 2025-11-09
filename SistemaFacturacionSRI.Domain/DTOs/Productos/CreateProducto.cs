using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Productos
{
    public class CreateProducto
    {
        [Required, MaxLength(25)]
        public string CodigoPrincipal { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? CodigoAuxiliar { get; set; }

        [Required, MaxLength(300)]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        [RegularExpression("Bien|Servicio", ErrorMessage = "Debe ser 'Bien' o 'Servicio'.")]
        public string TipoProducto { get; set; } = "Bien";

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal PrecioUnitario { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? PrecioCompra { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal PorcentajeIVA { get; set; } = 15.00m;

        [MaxLength(10)]
        public string? CodigoICE { get; set; }
        [Range(0, 300)]
        public decimal? PorcentajeICE { get; set; }

        [MaxLength(10)]
        public string? CodigoIRBPNR { get; set; }
        [Range(0, 300)]
        public decimal? PorcentajeIRBPNR { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Stock { get; set; }
    }
}
