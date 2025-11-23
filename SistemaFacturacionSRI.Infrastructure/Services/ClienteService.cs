using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Clientes;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Domain.Validations; 
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

        // --- CREATE ---
        public async Task<Cliente> AddClienteAsync(CreateCliente cliente)
        {
            // 1. VALIDACIONES LÓGICAS (SRI)
            // Validamos Identificación (RUC/Cédula/Pasaporte)
            var valId = ValidadorCliente.ValidarIdentificacion(cliente.TipoIdentificacion, cliente.Identificacion);
            if (!valId.EsValido)
                throw new ArgumentException(valId.Mensaje);

            // Validamos Correo
            var valCorreo = ValidadorCliente.ValidarCorreo(cliente.Email);
            if (!valCorreo.EsValido)
                throw new ArgumentException(valCorreo.Mensaje);

            // Validamos Dirección
            var valDir = ValidadorCliente.ValidarDireccion(cliente.Direccion);
            if (!valDir.EsValido)
                throw new ArgumentException(valDir.Mensaje);

            // 2. VALIDAR DUPLICADOS EN BD
            // Verificamos si ya existe alguien con esa identificación
            bool existe = await _context.Clientes.AnyAsync(c => c.Identificacion == cliente.Identificacion);
            if (existe)
                throw new ArgumentException($"El cliente con identificación {cliente.Identificacion} ya está registrado.");

            // 3. MAPEO (DTO -> Entidad)
            var nuevocliente = new Cliente
            {
                TipoIdentificacion = cliente.TipoIdentificacion, // Nuevo campo
                Identificacion = cliente.Identificacion,
                NombreCompleto = cliente.NombreCompleto.ToUpper(), // SRI prefiere mayúsculas
                Direccion = cliente.Direccion.Replace("\n", " ").Trim(), // Limpiamos saltos de línea
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                Pais = cliente.Pais ?? "Ecuador" // Valor por defecto si es nulo
            };

            _context.Clientes.Add(nuevocliente);
            await _context.SaveChangesAsync();

            return nuevocliente;
        }

        // --- READ ALL ---
        public async Task<List<Cliente>> GetAllClientesAsync()
        {
            // Nota: Aquí podrías incluir también TipoIdentificacion y Pais en el Select si los necesitas en la vista
            return await _context.Clientes
                .Select(c => new Cliente
                {
                    Id = c.Id,
                    TipoIdentificacion = c.TipoIdentificacion, // <--- Agregado
                    Identificacion = c.Identificacion,
                    NombreCompleto = c.NombreCompleto,
                    Direccion = c.Direccion,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    Pais = c.Pais // <--- Agregado
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
                    TipoIdentificacion = c.TipoIdentificacion, // <--- Agregado
                    Identificacion = c.Identificacion,
                    NombreCompleto = c.NombreCompleto,
                    Direccion = c.Direccion,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    Pais = c.Pais // <--- Agregado
                })
                .FirstOrDefaultAsync();
        }

        // --- UPDATE ---
        public async Task<bool> UpdateClienteAsync(UpdateCliente cliente)
        {
            var clienteDb = await _context.Clientes.FindAsync(cliente.Id);

            if (clienteDb == null) return false;

            // 1. VALIDACIONES LÓGICAS
            var valId = ValidadorCliente.ValidarIdentificacion(cliente.TipoIdentificacion, cliente.Identificacion);
            if (!valId.EsValido) throw new ArgumentException(valId.Mensaje);

            var valCorreo = ValidadorCliente.ValidarCorreo(cliente.Email);
            if (!valCorreo.EsValido) throw new ArgumentException(valCorreo.Mensaje);

            // 2. VALIDAR DUPLICADOS (Excluyendo al propio usuario)
            bool duplicado = await _context.Clientes
                .AnyAsync(c => c.Identificacion == cliente.Identificacion && c.Id != cliente.Id);

            if (duplicado)
                throw new ArgumentException($"La identificación {cliente.Identificacion} ya pertenece a otro cliente.");

            // 3. ACTUALIZAR CAMPOS
            clienteDb.TipoIdentificacion = cliente.TipoIdentificacion;
            clienteDb.Identificacion = cliente.Identificacion;
            clienteDb.NombreCompleto = cliente.NombreCompleto.ToUpper();
            clienteDb.Direccion = cliente.Direccion.Replace("\n", " ").Trim();
            clienteDb.Telefono = cliente.Telefono;
            clienteDb.Email = cliente.Email;
            clienteDb.Pais = cliente.Pais ?? "Ecuador";

            await _context.SaveChangesAsync();
            return true;
        }

        // --- DELETE ---
        public async Task<bool> DeleteClienteAsync(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null) return false;

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
