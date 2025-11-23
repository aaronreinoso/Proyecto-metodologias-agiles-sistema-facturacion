using Microsoft.AspNetCore.Http; // Necesario para IFormFile
using SistemaFacturacionSRI.Domain.DTOs.Configuracion; // Importamos el DTO base

namespace SistemaFacturacionSRI.API.DTOs.Configuracion
{
    public class FirmaUploadDto : ConfiguracionInicioDto
    {
        public IFormFile? ArchivoP12 { get; set; }
    }
}