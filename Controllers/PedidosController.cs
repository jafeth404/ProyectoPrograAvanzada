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
        public async Task<IActionResult> Create(int MesaId, string TipoPedido, string ItemsJson)
        {
            if (string.IsNullOrEmpty(TipoPedido))
            {
                ModelState.AddModelError("", "Debe seleccionar el tipo de pedido");
            }

            var items = JsonSerializer.Deserialize<List<ItemPedidoVM>>(ItemsJson);

            if (items == null || !items.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto");
            }

            if (!ModelState.IsValid)
            {
                ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId", MesaId);
                ViewData["Productos"] = _context.Productos.ToList();
                return View();
            }

            // 🔥 Crear pedido
            var pedido = new Pedido
            {
                MesaId = MesaId,
                Fecha= DateTime.Now,
                TipoPedido = TipoPedido
            };

            _context.Add(pedido);
            await _context.SaveChangesAsync();

            // 🔥 Crear detalles automáticamente
            foreach (var item in items)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);

                var detalle = new PedidoDetalle
                {
                    PedidoId = pedido.PedidoId,
                    ProductoId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = producto!.Precio,
                    Estado = "Pendiente"
                };

                _context.Add(detalle);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Pedidos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos.FindAsync(id);

            if (pedido == null)
                return NotFound();

            ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId", pedido.MesaId);

            return View(pedido);
        }

        // POST: Pedidos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PedidoId,MesaId")] Pedido pedido)
        {
            if (id != pedido.PedidoId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pedido);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Pedidos.Any(e => e.PedidoId == pedido.PedidoId))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["MesaId"] = new SelectList(_context.Mesas, "MesaId", "MesaId", pedido.MesaId);
            return View(pedido);
        }

        // GET: Pedidos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Mesa)
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
                .Include(p => p.PedidoDetalles)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

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