using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.NotasCredito;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Infrastructure.Services;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotasCreditoController : ControllerBase
    {
        private readonly NotaCreditoService _notaCreditoService;
        private readonly AppDbContext _context;
        private readonly IPdfService _pdfService;

        public NotasCreditoController(
            NotaCreditoService notaCreditoService,
            AppDbContext context,
            IPdfService pdfService)
        {
            _notaCreditoService = notaCreditoService;
            _context = context;
            _pdfService = pdfService;
        }

        // POST: api/NotasCredito
        [HttpPost]
        public async Task<ActionResult<NotaCredito>> Crear([FromBody] CreateNotaCreditoDto dto)
        {
            try
            {
                // Mapear DTO a Entidades
                var items = dto.Detalles.Select(d => new DetalleNotaCredito
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad
                }).ToList();

                var nuevaNC = await _notaCreditoService.CrearNotaCreditoAsync(dto.FacturaId, dto.Motivo, items);
                return Ok(nuevaNC);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/NotasCredito/PorFactura/{facturaId}
        [HttpGet("PorFactura/{facturaId}")]
        public async Task<ActionResult<List<NotaCredito>>> GetPorFactura(int facturaId)
        {
            var ncs = await _context.NotasCredito
                .Include(n => n.Detalles).ThenInclude(d => d.Producto)
                .Where(n => n.FacturaId == facturaId)
                .OrderByDescending(n => n.FechaEmision)
                .ToListAsync();

            return Ok(ncs);
        }

        // GET: api/NotasCredito/{id}/pdf
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> DescargarPdf(int id)
        {
            var nc = await _context.NotasCredito
                .Include(n => n.Factura).ThenInclude(f => f.Cliente)
                .Include(n => n.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (nc == null) return NotFound("Nota de Crédito no encontrada.");

            var config = await _context.ConfiguracionesSRI.FirstOrDefaultAsync();
            if (config == null) return BadRequest("Configuración no encontrada.");

            // Usar el método que crearemos en el paso 4
            // Nota: Debemos hacer cast a PdfService concreto o agregar el método a la interfaz IPdfService
            // Aquí asumimos que lo agregas a la interfaz o usas el servicio concreto.
            var pdfBytes = ((SistemaFacturacionSRI.Infrastructure.Services.Pdf.PdfService)_pdfService)
                            .GenerarNotaCreditoPdf(nc, config);

            return File(pdfBytes, "application/pdf", $"NC-{nc.Id}.pdf");
        }
    }
}