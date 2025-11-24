using System.Net;
using System.Text;
using System.Xml.Linq;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Services
{
    public class SriSoapClient
    {
        // URLs de Pruebas (Para producción, cambiar 'celcer' por 'cel')
        private const string URL_RECEPCION = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";
        private const string URL_AUTORIZACION = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline?wsdl";

        private readonly HttpClient _httpClient;

        public SriSoapClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> EnviarRecepcionAsync(byte[] xmlFirmadoBytes)
        {
            string xmlBase64 = Convert.ToBase64String(xmlFirmadoBytes);

            // Sobre SOAP para Recepción
            string soapEnvelope = $@"
                <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/' xmlns:ec='http://ec.gob.sri.ws.recepcion'>
                   <soapenv:Header/>
                   <soapenv:Body>
                      <ec:validarComprobante>
                         <xml>{xmlBase64}</xml>
                      </ec:validarComprobante>
                   </soapenv:Body>
                </soapenv:Envelope>";

            return await EnviarSolicitudSoap(URL_RECEPCION, soapEnvelope, "validarComprobante");
        }

        public async Task<string> EnviarAutorizacionAsync(string claveAcceso)
        {
            // Sobre SOAP para Autorización
            string soapEnvelope = $@"
                <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/' xmlns:ec='http://ec.gob.sri.ws.autorizacion'>
                   <soapenv:Header/>
                   <soapenv:Body>
                      <ec:autorizacionComprobante>
                         <claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante>
                      </ec:autorizacionComprobante>
                   </soapenv:Body>
                </soapenv:Envelope>";

            return await EnviarSolicitudSoap(URL_AUTORIZACION, soapEnvelope, "autorizacionComprobante");
        }

        private async Task<string> EnviarSolicitudSoap(string url, string soapEnvelope, string actionName)
        {
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

            // Intentar el envío
            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();
                return responseString; // Devuelve el XML crudo de respuesta del SRI
            }
            catch (Exception ex)
            {
                return $"ERROR_CONEXION: {ex.Message}";
            }
        }
    }
}