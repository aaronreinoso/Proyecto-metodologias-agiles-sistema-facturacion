using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class DetalleNotaCredito
    {
        [Key]
        public int Id { get; set; }

        public int NotaCreditoId { get; set; }
        [ForeignKey("NotaCreditoId")]
        public virtual NotaCredito? NotaCredito { get; set; }

        public int ProductoId { get; set; }
        [ForeignKey("ProductoId")]
        public virtual Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }
    }
}