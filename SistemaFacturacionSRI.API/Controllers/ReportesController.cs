using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaFacturacionSRI.Domain.DTOs.Reportes;
using SistemaFacturacionSRI.Infrastructure.Persistence;

namespace SistemaFacturacionSRI.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        // 1. VENTAS DE LOS ÚLTIMOS 30 DÍAS
        [HttpGet("ventas-mensuales")]
        public async Task<ActionResult<List<ReporteVentasDiariasDto>>> GetVentasMensuales()
        {
            var fechaInicio = DateTime.Today.AddDays(-30);

            var reporte = await _context.Facturas
                .Where(f => f.FechaEmision >= fechaInicio && f.EstadoSRI == "AUTORIZADO") // Solo ventas reales
                .GroupBy(f => f.FechaEmision.Date)
                .Select(g => new ReporteVentasDiariasDto
                {
                    Fecha = g.Key,
                    TotalVendido = g.Sum(f => f.Total),
                    CantidadFacturas = g.Count()
                })
                .OrderBy(r => r.Fecha)
                .ToListAsync();

            return Ok(reporte);
        }

        // 2. TOP 5 PRODUCTOS MÁS VENDIDOS
        [HttpGet("top-productos")]
        public async Task<ActionResult<List<ReporteTopProductoDto>>> GetTopProductos()
        {
            var reporte = await _context.DetallesFactura
                .Include(d => d.Producto)
                .GroupBy(d => d.Producto.NombreGenerico)
                .Select(g => new ReporteTopProductoDto
                {
                    Producto = g.Key,
                    CantidadTotal = g.Sum(d => d.Cantidad),
                    MontoTotal = g.Sum(d => d.Subtotal) // Corregido aquí
                })
                .OrderByDescending(r => r.CantidadTotal)
                .Take(5)
                .ToListAsync();

            return Ok(reporte);
        }

        // 3. SEMÁFORO DE CADUCIDAD (Lotes por vencer en 60 días)
        [HttpGet("proximos-a-caducar")]
        public async Task<ActionResult<List<ReporteCaducidadDto>>> GetProximosCaducar()
        {
            var fechaLimite = DateTime.Today.AddDays(60);

            // PASO 1: Consulta simple a la BD y trae los datos a la memoria del servidor (LINQ to Entities)
            var lotesEnMemoria = await _context.LotesProducto
                .Include(l => l.Producto)
                // Filtrar solo los lotes relevantes en el servidor (esto es traducible a SQL)
                .Where(l => l.CantidadActual > 0
                            && l.FechaCaducidad.HasValue
                            && l.FechaCaducidad.Value <= fechaLimite)
                .ToListAsync(); // <--- ¡CRÍTICO! Aquí se ejecuta la consulta SQL.

            // PASO 2: Proyección y Ordenamiento en la memoria de la aplicación (LINQ to Objects)
            var reporte = lotesEnMemoria
                .Select(l => new ReporteCaducidadDto
                {
                    Producto = l.Producto.NombreGenerico,
                    Lote = l.Id.ToString(), // Usar el ID como identificador de lote
                    FechaCaducidad = l.FechaCaducidad!.Value, // Sabemos que tiene valor por el filtro anterior
                                                              // Cálculo complejo de días restantes (se ejecuta en C#)
                    DiasRestantes = (l.FechaCaducidad.Value - DateTime.Today).Days,
                    Stock = l.CantidadActual
                })
                .OrderBy(r => r.DiasRestantes) // Ordenar por días restantes en C#
                .ToList();

            return Ok(reporte);
        }



        // 4. REPORTE DE GANANCIAS POR PERÍODO (Asume CostoUnitario existe en DetalleFactura)
        [HttpGet("ganancias-mensuales")]
        public async Task<ActionResult<List<ReporteGananciaDiariaDto>>> GetGananciasMensuales()
        {
            var fechaInicio = DateTime.Today.AddDays(-30);

            // Consulta para calcular la utilidad bruta
            var reporte = await _context.DetallesFactura
                .Include(d => d.Factura)
                // Filtramos solo por ventas autorizadas en el último mes
                .Where(d => d.Factura.EstadoSRI == "AUTORIZADO" && d.Factura.FechaEmision >= fechaInicio)
                .GroupBy(d => d.Factura.FechaEmision.Date)
                .Select(g => new ReporteGananciaDiariaDto
                {
                    Fecha = g.Key,
                    // Suma de precios totales (Ingreso)
                    VentaBruta = g.Sum(d => d.Subtotal),
                    // Suma de Costo Unitario * Cantidad (Costo de Venta)
                    CostoTotal = g.Sum(d => d.CostoUnitario * d.Cantidad),
                })
                .OrderBy(r => r.Fecha)
                .ToListAsync();

            return Ok(reporte);
        }

    }
}