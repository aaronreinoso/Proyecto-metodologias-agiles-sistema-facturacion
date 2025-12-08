using SistemaFacturacionSRI.Domain.DTOs.Facturas; // <--- AGREGAR
using SistemaFacturacionSRI.Domain.Entities;
using System.Collections.Generic; // <--- AGREGAR
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IFacturaService
    {
        Task<Factura> CrearFacturaAsync(CreateFacturaDto dto);

        // --- AGREGAR ESTA LÍNEA ---
        Task<List<FacturaResumenDto>> ObtenerHistorialFacturasAsync();
    }
}