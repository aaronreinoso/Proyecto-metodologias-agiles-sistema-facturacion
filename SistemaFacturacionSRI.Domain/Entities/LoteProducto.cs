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
        public virtual Producto Producto { get; set; } // Propiedad de navegación

        [Required]
        public int CantidadInicial { get; set; }

        [Required]
        public int CantidadActual { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioCompra { get; set; }

        // CAMBIO 2: Se hace nullable (con '?') para lotes sin caducidad.
        public DateTime? FechaCaducidad { get; set; }

        // CAMBIO 3: Se mantiene UtcNow para la fecha universal (hora del sistema).
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}
