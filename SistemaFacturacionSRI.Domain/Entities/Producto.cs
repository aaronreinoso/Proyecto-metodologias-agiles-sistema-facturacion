namespace SistemaFacturacionSRI.Domain.Entities
{
    public class Producto
    {
        public int Id { get; set; }
        public string CodigoPrincipal { get; set; }
        public string Descripcion { get; set; }
        public decimal PrecioUnitario { get; set; }
        // Se puede agregar más propiedades como 'Iva' (Impuesto)
    }
}