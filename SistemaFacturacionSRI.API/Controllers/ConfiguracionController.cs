using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.API.DTOs.Configuracion;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Helpers;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using System.Security.Cryptography.X509Certificates;
using SistemaFacturacionSRI.Domain.DTOs.Configuracion;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConfiguracionController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<ConfiguracionInicioDto>> GetConfiguracion()
        {
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            if (config == null) return NotFound();

            return new ConfiguracionInicioDto
            {
                Ruc = config.Ruc,
                RazonSocial = config.RazonSocial,
                NombreComercial = config.NombreComercial,
                DireccionMatriz = config.DireccionMatriz,
                DireccionEstablecimiento = config.DireccionEstablecimiento,
                CodigoEstablecimiento = config.CodigoEstablecimiento,
                CodigoPuntoEmision = config.CodigoPuntoEmision,
                ObligadoContabilidad = config.ObligadoContabilidad,
                ContribuyenteEspecial = config.ContribuyenteEspecial,
                NombreArchivoFirma = config.NombreArchivo,
                ClaveFirma = null
            };
        }

        [HttpPost]
        public async Task<IActionResult> GuardarConfiguracion([FromForm] FirmaUploadDTO dto)
        {
            try
            {
                // 1. Validar archivo de firma (Si se subió uno nuevo)
                byte[]? firmaBytes = null;

                if (dto.ArchivoFirma != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        await dto.ArchivoFirma.CopyToAsync(ms);
                        firmaBytes = ms.ToArray();
                    }

                    // Validar contraseña SOLO si el usuario ingresó una
                    if (!string.IsNullOrEmpty(dto.ClaveFirma))
                    {
                        try
                        {
                            var cert = new X509Certificate2(
                                firmaBytes,
                                dto.ClaveFirma,
                                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                        }
                        catch (System.Security.Cryptography.CryptographicException)
                        {
                            return BadRequest("La contraseña de la firma electrónica es INCORRECTA.");
                        }
                        catch (Exception ex)
                        {
                            return BadRequest($"El archivo de firma parece dañado: {ex.Message}");
                        }
                    }
                }

                // 2. Buscar o Crear Configuración
                var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
                bool esNuevaConfig = (config == null);

                if (esNuevaConfig) config = new ConfiguracionSRI();

                // 3. Actualizar Datos Básicos
                config.Ruc = dto.Ruc;
                config.RazonSocial = dto.RazonSocial;
                config.NombreComercial = dto.NombreComercial;
                config.DireccionMatriz = dto.DireccionMatriz;
                config.DireccionEstablecimiento = dto.DireccionEstablecimiento;
                config.CodigoEstablecimiento = dto.CodigoEstablecimiento;
                config.CodigoPuntoEmision = dto.CodigoPuntoEmision;
                config.ObligadoContabilidad = dto.ObligadoContabilidad;
                config.ContribuyenteEspecial = dto.ContribuyenteEspecial;

                // 4. Lógica de Clave
                if (!string.IsNullOrEmpty(dto.ClaveFirma))
                {
                    config.ClaveFirma = EncryptionHelper.Encrypt(dto.ClaveFirma);
                }
                else if (esNuevaConfig)
                {
                    return BadRequest("La contraseña de la firma es obligatoria la primera vez.");
                }

                // 5. Lógica de Archivo
                if (firmaBytes != null)
                {
                    config.FirmaElectronica = firmaBytes;
                    config.NombreArchivo = dto.ArchivoFirma!.FileName;
                    config.FechaSubida = DateTime.Now;
                }
                else if (esNuevaConfig)
                {
                    return BadRequest("Debe subir el archivo de firma electrónica (.p12) la primera vez.");
                }

                if (esNuevaConfig) _context.ConfiguracionesSRI.Add(config);
                else _context.ConfiguracionesSRI.Update(config);

                await _context.SaveChangesAsync();

                return Ok(new { message = "Configuración guardada correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}