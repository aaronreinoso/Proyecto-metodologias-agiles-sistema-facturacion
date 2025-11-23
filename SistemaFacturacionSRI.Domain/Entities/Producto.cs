using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    /// <summary>
    /// Entidad que representa los productos o servicios registrados en el sistema.
    /// La lógica de impuestos y perecibilidad es heredada de CategoriaProducto.
    /// </summary>
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        // --- CAMPOS DE IDENTIFICACIÓN (Para la Regla de Unicidad) ---
        [Required(ErrorMessage = "El código principal es obligatorio.")]
        [MaxLength(25)]
        public string CodigoPrincipal { get; set; } = string.Empty;

        // Nuevos campos para la Regla de Unicidad (Nombre, Marca, Presentación)
        [Required(ErrorMessage = "El nombre genérico es obligatorio.")]
        [MaxLength(100)]
        public string NombreGenerico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [MaxLength(100)]
        public string Marca { get; set; } = string.Empty;

        [Required(ErrorMessage = "La presentación es obligatoria.")]
        [MaxLength(100)]
        public string Presentacion { get; set; } = string.Empty;

        // Descripción que se envía al SRI (se autogenera en la lógica: Nombre + Marca + Presentacion)
        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [MaxLength(300)]
        public string Descripcion { get; set; } = string.Empty;


        // --- CAMPO DE CLASIFICACIÓN (COHERENCIA LÓGICA) ---

      
       


        // Precio de Venta al Público (PVP)
        [Required(ErrorMessage = "El precio unitario es obligatorio.")]
        [Range(0.00, double.MaxValue, ErrorMessage = "El precio unitario debe ser mayor o igual que 0.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }
        public bool EsPerecible { get; set; }
        public int TarifaIvaId { get; set; }
        public TarifaIva TarifaIva { get; set; }


        // --- STOCK y OTROS CÓDIGOS SRI ---

        // Stock actual (sumatoria de los lotes)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Stock { get; set; } = 0.00m;

        [MaxLength(25)]
        public string? CodigoAuxiliar { get; set; }
        [MaxLength(10)]
        public string? CodigoICE { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PorcentajeICE { get; set; }
        [MaxLength(10)]
        public string? CodigoIRBPNR { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PorcentajeIRBPNR { get; set; }

        // --- AUDITORÍA ---
        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}