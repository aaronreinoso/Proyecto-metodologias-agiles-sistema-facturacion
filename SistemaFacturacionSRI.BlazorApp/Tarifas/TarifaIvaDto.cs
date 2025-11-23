namespace SistemaFacturacionSRI.BlazorApp.DTOs.Tarifas
{
    public class TarifaIvaDto
    {
        public int Id { get; set; }
        public string CodigoSRI { get; set; } = string.Empty;
        public decimal Porcentaje { get; set; }

        // Propiedad de solo lectura para mostrar en el Select (ej: "15% (Código 4)")
        public string DescripcionVisual => $"{Porcentaje:N0}% (Código {CodigoSRI})";
    }
}