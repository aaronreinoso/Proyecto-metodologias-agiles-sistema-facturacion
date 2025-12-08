using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.Facturas;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacturasController : ControllerBase
    {
        private readonly IFacturaService _facturaService;

        // =========================================================
        //  INYECCIONES NECESARIAS
        // =========================================================
        private readonly AppDbContext _context;     // Para consulta directa
        private readonly IPdfService _pdfService;   // Para generar PDF
        private readonly IEmailService _emailService; // Para reenviar email

        public FacturasController(
            IFacturaService facturaService,
            AppDbContext context,          // <<< requerido para PDF
            IPdfService pdfService,        // <<< requerido para PDF
            IEmailService emailService     // <<< para reenviar email
        )
        {
            _facturaService = facturaService;
            _context = context;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // =========================================================
        //  CREAR FACTURA
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
        //  HISTORIAL DE FACTURAS
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
        //   DESCARGAR PDF  (CAMBIO FINAL: constraint :int + anti-cache)
        // =========================================================
        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> DescargarPdf(int id)
        {
            // 1. Cargar factura completa
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (factura == null)
                return NotFound("Factura no encontrada.");

            // 2. Configuración de empresa para encabezado del PDF
            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();

            // 3. Generar PDF
            var pdfBytes = _pdfService.GenerarFacturaPdf(factura, config);

            // 4. Evitar problemas de caché en navegadores
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

            // 5. Retornar PDF descargable
            return File(pdfBytes, "application/pdf", $"Factura-{id}.pdf");
        }

        // =========================================================
        //  REENVIAR FACTURA POR EMAIL
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

                if (factura == null)
                    return NotFound("Factura no encontrada.");

                if (string.IsNullOrEmpty(factura.Cliente?.Email))
                    return BadRequest("El cliente no tiene correo registrado.");

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
