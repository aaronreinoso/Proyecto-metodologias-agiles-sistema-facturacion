using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Domain.DTOs.LotesProducto
{
    public class CreateLoteProducto
    {
        [Required]
        public int ProductoId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser positivo.")]
        public decimal PrecioCompra { get; set; }

        // Es nullable, igual que en la entidad
        public DateTime? FechaCaducidad { get; set; }
    }
}
