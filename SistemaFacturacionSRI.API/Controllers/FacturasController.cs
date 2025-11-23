using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacturasController : ControllerBase
    {
        private readonly IFacturaService _facturaService;

        public FacturasController(IFacturaService facturaService)
        {
            _facturaService = facturaService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearFactura([FromBody] CreateFacturaDto dto)
        {
            try
            {
                var factura = await _facturaService.CrearFacturaAsync(dto);
                // Retornamos Ok con el objeto creado.
                // A futuro, aquí podrías llamar al servicio del SRI inmediatamente si fuera síncrono.
                return Ok(factura);
            }
            catch (Exception ex)
            {
                // Retorna 400 Bad Request con el mensaje de error (ej: "Stock insuficiente")
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}