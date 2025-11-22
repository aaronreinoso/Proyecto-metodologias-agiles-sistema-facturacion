using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class DetalleFactura
    {
        [Key]
        public int Id { get; set; }

        public int FacturaId { get; set; }
        [ForeignKey("FacturaId")]
        public virtual Factura? Factura { get; set; }

        public int ProductoId { get; set; }
        [ForeignKey("ProductoId")]
        public virtual Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioUnitario { get; set; } // Precio al momento de la venta

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }
    }
}