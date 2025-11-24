using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaFacturacionSRI.Domain.Entities
{
    public class AjusteInventario
    {
        [Key]
        public int Id { get; set; }

        public int LoteProductoId { get; set; }
        [ForeignKey("LoteProductoId")]
        public virtual LoteProducto? LoteProducto { get; set; }

        [Required]
        public int CantidadAnterior { get; set; }

        [Required]
        public int CantidadNueva { get; set; }

        [Required]
        public int Diferencia { get; set; }

        [Required]
        [MaxLength(500)]
        public string Justificacion { get; set; } = string.Empty;

        public DateTime FechaAjuste { get; set; } = DateTime.Now;

        public int? UsuarioId { get; set; }
    }
}
