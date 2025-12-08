using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FacturasController : ControllerBase
    {
        private readonly IFacturaService _facturaService;
        private readonly AppDbContext _context;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;

        public FacturasController(
            IFacturaService facturaService,
            AppDbContext context,
            IPdfService pdfService,
            IEmailService emailService)
        {
            _facturaService = facturaService;
            _context = context;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // =========================================================
        //  1. OBTENER UNA FACTURA POR ID (FALTABA ESTE)
        // =========================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<Factura>> GetFactura(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles)
                    .ThenInclude(d => d.Producto) // Importante para ver los items en la NC
                    .ThenInclude(p => p.TarifaIva)
                .Include(f => f.NotasCredito)     // Importante para validar saldos
                .FirstOrDefaultAsync(f => f.Id == id)
                
                ;

            if (factura == null)
            {
                return NotFound("Factura no encontrada.");
            }

            return Ok(factura);
        }

        // =========================================================
        //  2. CREAR FACTURA
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> CrearFactura([FromBody] CreateFacturaDto dto)
        {
            try
            {
                var factura = await _facturaService.CrearFacturaAsync(dto);
                return Ok(factura);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // =========================================================
        //  3. HISTORIAL DE FACTURAS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetHistorial()
        {
            try
            {
                var historial = await _facturaService.ObtenerHistorialFacturasAsync();
                return Ok(historial);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno: " + ex.Message });
            }
        }

        // =========================================================
        //  4. REINTENTAR ENVÍO AL SRI (FALTABA ESTE)
        // =========================================================
        [HttpPost("{id}/reintentar-sri")]
        public async Task<IActionResult> ReintentarSri(int id)
        {
            try
            {
                await _facturaService.ReintentarFacturaAsync(id);
                return Ok(new { message = "Proceso de reintento finalizado." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message); // Mensaje simple para que Blazor lo muestre en el alert
            }
        }

        // =========================================================
        //  5. DESCARGAR PDF
        // =========================================================
        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> DescargarPdf(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (factura == null) return NotFound("Factura no encontrada.");

            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            var pdfBytes = _pdfService.GenerarFacturaPdf(factura, config);

            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            return File(pdfBytes, "application/pdf", $"Factura-{id}.pdf");
        }

        // =========================================================
        //  6. REENVIAR EMAIL
        // =========================================================
        [HttpPost("{id}/reenviar-email")]
        public async Task<IActionResult> ReenviarEmail(int id)
        {
            try
            {
                var factura = await _context.Facturas
                    .Include(f => f.Cliente)
                    .Include(f => f.Detalles).ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(f => f.Id == id);

                if (factura == null) return NotFound("Factura no encontrada.");
                if (string.IsNullOrEmpty(factura.Cliente?.Email)) return BadRequest("El cliente no tiene correo.");

                var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
                var pdfBytes = _pdfService.GenerarFacturaPdf(factura, config);
                string numFac = $"{config.CodigoEstablecimiento}-{config.CodigoPuntoEmision}-{factura.Id:D9}";

                await _emailService.EnviarFacturaAsync(
                    factura.Cliente.Email,
                    factura.XmlGenerado ?? "",
                    pdfBytes,
                    numFac
                );

                return Ok(new { message = "Correo enviado exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error enviando correo: " + ex.Message });
            }
        }
    }
}