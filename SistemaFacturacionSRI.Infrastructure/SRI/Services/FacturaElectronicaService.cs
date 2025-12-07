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

        public byte[] GenerarXmlFirmado(Factura facturaDominio, ConfiguracionSRI configEmisor, string passwordFirma)
        {
            // 1. Validar datos mínimos
            if (configEmisor.FirmaElectronica == null || configEmisor.FirmaElectronica.Length == 0)
                throw new Exception("No se ha cargado la firma electrónica en la configuración.");

            // 2. Generar Clave de Acceso
            string ambiente = "1"; // 1: Pruebas, 2: Producción

            string secuencial = facturaDominio.Id.ToString().PadLeft(9, '0');
            string codigoNumerico = new Random().Next(10000000, 99999999).ToString();

            string claveAcceso = _claveService.GenerarClaveAcceso(
                facturaDominio.FechaEmision,
                "01", // Tipo Factura
                configEmisor.Ruc,
                ambiente,
                configEmisor.CodigoEstablecimiento,
                configEmisor.CodigoPuntoEmision,
                secuencial,
                codigoNumerico
            );

            facturaDominio.ClaveAcceso = claveAcceso;

            // 3. Mapear Dominio -> Modelo XML SRI
            var facturaSRI = MapearFactura(facturaDominio, configEmisor, claveAcceso, ambiente, secuencial);

            // 4. Serializar a XML String (Forzando UTF-8)
            string xmlString = "";
            var serializer = new XmlSerializer(typeof(FacturaSRI));

            // Configuración para el XML
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // false = Sin BOM (Byte Order Mark)
                Indent = true,
                OmitXmlDeclaration = false
            };

            // Usamos nuestra clase auxiliar que fuerza la codificación UTF-8
            using (var stringWriter = new Utf8StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    serializer.Serialize(xmlWriter, facturaSRI);
                }
                xmlString = stringWriter.ToString();
            }

            // 5. Firmar el XML (XAdES-BES)
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(xmlString);

            FirmarXml(xmlDoc, configEmisor.FirmaElectronica, passwordFirma);

            return Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
        }

        private FacturaSRI MapearFactura(Factura f, ConfiguracionSRI config, string claveAcceso, string ambiente, string secuencial)
        {
            var xml = new FacturaSRI();
            xml.Version = "1.1.0"; // Versión vigente del esquema SRI

            // --- Info Tributaria ---
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
                ContribuyenteEspecial = config.ContribuyenteEspecial,

                // Lógica tipo ID
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
                    Descripcion = det.Producto.Descripcion,
                    Cantidad = det.Cantidad,
                    PrecioUnitario = det.PrecioUnitario,
                    Descuento = 0,
                    PrecioTotalSinImpuesto = det.Subtotal
                };

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

            // --- INFO ADICIONAL (CORREO Y DIRECCIÓN DEL CLIENTE) ---
            // Aquí inyectamos los datos para que salgan en el XML y RIDE

            // 1. Dirección (Si existe en la BD)
            if (!string.IsNullOrEmpty(f.Cliente.Direccion))
            {
                xml.InfoAdicional.Add(new CampoAdicional
                {
                    Nombre = "Dirección",
                    Valor = f.Cliente.Direccion
                });
            }

            // 2. Email (Si existe en la BD)
            if (!string.IsNullOrEmpty(f.Cliente.Email))
            {
                xml.InfoAdicional.Add(new CampoAdicional
                {
                    Nombre = "Email",
                    Valor = f.Cliente.Email
                });
            }

            // 3. Teléfono (Si tu entidad Cliente tiene este campo, descomenta esto)
            
            if (!string.IsNullOrEmpty(f.Cliente.Telefono))
            {
                xml.InfoAdicional.Add(new CampoAdicional 
                { 
                    Nombre = "Teléfono", 
                    Valor = f.Cliente.Telefono 
                });
            }
            

            return xml;
        }

        private void FirmarXml(XmlDocument xmlDoc, byte[] firmaBytes, string password)
        {
            // Cargar certificado con flags robustos
            var certificado = new X509Certificate2(
                firmaBytes,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            SignedXml signedXml = new SignedXml(xmlDoc);
            signedXml.SigningKey = certificado.PrivateKey;

            Reference reference = new Reference("");
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            signedXml.AddReference(reference);

            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();

            XmlElement xmlDigitalSignature = signedXml.GetXml();
            xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
        }
    }

    // Clase auxiliar para forzar UTF-8 en el StringWriter
    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}