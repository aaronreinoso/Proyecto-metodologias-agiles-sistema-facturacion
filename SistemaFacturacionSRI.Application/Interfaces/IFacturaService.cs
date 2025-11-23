using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IFacturaService
    {
        Task<Factura> CrearFacturaAsync(CreateFacturaDto dto);
    }
}