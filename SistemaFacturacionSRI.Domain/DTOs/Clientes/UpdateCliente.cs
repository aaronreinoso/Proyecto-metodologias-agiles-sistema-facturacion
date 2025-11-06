namespace SistemaFacturacionSRI.Domain.DTOs.Clientes
{
    public class UpdateCliente
    {
        public int Id { get; set; } // Necesario para identificar el cliente a actualizar
        public string Identificacion { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
