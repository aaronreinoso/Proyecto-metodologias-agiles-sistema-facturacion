using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.Entities;

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
                // Lado Izquierdo: Datos Emisor
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(config.RazonSocial).FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"RUC: {config.Ruc}");
                    column.Item().Text($"Dir: {config.DireccionMatriz}");
                    column.Item().Text("Obligado a llevar contabilidad: SI");
                });

                // Lado Derecho: Datos Factura
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("R.U.C.: " + config.Ruc).Bold();
                    column.Item().Text("FACTURA").FontSize(14).Bold();
                    // Aquí deberías formatear el número completo
                    column.Item().Text($"No. {config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}-{factura.Id.ToString("D9")}");
                    column.Item().Text($"AUTORIZACIÓN: {factura.FechaAutorizacion}");
                    column.Item().Text($"CLAVE ACCESO:").FontSize(8);
                    column.Item().Text(factura.ClaveAcceso).FontSize(8);
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
    }
}