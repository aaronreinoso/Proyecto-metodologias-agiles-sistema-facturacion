namespace SistemaFacturacionSRI.Domain.Entities
{
    public class Cliente
    {
        public int Id { get; set; }
        public string Identificacion { get; set; } // Cédula o RUC
        public string NombreCompleto { get; set; }
        public string Direccion { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
    }
}