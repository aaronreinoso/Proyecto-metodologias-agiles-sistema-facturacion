using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using SistemaFacturacionSRI.Application.Interfaces;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task EnviarFacturaAsync(string destinatario, string xmlContent, byte[] pdfBytes, string numeroFactura)
        {
            if (string.IsNullOrEmpty(destinatario)) return; // Si no hay correo, salimos sin error

            var email = new MimeMessage();
            var smtpSettings = _config.GetSection("SmtpSettings");

            email.From.Add(new MailboxAddress(smtpSettings["SenderName"], smtpSettings["SenderEmail"]));
            email.To.Add(new MailboxAddress("", destinatario));
            email.Subject = $"Comprobante Electrónico - Factura {numeroFactura}";

            var builder = new BodyBuilder();
            builder.TextBody = $"Estimado cliente, adjunto encontrará su factura electrónica No. {numeroFactura}.";

            // Adjuntar PDF
            builder.Attachments.Add($"Factura_{numeroFactura}.pdf", pdfBytes, new ContentType("application", "pdf"));

            // Adjuntar XML (Convertimos el string XML a stream)
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent)))
            {
                builder.Attachments.Add($"Factura_{numeroFactura}.xml", stream, new ContentType("application", "xml"));
            }

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(smtpSettings["Server"], int.Parse(smtpSettings["Port"]), MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(smtpSettings["Username"], smtpSettings["Password"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}