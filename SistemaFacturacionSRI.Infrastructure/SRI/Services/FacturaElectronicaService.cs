using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml; // Requerido: System.Security.Cryptography.Xml
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.SRI.Models;

namespace SistemaFacturacionSRI.Infrastructure.SRI.Services
{
    public class FacturaElectronicaService
    {
        private readonly ClaveAccesoService _claveService;

        // DATOS DE LA EMPRESA EMISORA (TODO: Deberías inyectar esto desde IConfiguration o una base de datos)
        private const string RUC_EMISOR = "1790000000001";
        private const string RAZON_SOCIAL_EMISOR = "MI EMPRESA S.A.";
        private const string DIR_MATRIZ = "Av. Los Shyris y Suecia";
        private const string ESTAB = "001";
        private const string PTO_EMI = "002";

        public FacturaElectronicaService()
        {
            _claveService = new ClaveAccesoService();
        }

        public byte[] GenerarXmlFirmado(Factura facturaDominio, string pathFirma, string passwordFirma)
        {
            // 1. Generar Clave de Acceso
            // IMPORTANTE: Aquí asumimos ambiente "1" (Pruebas). Cambiar a "2" para producción.
            string ambiente = "1";
            // Generamos un secuencial formateado a 9 dígitos (ej: 1 -> 000000001)
            string secuencial = facturaDominio.Id.ToString().PadLeft(9, '0');
            string codigoNumerico = new Random().Next(10000000, 99999999).ToString(); // Aleatorio 8 dígitos

            string claveAcceso = _claveService.GenerarClaveAcceso(
                facturaDominio.FechaEmision,
                "01", // Tipo Factura
                RUC_EMISOR,
                ambiente,
                ESTAB,
                PTO_EMI,
                secuencial,
                codigoNumerico
            );

            // Guardamos la clave generada en la entidad de dominio para referencia futura
            facturaDominio.ClaveAcceso = claveAcceso;

            // 2. Mapear Dominio (BD) -> Modelo XML SRI
            var facturaSRI = MapearFactura(facturaDominio, claveAcceso, ambiente, secuencial);

            // 3. Serializar a XML String (UTF-8)
            string xmlString = "";
            var serializer = new XmlSerializer(typeof(FacturaSRI));

            // Usamos XmlWriterSettings para evitar el BOM y asegurar UTF-8
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // False para quitar BOM
                Indent = true
            };

            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    serializer.Serialize(xmlWriter, facturaSRI);
                }
                xmlString = stringWriter.ToString();
            }

            // 4. Firmar el XML (XAdES-BES)
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(xmlString);

            FirmarXml(xmlDoc, pathFirma, passwordFirma);

            return Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
        }

        private FacturaSRI MapearFactura(Factura f, string claveAcceso, string ambiente, string secuencial)
        {
            var xml = new FacturaSRI();

            // --- Info Tributaria ---
            xml.InfoTributaria = new InfoTributaria
            {
                Ambiente = ambiente,
                TipoEmision = "1",
                RazonSocial = RAZON_SOCIAL_EMISOR,
                NombreComercial = RAZON_SOCIAL_EMISOR,
                Ruc = RUC_EMISOR,
                ClaveAcceso = claveAcceso,
                CodDoc = "01", // Factura
                Estab = ESTAB,
                PtoEmi = PTO_EMI,
                Secuencial = secuencial,
                DirMatriz = DIR_MATRIZ
            };

            // --- Info Factura ---
            xml.InfoFactura = new InfoFactura
            {
                FechaEmision = f.FechaEmision.ToString("dd/MM/yyyy"),
                DirEstablecimiento = DIR_MATRIZ,
                ObligadoContabilidad = "NO",
                // TODO: Validar lógica para tipo ID (05 cedula, 04 ruc, 06 pasaporte, 07 consumidor final)
                TipoIdentificacionComprador = f.Cliente.Identificacion.Length == 13 ? "04" : "05",
                RazonSocialComprador = f.Cliente.NombreCompleto,
                IdentificacionComprador = f.Cliente.Identificacion,
                TotalSinImpuestos = f.Subtotal,
                TotalDescuento = 0, // TODO: Implementar si tu sistema maneja descuentos
                Propina = 0,
                ImporteTotal = f.Total,
                Moneda = "DOLAR",
                Pagos = new List<PagoSRI> {
                    new PagoSRI { FormaPago = "01", Total = f.Total } // 01 = Sin utilización del sistema financiero
                }
            };

            // --- Totales de Impuestos (Resumen) ---
            // Aquí agrupamos por IVA. En este ejemplo asumimos todo al 15% (Código 4)
            // Si tienes productos con IVA 0%, debes agregar lógica para agruparlos.
            xml.InfoFactura.TotalConImpuestos.Add(new TotalImpuesto
            {
                Codigo = "2", // IVA
                CodigoPorcentaje = "4", // 4 = 15% (Según tabla SRI vigente 2024/2025)
                BaseImponible = f.Subtotal,
                Valor = f.TotalIVA
            });

            // --- Detalles ---
            foreach (var det in f.Detalles)
            {
                var detalleXml = new DetalleSRI
                {
                    CodigoPrincipal = det.Producto.CodigoPrincipal,
                    Descripcion = det.Producto.Descripcion,
                    Cantidad = det.Cantidad,
                    PrecioUnitario = det.PrecioUnitario,
                    Descuento = 0,
                    PrecioTotalSinImpuesto = det.Subtotal
                };

                // Impuesto individual por producto
                detalleXml.Impuestos.Add(new Impuesto
                {
                    Codigo = "2", // IVA
                    CodigoPorcentaje = "4", // 15% (Ajustar dinámicamente según el producto)
                    Tarifa = 15,
                    BaseImponible = det.Subtotal,
                    Valor = det.Subtotal * 0.15m
                });

                xml.Detalles.Add(detalleXml);
            }

            return xml;
        }

        private void FirmarXml(XmlDocument xmlDoc, string pathFirma, string password)
        {
            // 1. Cargar certificado
            // IMPORTANTE: En Linux/Docker puede requerir ajustes en X509KeyStorageFlags
            if (!File.Exists(pathFirma)) throw new FileNotFoundException($"No se encontró la firma en: {pathFirma}");

            var certificado = new X509Certificate2(pathFirma, password, X509KeyStorageFlags.Exportable);

            // 2. Instanciar SignedXml
            SignedXml signedXml = new SignedXml(xmlDoc);
            signedXml.SigningKey = certificado.PrivateKey;

            // 3. Crear Referencia (Firmar todo el documento)
            Reference reference = new Reference("");
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            signedXml.AddReference(reference);

            // 4. Agregar KeyInfo (Información del certificado público para que el SRI valide)
            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            // 5. Calcular Firma
            signedXml.ComputeSignature();

            // 6. Obtener la representación XML de la firma
            XmlElement xmlDigitalSignature = signedXml.GetXml();

            // 7. Adjuntar la firma al documento original
            xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
        }
    }
}