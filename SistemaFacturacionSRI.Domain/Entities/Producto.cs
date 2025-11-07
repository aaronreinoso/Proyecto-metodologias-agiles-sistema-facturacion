using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.Entities
{
    /// <summary>
    /// Entidad que representa los productos o servicios registrados en el sistema.
    /// Cumple con los campos exigidos por el SRI para la generación de comprobantes electrónicos.
    /// </summary>
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        // --- CAMPOS REQUERIDOS POR EL SRI ---
        [Required(ErrorMessage = "El código principal es obligatorio.")]
        [MaxLength(25)]
        public string CodigoPrincipal { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? CodigoAuxiliar { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [MaxLength(300)]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El precio unitario es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio unitario debe ser mayor que 0.")]
        public decimal PrecioUnitario { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Porcentaje de IVA inválido.")]
        public decimal PorcentajeIVA { get; set; } = 15.00m;

        // --- CAMPOS ADICIONALES ---
        [Required]
        [MaxLength(20)]
        public string TipoProducto { get; set; } = "Bien";

        [Range(0, double.MaxValue)]
        public decimal? PrecioCompra { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Stock { get; set; }

        [MaxLength(10)]
        public string? CodigoICE { get; set; }

        [Range(0, 300)]
        public decimal? PorcentajeICE { get; set; }

        [MaxLength(10)]
        public string? CodigoIRBPNR { get; set; }

        [Range(0, 300)]
        public decimal? PorcentajeIRBPNR { get; set; }

        // --- AUDITORÍA ---
        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
