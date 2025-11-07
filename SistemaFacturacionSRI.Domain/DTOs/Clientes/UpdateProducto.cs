using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Productos
{
    public class UpdateProducto : CreateProducto
    {
        [Range(1, int.MaxValue)]
        public int Id { get; set; }

        public bool Estado { get; set; } = true;
    }
}
