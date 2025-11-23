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

        // --- CAMPOS DE COSTO EXCLUYENTES (Solo uno debe ser proporcionado por el usuario) ---

        // Input A: Precio de Compra del Lote (Total)
        [Range(0.01, double.MaxValue, ErrorMessage = "El costo total del lote debe ser positivo.")]
        public decimal? CostoTotalLote { get; set; }

        // Input B: Precio de Compra Unitario (Para casos donde el total es desconocido o complejo)
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio unitario de compra debe ser positivo.")]
        public decimal? PrecioUnitarioInput { get; set; }

        // --- TRAZABILIDAD ---

        // Es nullable, pero el servicio LoteProductoService.cs lo requerirá si el Producto es perecible.
        public DateTime? FechaCaducidad { get; set; }

        // NOTA: El campo PrecioCompra fue eliminado para dar paso a CostoTotalLote y PrecioUnitarioInput
    }
}