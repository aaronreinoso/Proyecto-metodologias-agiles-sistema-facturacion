using SistemaFacturacionSRI.Domain.Enumss; 

namespace SistemaFacturacionSRI.Domain.DTOs.Clientes
{
    public class UpdateCliente
    {
        public int Id { get; set; }

        public TipoIdentificacion TipoIdentificacion { get; set; }

        public string Identificacion { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string? Pais { get; set; }
    }
}