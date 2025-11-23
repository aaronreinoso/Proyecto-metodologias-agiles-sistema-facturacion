using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.API.DTOs.Configuracion;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Helpers;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System.Security.Cryptography.X509Certificates;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Administrador")] // Solo el admin puede tocar esto
    public class ConfiguracionController : ControllerBase
    {
        private readonly AppDbContext _context;

        // ✅ CONSTRUCTOR CORREGIDO: Solo inyectamos el DbContext (Servicios)
        public ConfiguracionController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Configuracion/guardar
        [HttpPost("guardar")]
        public async Task<IActionResult> Guardar([FromForm] FirmaUploadDto dto)
        {
            // 1. Obtener la configuración existente o crear una nueva
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new ConfiguracionSRI();
                _context.ConfiguracionesSRI.Add(config);
            }

            // 2. Actualizar los datos de texto (Información de la empresa)
            config.Ruc = dto.Ruc;
            config.RazonSocial = dto.RazonSocial;
            config.NombreComercial = dto.NombreComercial;
            config.DireccionMatriz = dto.DireccionMatriz;

            // Si la dirección del establecimiento viene vacía, usamos la matriz
            config.DireccionEstablecimiento = !string.IsNullOrEmpty(dto.DireccionEstablecimiento)
                                              ? dto.DireccionEstablecimiento
                                              : dto.DireccionMatriz;

            config.CodigoEstablecimiento = dto.CodigoEstablecimiento;
            config.CodigoPuntoEmision = dto.CodigoPuntoEmision;
            config.ObligadoContabilidad = dto.ObligadoContabilidad;

            // Si agregaste el campo "ContribuyenteEspecial" a tu entidad y DTO, descomenta esto:
            // config.ContribuyenteEspecial = dto.ContribuyenteEspecial;

            // 3. Procesar Firma Electrónica (Solo si el usuario subió un archivo nuevo)
            if (dto.ArchivoP12 != null && dto.ArchivoP12.Length > 0)
            {
                // Si sube archivo, la clave es OBLIGATORIA
                if (string.IsNullOrEmpty(dto.ClaveFirma))
                {
                    return BadRequest("Para actualizar la firma, es obligatorio ingresar la contraseña.");
                }

                using var ms = new MemoryStream();
                await dto.ArchivoP12.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                // VALIDACIÓN DE SEGURIDAD: Intentar abrir el certificado
                try
                {
                    // Esto lanzará una excepción si la clave es incorrecta o el archivo está corrupto
                    // Usamos X509KeyStorageFlags.MachineKeySet para evitar problemas de permisos en algunos servidores
                    new X509Certificate2(fileBytes, dto.ClaveFirma, X509KeyStorageFlags.MachineKeySet);

                    // Si pasamos la prueba, guardamos los datos sensibles
                    config.FirmaElectronica = fileBytes;
                    config.ClaveFirma = EncryptionHelper.Encrypt(dto.ClaveFirma); // Guardamos encriptada
                    config.NombreArchivo = dto.ArchivoP12.FileName;
                    config.FechaSubida = DateTime.Now;
                }
                catch (Exception)
                {
                    return BadRequest("La contraseña de la firma es incorrecta o el archivo .p12 no es válido.");
                }
            }

            // 4. Guardar cambios en la Base de Datos
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Configuración guardada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al guardar: {ex.Message}");
            }
        }

        // GET: api/Configuracion
        [HttpGet]
        public async Task<ActionResult<ConfiguracionSRI>> GetConfiguracion()
        {
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();

            if (config == null)
            {
                // Retornamos un objeto vacío para que el formulario no falle al cargar
                return Ok(new ConfiguracionSRI());
            }

            // 🛡️ SEGURIDAD: Limpiamos datos sensibles antes de enviarlos al Frontend
            config.ClaveFirma = "";
            config.FirmaElectronica = null;

            return Ok(config);
        }
    }
}