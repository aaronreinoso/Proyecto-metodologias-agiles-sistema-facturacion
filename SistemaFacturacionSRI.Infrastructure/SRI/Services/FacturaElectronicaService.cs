using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
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

        public FacturaElectronicaService()
        {
            _claveService = new ClaveAccesoService();
        }

        // MODIFICADO: Ahora recibe la configuración (datos emisor + firma en bytes) y la contraseña descifrada
        public byte[] GenerarXmlFirmado(Factura facturaDominio, ConfiguracionSRI configEmisor, string passwordFirma)
        {
            // 1. Validar datos mínimos
            if (configEmisor.FirmaElectronica == null || configEmisor.FirmaElectronica.Length == 0)
                throw new Exception("No se ha cargado la firma electrónica en la configuración.");

            // 2. Generar Clave de Acceso
            // Nota: El ambiente '1' es Pruebas, '2' Producción. Podrías agregarlo a tu tabla Configuración si deseas.
            string ambiente = "1";

            string secuencial = facturaDominio.Id.ToString().PadLeft(9, '0');
            string codigoNumerico = new Random().Next(10000000, 99999999).ToString();

            string claveAcceso = _claveService.GenerarClaveAcceso(
                facturaDominio.FechaEmision,
                "01", // Tipo Factura
                configEmisor.Ruc, // Usamos el RUC de la DB
                ambiente,
                configEmisor.CodigoEstablecimiento, // Usamos Estab de la DB
                configEmisor.CodigoPuntoEmision,    // Usamos PtoEmi de la DB
                secuencial,
                codigoNumerico
            );

            facturaDominio.ClaveAcceso = claveAcceso;

            // 3. Mapear Dominio (BD) -> Modelo XML SRI (Pasamos la config)
            var facturaSRI = MapearFactura(facturaDominio, configEmisor, claveAcceso, ambiente, secuencial);

            // 4. Serializar a XML String (UTF-8)
            string xmlString = "";
            var serializer = new XmlSerializer(typeof(FacturaSRI));

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
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

            // 5. Firmar el XML (XAdES-BES) usando BYTES
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(xmlString);

            // Pasamos los bytes de la firma
            FirmarXml(xmlDoc, configEmisor.FirmaElectronica, passwordFirma);

            return Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
        }

        private FacturaSRI MapearFactura(Factura f, ConfiguracionSRI config, string claveAcceso, string ambiente, string secuencial)
        {
            var xml = new FacturaSRI();

            // --- Info Tributaria (Datos dinámicos desde la DB) ---
            xml.InfoTributaria = new InfoTributaria
            {
                Ambiente = ambiente,
                TipoEmision = "1", // Normal
                RazonSocial = config.RazonSocial,
                NombreComercial = config.NombreComercial ?? config.RazonSocial,
                Ruc = config.Ruc,
                ClaveAcceso = claveAcceso,
                CodDoc = "01",
                Estab = config.CodigoEstablecimiento,
                PtoEmi = config.CodigoPuntoEmision,
                Secuencial = secuencial,
                DirMatriz = config.DireccionMatriz
            };

            // --- Info Factura ---
            xml.InfoFactura = new InfoFactura
            {
                FechaEmision = f.FechaEmision.ToString("dd/MM/yyyy"),
                DirEstablecimiento = config.DireccionEstablecimiento ?? config.DireccionMatriz,
                ObligadoContabilidad = config.ObligadoContabilidad ? "SI" : "NO",
                ContribuyenteEspecial = config.ContribuyenteEspecial, // Si aplica

                // TODO: Asegúrate que tu cliente tenga 13 dígitos para RUC o 10 para cédula
                TipoIdentificacionComprador = f.Cliente.Identificacion.Length == 13 ? "04" : "05",
                RazonSocialComprador = f.Cliente.NombreCompleto,
                IdentificacionComprador = f.Cliente.Identificacion,
                TotalSinImpuestos = f.Subtotal,
                TotalDescuento = 0,
                Propina = 0,
                ImporteTotal = f.Total,
                Moneda = "DOLAR",
                Pagos = new List<PagoSRI> {
                    new PagoSRI { FormaPago = "01", Total = f.Total }
                }
            };

            // --- Totales de Impuestos ---
            // Agrupar detalles por código de impuesto para el resumen
            var impuestosAgrupados = f.Detalles
                .GroupBy(d => d.Producto.TarifaIva)
                .Select(g => new TotalImpuesto
                {
                    Codigo = "2", // IVA
                    CodigoPorcentaje = g.Key.CodigoSRI,
                    BaseImponible = g.Sum(x => x.Subtotal),
                    Valor = g.Sum(x => x.Subtotal * (x.Producto.TarifaIva.Porcentaje / 100m)),
                    Tarifa = g.Key.Porcentaje
                }).ToList();

            xml.InfoFactura.TotalConImpuestos = impuestosAgrupados;


            // --- Detalles ---
            foreach (var det in f.Detalles)
            {
                var detalleXml = new DetalleSRI
                {
                    CodigoPrincipal = det.Producto.CodigoPrincipal,
                    Descripcion = det.Producto.Descripcion, // Usamos la descripción completa generada
                    Cantidad = det.Cantidad,
                    PrecioUnitario = det.PrecioUnitario,
                    Descuento = 0,
                    PrecioTotalSinImpuesto = det.Subtotal
                };

                // Impuesto individual
                detalleXml.Impuestos.Add(new Impuesto
                {
                    Codigo = "2", // IVA
                    CodigoPorcentaje = det.Producto.TarifaIva.CodigoSRI,
                    Tarifa = det.Producto.TarifaIva.Porcentaje,
                    BaseImponible = det.Subtotal,
                    Valor = det.Subtotal * (det.Producto.TarifaIva.Porcentaje / 100m)
                });

                xml.Detalles.Add(detalleXml);
            }

            return xml;
        }

        // MODIFICADO: Acepta byte[] en lugar de string path
        private void FirmarXml(XmlDocument xmlDoc, byte[] firmaBytes, string password)
        {
            // 1. Cargar certificado desde MEMORIA (bytes)
            // MachineKeySet es vital para evitar errores de "Key not valid for use in specified state" en IIS/Azure
            var certificado = new X509Certificate2(firmaBytes, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);

            // 2. Instanciar SignedXml
            SignedXml signedXml = new SignedXml(xmlDoc);
            signedXml.SigningKey = certificado.PrivateKey;

            // 3. Crear Referencia
            Reference reference = new Reference("");
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            signedXml.AddReference(reference);

            // 4. Agregar KeyInfo
            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            // 5. Calcular Firma
            signedXml.ComputeSignature();

            // 6. Adjuntar
            XmlElement xmlDigitalSignature = signedXml.GetXml();
            xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
        }
    }
}