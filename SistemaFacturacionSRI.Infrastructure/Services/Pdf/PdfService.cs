using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.Entities;
using QRCoder;

namespace SistemaFacturacionSRI.Infrastructure.Services.Pdf
{
    public class PdfService : IPdfService
    {
        public PdfService()
        {
            // Licencia Community (Gratis para uso no enterprise)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerarFacturaPdf(Factura factura, ConfiguracionSRI config)
        {
            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(header => ComponerCabecera(header, factura, config));
                    page.Content().Element(content => ComponerContenido(content, factura));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private void ComponerCabecera(IContainer container, Factura factura, ConfiguracionSRI config)
        {
            container.Row(row =>
            {
                // COLUMNA 1: LOGO Y DATOS EMISOR (Izquierda)
                row.RelativeItem(5).Column(column =>
                {
                    // Si tuvieras logo: column.Item().Image(logoBytes).Width(150);
                    column.Item().Text(config.RazonSocial).FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"RUC: {config.Ruc}").FontSize(10);
                    column.Item().Text($"Dir: {config.DireccionMatriz}").FontSize(10);
                    column.Item().Text("Obligado a llevar contabilidad: SI").FontSize(10);

                    if (!string.IsNullOrEmpty(config.ContribuyenteEspecial))
                    {
                        column.Item().Text($"Contribuyente Especial Nro: {config.ContribuyenteEspecial}").FontSize(10);
                    }
                });

                // COLUMNA 2: DATOS FACTURA Y QR (Derecha)
                row.RelativeItem(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
                {
                    column.Item().Text($"R.U.C.: {config.Ruc}").Bold().FontSize(12);
                    column.Item().Text("FACTURA").FontSize(12).Bold();

                    string numeroFactura = $"{config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}-{factura.Id:D9}";
                    column.Item().Text($"No. {numeroFactura}").FontSize(11);

                    column.Item().Text($"AUTORIZACIÓN:").FontSize(9).FontColor(Colors.Grey.Darken1);
                    // Aquí iría el número de autorización real si ya fue autorizado, por ahora usamos la fecha
                    column.Item().Text($"{factura.FechaAutorizacion}").FontSize(9);

                    column.Item().Text("CLAVE DE ACCESO:").FontSize(9).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(factura.ClaveAcceso).FontSize(8).SemiBold();

                    // --- AQUÍ INSERTAMOS EL QR ---
                    if (!string.IsNullOrEmpty(factura.ClaveAcceso))
                    {
                        column.Item().PaddingTop(5).AlignRight().Width(80).Image(GenerarCodigoQR(factura.ClaveAcceso));
                    }
                    // -----------------------------
                });
            });
        }
        private void ComponerContenido(IContainer container, Factura factura)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Datos Cliente
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(c =>
                {
                    c.Item().Text($"Razón Social: {factura.Cliente?.NombreCompleto}");
                    c.Item().Text($"Identificación: {factura.Cliente?.Identificacion}");
                    c.Item().Text($"Fecha Emisión: {factura.FechaEmision:dd/MM/yyyy}");
                });

                column.Item().PaddingVertical(10);

                // Tabla de Productos
                column.Item().Table(tabla =>
                {
                    tabla.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40); // Cantidad
                        columns.RelativeColumn();   // Descripción
                        columns.ConstantColumn(60); // P.Unit
                        columns.ConstantColumn(60); // Total
                    });

                    tabla.Header(header =>
                    {
                        header.Cell().Element(EstiloCelda).Text("Cant");
                        header.Cell().Element(EstiloCelda).Text("Descripción");
                        header.Cell().Element(EstiloCelda).Text("P.Unit");
                        header.Cell().Element(EstiloCelda).Text("Total");
                    });

                    foreach (var item in factura.Detalles)
                    {
                        tabla.Cell().Element(EstiloCeldaBody).Text(item.Cantidad.ToString());
                        tabla.Cell().Element(EstiloCeldaBody).Text(item.Producto?.Descripcion ?? "Item");
                        tabla.Cell().Element(EstiloCeldaBody).AlignRight().Text($"${item.PrecioUnitario:F2}");
                        tabla.Cell().Element(EstiloCeldaBody).AlignRight().Text($"${item.Subtotal:F2}");
                    }
                });

                // Totales
                column.Item().AlignRight().PaddingTop(10).Column(c =>
                {
                    c.Item().Text($"Subtotal: ${factura.Subtotal:F2}");
                    c.Item().Text($"IVA: ${factura.TotalIVA:F2}");
                    c.Item().Text($"TOTAL: ${factura.Total:F2}").Bold().FontSize(12);
                });
            });
        }

        static IContainer EstiloCelda(IContainer container)
        {
            return container.Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten3).Padding(5);
        }

        static IContainer EstiloCeldaBody(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
        }

        private byte[] GenerarCodigoQR(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return Array.Empty<byte>();

            // 1. Crear el generador
            using var qrGenerator = new QRCodeGenerator();

            // 2. Generar los datos del QR (Nivel Q es buena calidad)
            using var qrCodeData = qrGenerator.CreateQrCode(texto, QRCodeGenerator.ECCLevel.Q);

            // 3. Renderizar a PNG (Byte Array) - No requiere System.Drawing
            using var qrCode = new PngByteQRCode(qrCodeData);

            // "20" es los píxeles por módulo (tamaño de la imagen)
            return qrCode.GetGraphic(20);
        }

    }




}