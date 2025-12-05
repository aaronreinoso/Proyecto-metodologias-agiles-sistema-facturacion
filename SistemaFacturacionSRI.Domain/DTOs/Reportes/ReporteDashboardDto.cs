namespace SistemaFacturacionSRI.Domain.DTOs.Reportes
{
    public class ReporteVentasDiariasDto
    {
        public DateTime Fecha { get; set; }
        public decimal TotalVendido { get; set; }
        public int CantidadFacturas { get; set; }
    }

    public class ReporteTopProductoDto
    {
        public string Producto { get; set; } = string.Empty;
        public decimal CantidadTotal { get; set; }
        public decimal MontoTotal { get; set; }
    }

    public class ReporteCaducidadDto
    {
        public string Producto { get; set; } = string.Empty;
        public string Lote { get; set; } = string.Empty;
        public DateTime FechaCaducidad { get; set; }
        public int DiasRestantes { get; set; }
        public decimal Stock { get; set; }
    }


    public class ReporteGananciaDiariaDto
    {
        public DateTime Fecha { get; set; }
        public decimal VentaBruta { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal UtilidadBruta => VentaBruta - CostoTotal;
        public decimal MargenPorcentual => VentaBruta > 0 ? (UtilidadBruta / VentaBruta) * 100 : 0;
    }
}