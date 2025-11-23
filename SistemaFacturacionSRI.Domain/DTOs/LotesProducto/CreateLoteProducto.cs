using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.LotesProducto
{
    public class CreateLoteProducto
    {
        [Required]
        public int ProductoId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        // --- CAMPOS DE ENTRADA Y CÁLCULO ---

        // Input 1: El costo total del lote (Lo que el usuario ingresa en el modal)
        [Range(0.01, double.MaxValue, ErrorMessage = "El costo total del lote debe ser positivo.")]
        // Lo dejamos nullable para que la lógica de exclusión del servicio lo valide
        public decimal? CostoTotalLote { get; set; }

        // Input 2: El precio unitario, usado para el cálculo en el servicio.
        // Lo dejamos nullable para la lógica de exclusión
        [Range(0.00001, double.MaxValue, ErrorMessage = "El precio unitario de compra debe ser positivo.")]
        public decimal? PrecioCompraUnitario { get; set; }

        // --- TRAZABILIDAD ---

        // Es nullable. La obligatoriedad la determina la bandera EsPerecible en el producto.
        public DateTime? FechaCaducidad { get; set; }
    }
}