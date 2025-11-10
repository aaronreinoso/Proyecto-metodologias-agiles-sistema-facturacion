using SistemaFacturacionSRI.Domain.DTOs.LotesProducto;
using SistemaFacturacionSRI.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface ILoteProductoService
    {
        /// Agrega un nuevo lote a un producto existente y actualiza el stock del producto. (HU-006)

        Task<LoteProducto> AddLoteAsync(CreateLoteProducto loteDto);

        /// Busca un lote específico por su clave primaria.
        Task<LoteProducto> GetLoteByIdAsync(int id);

        /// Busca todos los lotes asociados a un ID de producto específico.
        Task<IEnumerable<LoteProducto>> GetLotesByProductoIdAsync(int productoId);
    }
}