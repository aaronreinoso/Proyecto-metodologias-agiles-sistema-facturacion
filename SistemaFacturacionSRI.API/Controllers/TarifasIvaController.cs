using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.Entities;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TarifasIvaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TarifasIvaController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/tarifasiva
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TarifaIva>>> GetTarifas()
        {
            var tarifas = await _context.TarifasIva.ToListAsync();
            return Ok(tarifas);
        }
    }
}
