namespace SistemaFacturacionSRI.Domain.DTOs.NotasCredito
{
    public class CreateNotaCreditoDto
    {
        public int FacturaId { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public List<ItemNotaCreditoDto> Detalles { get; set; } = new();
    }

    public class ItemNotaCreditoDto
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}