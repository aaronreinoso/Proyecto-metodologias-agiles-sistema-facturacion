using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class LoteProducto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; } // Clave foránea

        [ForeignKey("ProductoId")]
        public virtual Producto? Producto { get; set; } // Propiedad de navegación (Nullable para evitar warnings)

        [Required]
        public int CantidadInicial { get; set; }

        [Required]
        public int CantidadActual { get; set; }

        // --- CAMPOS DE COSTO (PARA KARDEX Y VALORACIÓN) ---

        // 1. Costo Total del Lote (Input directo del proveedor en la factura)
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal CostoTotalLote { get; set; }

        // 2. Precio Unitario de Compra (Calculado: CostoTotalLote / CantidadInicial).
        // Se usa alta precisión (18, 5) para un cálculo preciso del costo.
        [Required]
        [Column(TypeName = "decimal(18, 5)")]
        public decimal PrecioCompraUnitario { get; set; }

        // --- CAMPOS DE TRAZABILIDAD ---

        public DateTime? FechaCaducidad { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow; // Fecha de ingreso del lote
    }
}