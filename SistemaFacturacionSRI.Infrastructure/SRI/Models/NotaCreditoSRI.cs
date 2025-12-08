using System.Xml.Serialization;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Models
{
    [XmlRoot("notaCredito")]
    public class NotaCreditoSRI
    {
        [XmlAttribute("id")]
        public string Id { get; set; } = "comprobante";

        [XmlAttribute("version")]
        public string Version { get; set; } = "1.1.0";

        [XmlElement("infoTributaria")]
        public InfoTributaria InfoTributaria { get; set; } // Reusamos la clase existente

        [XmlElement("infoNotaCredito")]
        public InfoNotaCredito InfoNotaCredito { get; set; }

        [XmlArray("detalles")]
        [XmlArrayItem("detalle")]
        public List<DetalleNC_SRI> Detalles { get; set; } = new();
    }

    public class InfoNotaCredito
    {
        public string fechaEmision { get; set; }
        public string dirEstablecimiento { get; set; }
        public string tipoIdentificacionComprador { get; set; }
        public string razonSocialComprador { get; set; }
        public string identificacionComprador { get; set; }
        public string obligadoContabilidad { get; set; }

        // --- CAMPOS ESPECÍFICOS DE NC ---
        public string codDocModificado { get; set; } = "01"; // 01 = Factura
        public string numDocModificado { get; set; } // Formato 001-001-123456789
        public string fechaEmisionDocSustento { get; set; } // Fecha de la Factura original
        public decimal totalSinImpuestos { get; set; }
        public decimal valorModificacion { get; set; } // El Total de la NC
        public string moneda { get; set; } = "DOLAR";

        [XmlArray("totalConImpuestos")]
        [XmlArrayItem("totalImpuesto")]
        public List<TotalImpuesto> TotalConImpuestos { get; set; } = new();

        public string motivo { get; set; }
    }

    public class DetalleNC_SRI
    {
        public string codigoInterno { get; set; }
        public string descripcion { get; set; }
        public int cantidad { get; set; }
        public decimal precioUnitario { get; set; }
        public decimal descuento { get; set; }
        public decimal precioTotalSinImpuesto { get; set; }

        [XmlArray("impuestos")]
        [XmlArrayItem("impuesto")]
        public List<Impuesto> Impuestos { get; set; } = new();
    }
}