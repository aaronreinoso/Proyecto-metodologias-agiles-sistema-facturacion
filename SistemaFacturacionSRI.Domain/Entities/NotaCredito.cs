using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class NotaCredito
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FacturaId { get; set; }

        [ForeignKey("FacturaId")]
        public virtual Factura? Factura { get; set; }

        public DateTime FechaEmision { get; set; } = DateTime.Now;
        public string Motivo { get; set; } = string.Empty;

        [MaxLength(49)]
        public string? ClaveAcceso { get; set; }
        public string? XmlGenerado { get; set; }
        public string? EstadoSRI { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public string? MensajeErrorSRI { get; set; }
        public string Estado { get; set; } // "Pendiente", "Autorizada", "Anulada"

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalIVA { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Total { get; set; }

        public virtual List<DetalleNotaCredito> Detalles { get; set; } = new();
    }
}