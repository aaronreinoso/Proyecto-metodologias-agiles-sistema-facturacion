using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerarFacturaPdf(Factura factura, ConfiguracionSRI config);
    }
}