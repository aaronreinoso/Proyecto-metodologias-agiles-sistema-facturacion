using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Infrastructure.Persistence;
using SistemaFacturacionSRI.Domain.Entities;

namespace SistemaFacturacionSRI.API.Controllers
{
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
