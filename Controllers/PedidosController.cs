using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;
using proyectoprogra.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace proyectoprogra.Controllers
{
    public class PedidosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PedidosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Pedidos
        public async Task<IActionResult> Index()
        {
            var pedidos = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                .ThenInclude(d => d.Producto)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: Pedidos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null)
                return NotFound();

            return View(pedido);
        }

        // GET: Pedidos/Create
        public IActionResult Create()
        {
            ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId");
            ViewData["Productos"] = _context.Productos.ToList();

            return View();
        }

        // POST: Pedidos/Create (🔥 pantalla única)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int MesaId, string TipoPedido, string Estado, string ItemsJson)
        {
            if (string.IsNullOrEmpty(TipoPedido))
            {
                ModelState.AddModelError("", "Debe seleccionar el tipo de pedido");
            }

            var items = string.IsNullOrEmpty(ItemsJson)
                ? new List<ItemPedidoVM>()
                : JsonSerializer.Deserialize<List<ItemPedidoVM>>(ItemsJson);

            // 🔥 limpiar basura
            items = items.Where(i => i.ProductoId > 0 && i.Cantidad > 0).ToList();

            if (!items.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto válido");
            }

            if (!ModelState.IsValid)
            {
                ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId", MesaId);
                ViewData["Productos"] = _context.Productos.ToList();
                return View();
            }

            var pedido = new Pedido
            {
                MesaId = MesaId,
                Fecha = DateTime.Now,
                TipoPedido = TipoPedido,
                Estado = Estado
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);

                if (producto == null)
                    continue;

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

            return RedirectToAction(nameof(Index));
        }
        // GET: Pedidos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null)
                return NotFound();

            ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId", pedido.MesaId);

            // 🔥 ESTE ERA EL PROBLEMA
            ViewData["Productos"] = _context.Productos.ToList();

            return View(pedido);
        }

        // POST: Pedidos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Pedido pedido, string ItemsJson)
        {
            var pedidoDb = await _context.Pedidos
                .Include(p => p.PedidoDetalles)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedidoDb == null)
                return NotFound();

            pedidoDb.TipoPedido = pedido.TipoPedido;
            pedidoDb.MesaId = pedido.MesaId;
            pedidoDb.Estado = pedido.Estado;

            var items = JsonSerializer.Deserialize<List<ItemPedidoVM>>(ItemsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // 🔥 borrar detalles actuales
            _context.PedidoDetalles.RemoveRange(pedidoDb.PedidoDetalles);

            // 🔥 crear nuevos
            foreach (var item in items)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null) continue;

                _context.PedidoDetalles.Add(new PedidoDetalle
                {
                    PedidoId = id,
                    ProductoId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = producto.Precio,
                    Estado = "Pendiente"
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Pedidos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.PedidoId == id);

            if (pedido == null)
                return NotFound();

            return View(pedido);
        }

        // POST: Pedidos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.PedidoDetalles)
            .ThenInclude(d => d.Producto)
             .FirstOrDefaultAsync(m => m.PedidoId == id);

            if (pedido != null)
            {
                // 🔥 borrar detalles primero
                _context.PedidoDetalles.RemoveRange(pedido.PedidoDetalles);

                _context.Pedidos.Remove(pedido);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}

