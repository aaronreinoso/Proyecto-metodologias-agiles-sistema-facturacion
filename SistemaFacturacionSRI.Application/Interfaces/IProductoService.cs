using SistemaFacturacionSRI.Domain.DTOs.Productos;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IProductoService
    {
        Task<Producto> AddProductoAsync(CreateProducto producto);
        Task<List<Producto>> GetAllProductosAsync();
        Task<Producto?> GetProductoByIdAsync(int id);
        Task<bool> UpdateProductoAsync(UpdateProducto producto);
        Task<bool> DeleteProductoAsync(int id);


        Task<bool> UpdatePrecioProductoAsync(int id, decimal nuevoPrecio);


    }
}
