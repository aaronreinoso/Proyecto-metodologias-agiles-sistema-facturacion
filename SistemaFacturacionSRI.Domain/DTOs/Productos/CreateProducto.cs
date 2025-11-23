using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Productos
{
    public class CreateProducto
    {
        // El Código Principal es opcional, ya que puede ser autogenerado.
        [MaxLength(25)]
        public string? CodigoPrincipal { get; set; }

        // Campos desglosados para la regla de unicidad (Nombre + Marca + Presentación)
        [Required(ErrorMessage = "El nombre genérico es obligatorio.")]
        [MaxLength(100)]
        public string NombreGenerico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Marca { get; set; }

        [Required(ErrorMessage = "La presentación es obligatoria.")]
        [MaxLength(100)]
        public string Presentacion { get; set; }

        // FK a la tabla de tarifas IVA (Simple)
        [Required(ErrorMessage = "Debe seleccionar la Tarifa de IVA.")]
        public int TarifaIvaId { get; set; } // <--- ESTO ES LO QUE LE FALTABA A TU CÓDIGO

        // Bandera Perecible (FEFO/FIFO)
        public bool EsPerecible { get; set; } = false;

        [Required(ErrorMessage = "Debe especificar el tipo.")]
        [RegularExpression("Bien|Servicio", ErrorMessage = "Debe ser 'Bien' o 'Servicio'.")]
        public string TipoProducto { get; set; } = "Bien";

        // Precio de Venta al Público (PVP)
        [Range(0.00, double.MaxValue, ErrorMessage = "El precio de venta debe ser positivo o cero.")]
        public decimal PrecioUnitario { get; set; } = 0.00m;


        // --- CAMPOS SRI ADICIONALES ---
        [MaxLength(25)]
        public string? CodigoAuxiliar { get; set; }
        [MaxLength(10)]
        public string? CodigoICE { get; set; }
        [Range(0, 300)]
        public decimal? PorcentajeICE { get; set; }
        [MaxLength(10)]
        public string? CodigoIRBPNR { get; set; }
        [Range(0, 300)]
        public decimal? PorcentajeIRBPNR { get; set; }
    }
}