using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class Factura
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; }

        public virtual List<NotaCredito> NotasCredito { get; set; } = new();


        public DateTime FechaEmision { get; set; } = DateTime.Now;

        // Estados: "Pendiente", "Autorizada", "Rechazada"
        [MaxLength(20)]
        public string Estado { get; set; } = "Pendiente";

        // Para SRI (Sprint 2/3)
        [MaxLength(49)]
        public string? ClaveAcceso { get; set; }

        public string? XmlGenerado { get; set; }

        // Totales
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalIVA { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Total { get; set; }

        public virtual List<DetalleFactura> Detalles { get; set; } = new();

        public string? EstadoSRI { get; set; } // "RECIBIDA", "AUTORIZADA", "DEVUELTA"
        public DateTime? FechaAutorizacion { get; set; }
        public string? MensajeErrorSRI { get; set; } // Para guardar por qué falló
    }
}