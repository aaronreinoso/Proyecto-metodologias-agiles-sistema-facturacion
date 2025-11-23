using SistemaFacturacionSRI.Domain.DTOs.Clientes;
using SistemaFacturacionSRI.Domain.Entities;
namespace SistemaFacturacionSRI.Application.Interfaces
{
    public interface IClienteService
    {
        // CREATE: Registra un nuevo cliente
        Task<Cliente> AddClienteAsync(CreateCliente cliente);
        
        // READ: Obtiene todos los clientes
        Task<List<Cliente>> GetAllClientesAsync();

        // READ: Obtiene un cliente por Id
        Task<Cliente> GetClienteByIdAsync(int id);

        // UPDATE: Actualiza un cliente existente
        Task<bool> UpdateClienteAsync(UpdateCliente cliente);

        // DELETE: Elimina un cliente por Id
        Task<bool> DeleteClienteAsync(int id);
    }
}
