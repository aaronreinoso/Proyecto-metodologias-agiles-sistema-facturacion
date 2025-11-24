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

        /// <summary>
        /// Actualiza la cantidad actual de un lote y registra el movimiento de ajuste. (HU-008)
        /// </summary>
        Task<bool> RealizarAjusteManualAsync(CreateAjusteInventarioDto ajusteDto); // <--- AGREGAR ESTA LÍNEA

        /// <summary>
        /// Obtiene el historial de ajustes para un lote específico.
        /// </summary>
        Task<IEnumerable<AjusteInventario>> GetAjustesByLoteIdAsync(int loteId); // <--- AGREGAR ESTA LÍNEA

    }
}