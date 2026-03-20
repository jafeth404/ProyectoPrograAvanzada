using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;
using DinkToPdf;
using DinkToPdf.Contracts;

public class FacturasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConverter _converter;

    public FacturasController(ApplicationDbContext context, IConverter converter)
    {
        _context = context;
        _converter = converter;
    }

    // 🔥 INDEX
    public async Task<IActionResult> Index()
    {
        var facturas = await _context.Facturas
            .Include(f => f.Pedido)
            .ToListAsync();

        return View(facturas);
    }

    // 🔥 DETAILS
    public async Task<IActionResult> Details(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .Include(f => f.FacturaDetalles)
            .ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null)
            return NotFound();

        return View(factura);
    }

    // 🔥 CREATE (GET)
    public async Task<IActionResult> Create(int? pedidoId)
    {
        ViewBag.Pedidos = new SelectList(
            await _context.Pedidos.ToListAsync(),
            "PedidoId",
            "PedidoId",
            pedidoId
        );

        if (pedidoId == null)
            return View(null);

        var pedido = await _context.Pedidos
            .Include(p => p.PedidoDetalles)
            .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

        if (pedido == null)
            return NotFound();

        return View(pedido);
    }

    // 🔥 CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int pedidoId, decimal? propina, decimal? costoEmpaque, decimal? costoDelivery)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.PedidoDetalles)
            .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

        if (pedido == null)
        {
            ViewBag.Pedidos = new SelectList(await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);
            ModelState.AddModelError("", "El pedido no existe.");
            return View(null);
        }

        if (!pedido.PedidoDetalles.Any())
        {
            ViewBag.Pedidos = new SelectList(await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);
            ModelState.AddModelError("", "El pedido no tiene productos.");
            return View(pedido);
        }

        var yaExiste = await _context.Facturas.AnyAsync(f => f.PedidoId == pedidoId);
        if (yaExiste)
        {
            ViewBag.Pedidos = new SelectList(await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);
            ModelState.AddModelError("", "Ese pedido ya tiene una factura.");
            return View(pedido);
        }

        decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        decimal iva = subtotal * 0.13m;
        decimal prop = propina ?? 0;
        decimal empaque = costoEmpaque ?? 0;
        decimal delivery = costoDelivery ?? 0;
        decimal total = subtotal + iva + prop + empaque + delivery;

        var factura = new Factura
        {
            NumeroFactura = $"FAC-{DateTime.Now:yyyyMMddHHmmss}",
            PedidoId = pedidoId,
            Fecha = DateTime.Now,
            Subtotal = subtotal,
            Iva = iva,
            Propina = prop,
            CostoEmpaque = empaque,
            CostoDelivery = delivery,
            Total = total,
            UsuarioId = null
        };

        _context.Facturas.Add(factura);
        await _context.SaveChangesAsync();

        foreach (var d in pedido.PedidoDetalles)
        {
            _context.FacturaDetalles.Add(new FacturaDetalle
            {
                FacturaId = factura.FacturaId,
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                TotalLinea = d.Cantidad * d.PrecioUnitario
            });
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // 🔥 EDIT
    public async Task<IActionResult> Edit(int id)
    {
        var factura = await _context.Facturas
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null)
            return NotFound();

        return View(factura);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("FacturaId,Propina,CostoEmpaque,CostoDelivery")] Factura factura)
    {
        if (id != factura.FacturaId)
            return NotFound();

        var facturaDb = await _context.Facturas.FirstOrDefaultAsync(f => f.FacturaId == id);
        if (facturaDb == null)
            return NotFound();

        facturaDb.Propina = factura.Propina ?? 0;
        facturaDb.CostoEmpaque = factura.CostoEmpaque ?? 0;
        facturaDb.CostoDelivery = factura.CostoDelivery ?? 0;
        facturaDb.Total = facturaDb.Subtotal + facturaDb.Iva +
                   (facturaDb.Propina ?? 0) +
                   (facturaDb.CostoEmpaque ?? 0) +
                   (facturaDb.CostoDelivery ?? 0);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // 🔥 DELETE
    public async Task<IActionResult> Delete(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null)
            return NotFound();

        return View(factura);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.FacturaDetalles)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura != null)
        {
            _context.FacturaDetalles.RemoveRange(factura.FacturaDetalles);
            _context.Facturas.Remove(factura);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // 🔥🔥🔥 PDF
    public async Task<IActionResult> Pdf(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.FacturaDetalles)
            .ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null)
            return NotFound();

        var html = await this.RenderViewAsync("FacturaPdf", factura);

        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = {
                PaperSize = PaperKind.A4
            },
            Objects = {
                new ObjectSettings()
                {
                    HtmlContent = html
                }
            }
        };

        var pdf = _converter.Convert(doc);

        return File(pdf, "application/pdf");
    }
}