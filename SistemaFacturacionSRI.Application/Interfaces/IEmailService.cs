public interface IEmailService
{
    Task EnviarFacturaAsync(string destinatario, string xmlPath, byte[] pdfBytes, string numeroFactura);
}