using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.LotesProducto;
using SistemaFacturacionSRI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LotesController : ControllerBase
    {
        private readonly ILoteProductoService _loteService;

        public LotesController(ILoteProductoService loteService)
        {
            _loteService = loteService;
        }

        /// <summary>
        /// Registra un nuevo lote de producto. (IMPLEMENTACIÓN DE HU-006)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<LoteProducto>> PostLote(CreateLoteProducto loteDto)
        {
            try
            {
                var nuevoLote = await _loteService.AddLoteAsync(loteDto);
                // Devuelve un 201 Created con la ubicación del nuevo lote y el lote mismo.
                return CreatedAtAction(nameof(GetLote), new { id = nuevoLote.Id }, nuevoLote);
            }
            catch (InvalidOperationException ex)
            {
                // Si el servicio lanza la excepción (producto no encontrado), devuelve un error.
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LoteProducto>> GetLote(int id)
        {
            var lote = await _loteService.GetLoteByIdAsync(id);
            if (lote == null)
            {
                return NotFound();
            }
            return Ok(lote);
        }

        [HttpGet("producto/{productoId}")]
        public async Task<ActionResult<IEnumerable<LoteProducto>>> GetLotesPorProducto(int productoId)
        {
            var lotes = await _loteService.GetLotesByProductoIdAsync(productoId);
            return Ok(lotes);
        }

        /// <summary>
        /// Realiza un ajuste manual en la cantidad de un lote específico. (IMPLEMENTACIÓN DE HU-008)
        /// </summary>
        [HttpPatch("{loteId}/ajuste")]
        public async Task<IActionResult> RealizarAjuste(int loteId, [FromBody] CreateAjusteInventarioDto ajusteDto)
        {
            if (loteId != ajusteDto.LoteProductoId)
                return BadRequest("El ID del lote en la ruta no coincide con el DTO.");

            // 1. Obtener ID del usuario autenticado (Auditoría - CA-004)
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            

        

            try
            {
                var resultado = await _loteService.RealizarAjusteManualAsync(ajusteDto);
                return resultado ? NoContent() : NotFound("Lote o Producto no encontrado.");
            }
            catch (InvalidOperationException ex)
            {
                // Conflictos de negocio (ej. cantidad negativa)
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno al realizar el ajuste: " + ex.Message });
            }
        }

        /// <summary>
        /// Obtiene el historial de ajustes para un lote. (NUEVO REQUISITO DE LECTURA)
        /// </summary>
        [HttpGet("{loteId}/ajustes")]
        public async Task<ActionResult<IEnumerable<AjusteInventario>>> GetAjustesPorLote(int loteId)
        {
            var ajustes = await _loteService.GetAjustesByLoteIdAsync(loteId);
            return Ok(ajustes);
        }

    }
}