using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;
using proyectoprogra.Models.ViewModels;
using System.Text.Json;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Salonero,Administrador")]
    public class SaloneroController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SaloneroController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Salonero/Index
        // Shows only dine-in pedidos (MesaId assigned)
        public async Task<IActionResult> Index()
        {
            var pedidos = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.MesaId != null)
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();

            return View(pedidos);
        }

        // POST: Salonero/EnviarACocina   (AJAX)
        // Sets all items of a pedido to Estado = "Pendiente" and marks pedido as sent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarACocina([FromBody] EnviarACocinaRequest request)
        {
            if (request == null || request.PedidoId <= 0)
                return BadRequest(new { success = false, message = "Solicitud inválida." });

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles)
                .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId);

            if (pedido == null)
                return NotFound(new { success = false, message = "Pedido no encontrado." });

            if (!pedido.PedidoDetalles.Any())
                return BadRequest(new { success = false, message = "El pedido no tiene ítems." });

            foreach (var item in pedido.PedidoDetalles)
                item.Estado = "Pendiente";

            pedido.Estado = "En Cocina";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, pedidoId = pedido.PedidoId });
        }

        // GET: Salonero/NuevoPedido
        public IActionResult NuevoPedido()
        {
            ViewData["Mesas"] = _context.Mesas.ToList();
            ViewData["Productos"] = _context.Productos.Where(p => p.Activo).ToList();
            return View();
        }

        // POST: Salonero/NuevoPedido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevoPedido(int MesaId, string ItemsJson)
        {
            var items = string.IsNullOrEmpty(ItemsJson)
                ? new List<ItemPedidoVM>()
                : JsonSerializer.Deserialize<List<ItemPedidoVM>>(ItemsJson,
                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            items = items?.Where(i => i.ProductoId > 0 && i.Cantidad > 0).ToList()
                    ?? new List<ItemPedidoVM>();

            if (MesaId <= 0)
                ModelState.AddModelError("", "Debe seleccionar una mesa.");

            if (!items.Any())
                ModelState.AddModelError("", "Debe agregar al menos un producto.");

            if (!ModelState.IsValid)
            {
                ViewData["Mesas"] = _context.Mesas.ToList();
                ViewData["Productos"] = _context.Productos.Where(p => p.Activo).ToList();
                return View();
            }

            var pedido = new Pedido
            {
                MesaId = MesaId,
                TipoPedido = "Dine-in",
                Fecha = DateTime.Now,
                Estado = "Pendiente"
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null) continue;

                _context.PedidoDetalles.Add(new PedidoDetalle
                {
                    PedidoId = pedido.PedidoId,
                    ProductoId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = producto.Precio,
                    Estado = "Pendiente"
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Pedido #{pedido.PedidoId} creado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class EnviarACocinaRequest
    {
        public int PedidoId { get; set; }
    }
}
