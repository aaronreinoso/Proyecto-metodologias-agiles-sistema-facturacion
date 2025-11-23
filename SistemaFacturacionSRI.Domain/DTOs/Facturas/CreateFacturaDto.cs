using System.ComponentModel.DataAnnotations;

namespace SistemaFacturacionSRI.Domain.DTOs.Facturas
{
    public class CreateFacturaDto
    {
        [Required]
        public int ClienteId { get; set; }

        [Required]
        public List<CreateDetalleFacturaDto> Detalles { get; set; } = new();
    }

    public class CreateDetalleFacturaDto
    {
        [Required]
        public int ProductoId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public int Cantidad { get; set; }
    }
}