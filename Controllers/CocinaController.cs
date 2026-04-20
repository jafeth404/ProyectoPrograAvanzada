using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Cocina,Administrador")]
    public class CocinaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CocinaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cocina/Index
        // Shows all pedidos that have at least one item with Estado != "Listo"
        public async Task<IActionResult> Index()
        {
            var pedidos = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.PedidoDetalles.Any(d => d.Estado != "Listo"))
                .OrderBy(p => p.Fecha)
                .ToListAsync();

            return View(pedidos);
        }

        // POST: Cocina/ActualizarEstado
        // AJAX endpoint — advances a single PedidoDetalle to the next estado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstado([FromBody] ActualizarEstadoRequest request)
        {
            if (request == null || request.DetalleId <= 0)
                return BadRequest(new { success = false, message = "Solicitud inválida." });

            var detalle = await _context.PedidoDetalles.FindAsync(request.DetalleId);

            if (detalle == null)
                return NotFound(new { success = false, message = "Ítem no encontrado." });

            var siguiente = SiguienteEstado(detalle.Estado);

            if (siguiente == null)
                return BadRequest(new { success = false, message = "El ítem ya está en el estado final." });

            detalle.Estado = siguiente;
            await _context.SaveChangesAsync();

            // Check if the parent pedido now has all items "Listo"
            var pedidoCompleto = !await _context.PedidoDetalles
                .Where(d => d.PedidoId == detalle.PedidoId && d.DetalleId != detalle.DetalleId)
                .AnyAsync(d => d.Estado != "Listo");

            return Ok(new
            {
                success = true,
                nuevoEstado = siguiente,
                pedidoId = detalle.PedidoId,
                pedidoCompleto
            });
        }

        private static string? SiguienteEstado(string? estadoActual) => estadoActual switch
        {
            "Pendiente"       => "En preparación",
            "En preparación"  => "Listo",
            _                 => null
        };
    }

    public class ActualizarEstadoRequest
    {
        public int DetalleId { get; set; }
    }
}
