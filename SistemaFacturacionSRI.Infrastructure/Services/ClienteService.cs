using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Clientes;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.Infrastructure.Services
{
    public class ClienteService : IClienteService
    {
        private readonly AppDbContext _context;

        public ClienteService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Cliente> AddClienteAsync(CreateCliente cliente)
        {
            // Mapeo DTO a Entidad
            var nuevocliente = new Cliente
            {
                Identificacion = cliente.Identificacion,
                NombreCompleto = cliente.NombreCompleto,
                Direccion = cliente.Direccion,
                Telefono = cliente.Telefono,
                Email = cliente.Email
            };

            _context.Clientes.Add(nuevocliente);
            await _context.SaveChangesAsync();

            // Mapeo Entidad a DTO para retornar el objeto creado con el Id generado
            return new Cliente
            {
                Identificacion = cliente.Identificacion,
                NombreCompleto = cliente.NombreCompleto,
                Direccion = cliente.Direccion,
                Telefono = cliente.Telefono,
                Email = cliente.Email
            };
        }

        // --- READ ALL ---
        public async Task<List<Cliente>> GetAllClientesAsync()
        {
            return await _context.Clientes
                .Select(c => new Cliente
                {
                    Id = c.Id,
                    Identificacion = c.Identificacion,
                    NombreCompleto = c.NombreCompleto,
                    Direccion = c.Direccion,
                    Telefono = c.Telefono,
                    Email = c.Email
                })
                .ToListAsync();
        }

        // --- READ BY ID ---
        public async Task<Cliente?> GetClienteByIdAsync(int id)
        {
            return await _context.Clientes
                .Where(c => c.Id == id)
                .Select(c => new Cliente
                {
                    Id = c.Id,
                    Identificacion = c.Identificacion,
                    NombreCompleto = c.NombreCompleto,
                    Direccion = c.Direccion,
                    Telefono = c.Telefono,
                    Email = c.Email
                })
                .FirstOrDefaultAsync();
        }

        // --- UPDATE ---
        public async Task<bool> UpdateClienteAsync(UpdateCliente cliente)
        {
            var borrarCliente = await _context.Clientes.FindAsync(cliente.Id);

            if (borrarCliente == null)
            {
                return false; // Cliente no encontrado
            }

            // Actualización de propiedades
            borrarCliente.Identificacion = cliente.Identificacion;
            borrarCliente.NombreCompleto = cliente.NombreCompleto;
            borrarCliente.Direccion = cliente.Direccion;
            borrarCliente.Telefono = cliente.Telefono;
            borrarCliente.Email = cliente.Email;

            await _context.SaveChangesAsync();
            return true;
        }

        // --- DELETE ---
        public async Task<bool> DeleteClienteAsync(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
            {
                return false; // Cliente no encontrado
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
