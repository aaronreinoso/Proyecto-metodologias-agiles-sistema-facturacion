using System.Collections.Generic;
using System.Xml.Serialization;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Models
{
    /// <summary>
    /// Modelo raíz que representa la estructura XML exacta exigida por el SRI.
    /// </summary>
    [XmlRoot("factura")]
    public class FacturaSRI
    {
        [XmlAttribute("id")]
        public string Id { get; set; } = "comprobante";

        [XmlAttribute("version")]
        public string Version { get; set; } = "1.1.0"; // ACTUALIZADO a 1.1.0

        [XmlElement("infoTributaria")]
        public InfoTributaria InfoTributaria { get; set; } = new();

        [XmlElement("infoFactura")]
        public InfoFactura InfoFactura { get; set; } = new();

        [XmlArray("detalles")]
        [XmlArrayItem("detalle")]
        public List<DetalleSRI> Detalles { get; set; } = new();

        // Opcional: Info Adicional (Dirección, Email, etc.)
        [XmlArray("infoAdicional")]
        [XmlArrayItem("campoAdicional")]
        public List<CampoAdicional> InfoAdicional { get; set; } = new();

        
        public bool ShouldSerializeInfoAdicional()
        {
            return InfoAdicional != null && InfoAdicional.Count > 0;
        }
    }

    public class InfoTributaria
    {
        [XmlElement("ambiente")]
        public string Ambiente { get; set; } // 1: Pruebas, 2: Producción

        [XmlElement("tipoEmision")]
        public string TipoEmision { get; set; } = "1"; // 1: Normal

        [XmlElement("razonSocial")]
        public string RazonSocial { get; set; }

        [XmlElement("nombreComercial")]
        public string? NombreComercial { get; set; } // Opcional

        [XmlElement("ruc")]
        public string Ruc { get; set; }

        [XmlElement("claveAcceso")]
        public string ClaveAcceso { get; set; }

        [XmlElement("codDoc")]
        public string CodDoc { get; set; } = "01"; // 01 = Factura

        [XmlElement("estab")]
        public string Estab { get; set; } // Ej: 001

        [XmlElement("ptoEmi")]
        public string PtoEmi { get; set; } // Ej: 002

        [XmlElement("secuencial")]
        public string Secuencial { get; set; } // 9 dígitos

        [XmlElement("dirMatriz")]
        public string DirMatriz { get; set; }
    }

    public class InfoFactura
    {
        [XmlElement("fechaEmision")]
        public string FechaEmision { get; set; } // dd/MM/yyyy

        [XmlElement("dirEstablecimiento")]
        public string DirEstablecimiento { get; set; }

        // Si eres contribuyente especial, pon el nro de resolución, sino borra o deja null
        [XmlElement("contribuyenteEspecial")]
        public string? ContribuyenteEspecial { get; set; }

        [XmlElement("obligadoContabilidad")]
        public string ObligadoContabilidad { get; set; } = "NO";

        [XmlElement("tipoIdentificacionComprador")]
        public string TipoIdentificacionComprador { get; set; } // 04: RUC, 05: Cedula, 06: Pasaporte, 07: Consumidor Final

        [XmlElement("razonSocialComprador")]
        public string RazonSocialComprador { get; set; }

        [XmlElement("identificacionComprador")]
        public string IdentificacionComprador { get; set; }

        [XmlElement("totalSinImpuestos")]
        public decimal TotalSinImpuestos { get; set; }

        [XmlElement("totalDescuento")]
        public decimal TotalDescuento { get; set; }

        [XmlArray("totalConImpuestos")]
        [XmlArrayItem("totalImpuesto")]
        public List<TotalImpuesto> TotalConImpuestos { get; set; } = new();

        [XmlElement("propina")]
        public decimal Propina { get; set; } = 0;

        [XmlElement("importeTotal")]
        public decimal ImporteTotal { get; set; } // Total final a pagar

        [XmlElement("moneda")]
        public string Moneda { get; set; } = "DOLAR";

        // Formas de pago (Obligatorio desde 2016)
        [XmlArray("pagos")]
        [XmlArrayItem("pago")]
        public List<PagoSRI> Pagos { get; set; } = new();
    }

    // Resumen de impuestos (Cabecera)
    public class TotalImpuesto
    {
        [XmlElement("codigo")]
        public string Codigo { get; set; } = "2"; // 2 = IVA, 3 = ICE, 5 = IRBPNR

        [XmlElement("codigoPorcentaje")]
        public string CodigoPorcentaje { get; set; } // Ej: 0 = 0%, 2 = 12%, 3 = 14%, 4 = 15%

        [XmlElement("baseImponible")]
        public decimal BaseImponible { get; set; }

        [XmlElement("tarifa")]
        public decimal Tarifa { get; set; } // El porcentaje real (ej: 15.00)

        [XmlElement("valor")]
        public decimal Valor { get; set; } // El valor calculado
    }

    // Detalle de cada producto
    public class DetalleSRI
    {
        [XmlElement("codigoPrincipal")]
        public string CodigoPrincipal { get; set; }

        [XmlElement("codigoAuxiliar")]
        public string? CodigoAuxiliar { get; set; }

        [XmlElement("descripcion")]
        public string Descripcion { get; set; }

        [XmlElement("cantidad")]
        public decimal Cantidad { get; set; }

        [XmlElement("precioUnitario")]
        public decimal PrecioUnitario { get; set; }

        [XmlElement("descuento")]
        public decimal Descuento { get; set; }

        [XmlElement("precioTotalSinImpuesto")]
        public decimal PrecioTotalSinImpuesto { get; set; }

        [XmlArray("impuestos")]
        [XmlArrayItem("impuesto")]
        public List<Impuesto> Impuestos { get; set; } = new();
    }

    // Impuesto individual por producto
    public class Impuesto
    {
        [XmlElement("codigo")]
        public string Codigo { get; set; } = "2"; // 2 = IVA

        [XmlElement("codigoPorcentaje")]
        public string CodigoPorcentaje { get; set; } // Código de la tabla SRI

        [XmlElement("tarifa")]
        public decimal Tarifa { get; set; } // Ej: 15

        [XmlElement("baseImponible")]
        public decimal BaseImponible { get; set; } // Subtotal de la línea

        [XmlElement("valor")]
        public decimal Valor { get; set; } // Valor del impuesto
    }

    public class PagoSRI
    {
        [XmlElement("formaPago")]
        public string FormaPago { get; set; } = "01"; // 01 = Sin utilización del sistema financiero (Efectivo)

        [XmlElement("total")]
        public decimal Total { get; set; }
    }

    public class CampoAdicional
    {
        [XmlAttribute("nombre")]
        public string Nombre { get; set; }

        [XmlText]
        public string Valor { get; set; }
    }
}