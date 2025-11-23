namespace SistemaFacturacionSRI.BlazorApp.Enums
{
    public enum ProductoCategoria
    {
        None = 0,
        CanastaBasica = 1,       // IVA 0%, Perecible Condicional
        Construccion = 2,        // IVA 5% o 15%, Perecible NO
        OtrosConsumoGeneral = 3  // IVA 0% o 15%, Perecible Condicional
    }
}