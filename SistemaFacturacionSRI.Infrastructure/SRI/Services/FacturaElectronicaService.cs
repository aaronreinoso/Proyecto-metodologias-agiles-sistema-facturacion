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
            // Usamos el tipo de redondeo solicitado (Half-Up)
            const MidpointRounding RoundingType = MidpointRounding.AwayFromZero;

            var xml = new FacturaSRI();
            xml.Version = "1.1.0";

            // --- Info Tributaria ---
            xml.InfoTributaria = new InfoTributaria
            {
                Ambiente = ambiente,
                TipoEmision = "1",
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
                TipoIdentificacionComprador =
                    f.Cliente.Identificacion == "9999999999999" ? "07" :
                    f.Cliente.Identificacion.Length == 13 ? "04" :
                    "05",

                RazonSocialComprador = f.Cliente.NombreCompleto,
                IdentificacionComprador = f.Cliente.Identificacion,
                TotalSinImpuestos = f.Subtotal, // El total sin impuestos puede ir con más decimales.
                TotalDescuento = 0,
                Propina = 0,

                // --- CAMBIO CRITICO 1: Importe Total a 2 decimales (Half-Up) ---
                ImporteTotal = Math.Round(f.Total, 2, RoundingType),

                Moneda = "DOLAR",
                Pagos = new List<PagoSRI> {
            // --- CAMBIO CRITICO 2: Total de Pago debe coincidir con el Importe Total redondeado ---
            new PagoSRI { FormaPago = "01", Total = Math.Round(f.Total, 2, RoundingType) }
        }
            };

            // --- Totales de Impuestos ---
            var impuestosAgrupados = f.Detalles
                .GroupBy(d => d.Producto.TarifaIva)
                .Select(g => new TotalImpuesto
                {
                    Codigo = "2",
                    CodigoPorcentaje = g.Key.CodigoSRI,
                    BaseImponible = g.Sum(x => x.Subtotal),
                    // --- CAMBIO CRITICO 3: Valor del impuesto a 2 decimales (Half-Up) ---
                    Valor = Math.Round(g.Sum(x => x.Subtotal * (x.Producto.TarifaIva.Porcentaje / 100m)), 2, RoundingType),
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
                    Codigo = "2",
                    CodigoPorcentaje = det.Producto.TarifaIva.CodigoSRI,
                    Tarifa = det.Producto.TarifaIva.Porcentaje,
                    BaseImponible = det.Subtotal,
                    // --- CAMBIO CRITICO 4: Valor impuesto por ítem a 2 decimales (Half-Up) ---
                    Valor = Math.Round(det.Subtotal * (det.Producto.TarifaIva.Porcentaje / 100m), 2, RoundingType)
                });

                xml.Detalles.Add(detalleXml);
            }

            // --- INFO ADICIONAL ---
            if (!string.IsNullOrEmpty(f.Cliente.Direccion))
            {
                xml.InfoAdicional.Add(new CampoAdicional { Nombre = "Dirección", Valor = f.Cliente.Direccion });
            }
            if (!string.IsNullOrEmpty(f.Cliente.Email))
            {
                xml.InfoAdicional.Add(new CampoAdicional { Nombre = "Email", Valor = f.Cliente.Email });
            }
            if (!string.IsNullOrEmpty(f.Cliente.Telefono))
            {
                xml.InfoAdicional.Add(new CampoAdicional { Nombre = "Teléfono", Valor = f.Cliente.Telefono });
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



        public byte[] GenerarXmlNotaCreditoFirmado(NotaCredito nc, ConfiguracionSRI configEmisor, string passwordFirma)
        {
            // 1. Validar datos mínimos de la firma
            if (configEmisor.FirmaElectronica == null || configEmisor.FirmaElectronica.Length == 0)
                throw new Exception("No se ha cargado la firma electrónica en la configuración.");

            // 2. Generar Clave de Acceso
            string ambiente = "1"; // 1: Pruebas, 2: Producción (Debería venir de config o DB)
            string secuencial = nc.Id.ToString().PadLeft(9, '0');
            string codigoNumerico = new Random().Next(10000000, 99999999).ToString();

            // "04" es el código para Nota de Crédito según tabla 3 del SRI
            string claveAcceso = _claveService.GenerarClaveAcceso(
                nc.FechaEmision,
                "04",
                configEmisor.Ruc,
                ambiente,
                configEmisor.CodigoEstablecimiento,
                configEmisor.CodigoPuntoEmision,
                secuencial,
                codigoNumerico
            );

            nc.ClaveAcceso = claveAcceso;

            // 3. Mapear Dominio -> Modelo XML SRI
            var ncSRI = new NotaCreditoSRI();

            // --- Info Tributaria ---
            ncSRI.InfoTributaria = new InfoTributaria
            {
                Ambiente = ambiente,
                TipoEmision = "1",
                RazonSocial = configEmisor.RazonSocial,
                NombreComercial = configEmisor.NombreComercial ?? configEmisor.RazonSocial,
                Ruc = configEmisor.Ruc,
                ClaveAcceso = claveAcceso,
                CodDoc = "04", // 04 = Nota Crédito
                Estab = configEmisor.CodigoEstablecimiento,
                PtoEmi = configEmisor.CodigoPuntoEmision,
                Secuencial = secuencial,
                DirMatriz = configEmisor.DireccionMatriz
            };

            // Usamos el tipo de redondeo requerido por el SRI
            const MidpointRounding RoundingType = MidpointRounding.AwayFromZero;

            // --- Info Nota Crédito ---
            ncSRI.InfoNotaCredito = new InfoNotaCredito
            {
                fechaEmision = nc.FechaEmision.ToString("dd/MM/yyyy"),
                dirEstablecimiento = configEmisor.DireccionEstablecimiento ?? configEmisor.DireccionMatriz,

                // Lógica tipo ID del comprador (reusando lógica de factura)
                tipoIdentificacionComprador =
                    nc.Factura.Cliente.Identificacion == "9999999999999" ? "07" :
                    nc.Factura.Cliente.Identificacion.Length == 13 ? "04" :
                    "05",

                razonSocialComprador = nc.Factura.Cliente.NombreCompleto,
                identificacionComprador = nc.Factura.Cliente.Identificacion,
                obligadoContabilidad = configEmisor.ObligadoContabilidad ? "SI" : "NO",

                // --- DATOS DEL DOCUMENTO MODIFICADO (FACTURA) ---
                codDocModificado = "01", // 01 = Factura
                numDocModificado = $"{configEmisor.CodigoEstablecimiento}-{configEmisor.CodigoPuntoEmision}-{nc.Factura.Id:D9}",
                fechaEmisionDocSustento = nc.Factura.FechaEmision.ToString("dd/MM/yyyy"),
                motivo = nc.Motivo,

                // Totales globales
                totalSinImpuestos = Math.Round(nc.Subtotal, 2, RoundingType),
                valorModificacion = Math.Round(nc.Total, 2, RoundingType),
                moneda = "DOLAR"
            };

            // --- Total Con Impuestos (Agrupado) ---
            // Calculamos el total de IVA agrupado por tarifa (ej: todo lo que es 15%, todo lo que es 0%)
            var impuestosAgrupados = nc.Detalles
                .GroupBy(d => d.Producto.TarifaIva)
                .Select(g => new TotalImpuesto
                {
                    Codigo = "2", // 2 = IVA
                    CodigoPorcentaje = g.Key.CodigoSRI,
                    BaseImponible = Math.Round(g.Sum(x => x.Subtotal), 2, RoundingType),
                    Valor = Math.Round(g.Sum(x => x.Subtotal * (x.Producto.TarifaIva.Porcentaje / 100m)), 2, RoundingType),
                    Tarifa = g.Key.Porcentaje // Opcional en el XML pero útil
                }).ToList();

            ncSRI.InfoNotaCredito.TotalConImpuestos = impuestosAgrupados;

            // --- Detalles ---
            foreach (var det in nc.Detalles)
            {
                var detalleXml = new DetalleNC_SRI
                {
                    codigoInterno = det.Producto.CodigoPrincipal,
                    descripcion = det.Producto.Descripcion,
                    cantidad = det.Cantidad,
                    precioUnitario = det.PrecioUnitario,
                    descuento = 0, // Se asume 0 para simplificar, o agregar campo descuento en entidad
                    precioTotalSinImpuesto = det.Subtotal
                };

                // Agregar el impuesto de este ítem específico
                detalleXml.Impuestos.Add(new Impuesto
                {
                    Codigo = "2", // IVA
                    CodigoPorcentaje = det.Producto.TarifaIva.CodigoSRI,
                    Tarifa = det.Producto.TarifaIva.Porcentaje,
                    BaseImponible = det.Subtotal,
                    Valor = Math.Round(det.Subtotal * (det.Producto.TarifaIva.Porcentaje / 100m), 2, RoundingType)
                });

                ncSRI.Detalles.Add(detalleXml);
            }

            // 4. Serializar a XML String (Forzando UTF-8 sin BOM)
            string xmlString = "";
            var serializer = new XmlSerializer(typeof(NotaCreditoSRI));

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (var stringWriter = new Utf8StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    serializer.Serialize(xmlWriter, ncSRI);
                }
                xmlString = stringWriter.ToString();
            }

            // 5. Firmar el XML (XAdES-BES)
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(xmlString);

            // Reutilizamos tu método privado existente FirmarXml
            FirmarXml(xmlDoc, configEmisor.FirmaElectronica, passwordFirma);

            return Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
        }



    }

    // Clase auxiliar para forzar UTF-8 en el StringWriter
    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }






}