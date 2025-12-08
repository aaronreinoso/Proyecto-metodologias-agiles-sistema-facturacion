using System.Xml.Serialization;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Models
{
    // ==========================================
    // 1. MODELOS PARA RESPUESTA DE RECEPCIÓN
    // ==========================================
    [XmlRoot("RespuestaSolicitud")]
    public class RespuestaSolicitud
    {
        [XmlElement("estado")]
        public string Estado { get; set; } // "RECIBIDA" o "DEVUELTA"

        [XmlArray("comprobantes")]
        [XmlArrayItem("comprobante")]
        public List<ComprobanteRecepcion> Comprobantes { get; set; } = new();
    }

    public class ComprobanteRecepcion
    {
        [XmlElement("claveAcceso")]
        public string ClaveAcceso { get; set; }

        [XmlArray("mensajes")]
        [XmlArrayItem("mensaje")]
        public List<MensajeSRI> Mensajes { get; set; } = new();
    }

    public class MensajeSRI
    {
        [XmlElement("identificador")]
        public string Identificador { get; set; }

        [XmlElement("mensaje")]
        public string Mensaje { get; set; }

        [XmlElement("informacionAdicional")]
        public string InformacionAdicional { get; set; }

        [XmlElement("tipo")]
        public string Tipo { get; set; }
    }

    // ==========================================
    // 2. MODELOS PARA RESPUESTA DE AUTORIZACIÓN
    // ==========================================
    [XmlRoot("RespuestaAutorizacion")]
    public class RespuestaAutorizacion
    {
        [XmlElement("numeroComprobantes")]
        public string NumeroComprobantes { get; set; }

        [XmlArray("autorizaciones")]
        [XmlArrayItem("autorizacion")]
        public List<AutorizacionSRI> Autorizaciones { get; set; } = new();
    }

    public class AutorizacionSRI
    {
        [XmlElement("estado")]
        public string Estado { get; set; } // "AUTORIZADO", "NO AUTORIZADO"

        [XmlElement("numeroAutorizacion")]
        public string NumeroAutorizacion { get; set; }

        [XmlElement("fechaAutorizacion")]
        public DateTime FechaAutorizacion { get; set; }

        [XmlElement("ambiente")]
        public string Ambiente { get; set; }

        [XmlElement("comprobante")]
        public string ComprobanteXML { get; set; } // El XML firmado devuelto

        [XmlArray("mensajes")]
        [XmlArrayItem("mensaje")]
        public List<MensajeSRI> Mensajes { get; set; } = new();
    }
}