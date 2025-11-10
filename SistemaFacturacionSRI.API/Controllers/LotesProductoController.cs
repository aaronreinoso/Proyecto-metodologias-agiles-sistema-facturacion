using Microsoft.AspNetCore.Mvc;
using SistemaFacturacionSRI.Application.Interfaces;
using SistemaFacturacionSRI.Domain.DTOs.LotesProducto;
using SistemaFacturacionSRI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaFacturacionSRI.API.Controllers
{
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
    }
}