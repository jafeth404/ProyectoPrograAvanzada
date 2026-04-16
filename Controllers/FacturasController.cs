using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.Entities;
using proyectoprogra.Services;
using System.Security.Claims;

public class FacturasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FacturaPdfService _pdfService;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;

    public FacturasController(
        ApplicationDbContext context,
        FacturaPdfService pdfService,
        IEmailSender emailSender,
        UserManager<ApplicationUser> userManager)
    {
        _context    = context;
        _pdfService = pdfService;
        _emailSender = emailSender;
        _userManager = userManager;
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

        // Pre-calculate auto values so the view can display them
        decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        bool esMesa = pedido.MesaId.HasValue;

        ViewBag.EsMesa = esMesa;
        ViewBag.PropinaAuto = esMesa ? Math.Round(subtotal * 0.10m, 2) : 0m;
        ViewBag.CostoEmpaqueAuto = !esMesa
            ? pedido.PedidoDetalles
                .Where(d => d.Producto!.RequiereEmpaque)
                .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
            : 0m;

        // Current user's available credit
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
        {
            var usuario = await _userManager.FindByIdAsync(userId);
            ViewBag.DineroDisponible = usuario?.DineroDisponible ?? 0m;
        }
        else
        {
            ViewBag.DineroDisponible = 0m;
        }

        return View(pedido);
    }

    // 🔥 CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int pedidoId, decimal? costoDelivery, string? emailCliente)
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

        // ── Stock validation ──────────────────────────────────────────────────
        foreach (var detalle in pedido.PedidoDetalles)
        {
            if (detalle.Producto == null) continue;
            if (detalle.Producto.Stock < detalle.Cantidad)
            {
                ViewBag.Pedidos = new SelectList(await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);
                ModelState.AddModelError("",
                    $"Stock insuficiente para \"{detalle.Producto.Nombre}\". " +
                    $"Disponible: {detalle.Producto.Stock}, Requerido: {detalle.Cantidad}.");
                return View(pedido);
            }
        }

        // ── Auto-calculate totals ─────────────────────────────────────────────
        decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        decimal iva      = subtotal * 0.13m;
        bool esMesa      = pedido.MesaId.HasValue;

        // 10% auto-tip for dine-in orders
        decimal propina = esMesa ? Math.Round(subtotal * 0.10m, 2) : 0m;

        // Packaging cost for takeout: sum per-product CostoEmpaque × Cantidad
        decimal empaque = !esMesa
            ? pedido.PedidoDetalles
                .Where(d => d.Producto!.RequiereEmpaque)
                .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
            : 0m;

        decimal delivery = costoDelivery ?? 0m;
        decimal total    = subtotal + iva + propina + empaque + delivery;

        // ── Current user ──────────────────────────────────────────────────────
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ApplicationUser? usuario = userId != null
            ? await _userManager.FindByIdAsync(userId)
            : null;

        // ── Apply DineroDisponible credit ─────────────────────────────────────
        decimal creditoAplicado = 0m;
        if (usuario != null && usuario.DineroDisponible > 0)
        {
            creditoAplicado            = Math.Min(usuario.DineroDisponible, total);
            total                     -= creditoAplicado;
            usuario.DineroDisponible  -= creditoAplicado;
        }

        // ── Persist factura ───────────────────────────────────────────────────
        var factura = new Factura
        {
            NumeroFactura  = $"FAC-{DateTime.Now:yyyyMMddHHmmss}",
            PedidoId       = pedidoId,
            Fecha          = DateTime.Now,
            Subtotal       = subtotal,
            Iva            = iva,
            Propina        = propina,
            CostoEmpaque   = empaque,
            CostoDelivery  = delivery,
            CreditoAplicado = creditoAplicado,
            Total          = total,
            UsuarioId      = userId
        };

        _context.Facturas.Add(factura);
        await _context.SaveChangesAsync();

        foreach (var d in pedido.PedidoDetalles)
        {
            _context.FacturaDetalles.Add(new FacturaDetalle
            {
                FacturaId      = factura.FacturaId,
                ProductoId     = d.ProductoId,
                Cantidad       = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                TotalLinea     = d.Cantidad * d.PrecioUnitario
            });
        }

        // ── Deduct stock ──────────────────────────────────────────────────────
        foreach (var detalle in pedido.PedidoDetalles)
        {
            if (detalle.Producto == null) continue;
            detalle.Producto.Stock -= detalle.Cantidad;
        }

        // ── Persist credit change on user ─────────────────────────────────────
        if (usuario != null && creditoAplicado > 0)
            await _userManager.UpdateAsync(usuario);

        await _context.SaveChangesAsync();

        // ── Email ─────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(emailCliente))
        {
            var html = BuildFacturaEmail(factura, emailCliente.Trim());
            await _emailSender.SendEmailAsync(
                emailCliente.Trim(),
                $"Ha recibido una Factura Electrónica – {factura.NumeroFactura}",
                html);
        }

        TempData["Success"] = $"Factura {factura.NumeroFactura} creada correctamente." +
            (creditoAplicado > 0 ? $" Se aplicó ₡{creditoAplicado:N2} de crédito." : "");
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

        facturaDb.Propina       = factura.Propina ?? 0;
        facturaDb.CostoEmpaque  = factura.CostoEmpaque ?? 0;
        facturaDb.CostoDelivery = factura.CostoDelivery ?? 0;
        facturaDb.Total         = facturaDb.Subtotal + facturaDb.Iva +
                                  (facturaDb.Propina      ?? 0) +
                                  (facturaDb.CostoEmpaque ?? 0) +
                                  (facturaDb.CostoDelivery ?? 0) -
                                  facturaDb.CreditoAplicado;

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

    // ── EMAIL ────────────────────────────────────────────────────────────────
    private static string BuildFacturaEmail(Factura factura, string emailCliente)
    {
        var nombre = emailCliente;
        var fecha  = factura.Fecha.ToString("dd-MM-yyyy");
        var total  = factura.Total.ToString("N2");

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#000000;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#000000;padding:30px 0;">
                <tr><td align="center">
                  <table width="580" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 4px 16px rgba(0,0,0,.5);">

                    <!-- HEADER -->
                    <tr>
                      <td style="background:#013f22;padding:36px 40px;text-align:center;">
                        <div style="display:inline-block;background:#69ff93;border-radius:50%;width:72px;height:72px;line-height:72px;margin-bottom:16px;">
                          <span style="font-size:36px;">🧾</span>
                        </div>
                        <h1 style="color:#ffffff;font-size:20px;margin:0;line-height:1.4;">
                          Ha recibido una factura<br>o documento tributario<br>electrónico
                        </h1>
                        <p style="color:#69ff93;font-size:22px;margin:12px 0 0;letter-spacing:4px;">»»»</p>
                      </td>
                    </tr>

                    <!-- BODY -->
                    <tr>
                      <td style="background:#ffffff;padding:36px 40px;">
                        <p style="margin:0 0 20px;font-size:15px;color:#000000;">
                          <strong>Estimado(a):</strong> {nombre}
                        </p>
                        <p style="margin:0 0 24px;font-size:15px;color:#013f22;">
                          Adjunto encontrará el Documento Tributario Electrónico:
                        </p>

                        <table width="100%" cellpadding="8" cellspacing="0" style="font-size:14px;color:#000000;border-top:2px solid #69ff93;">
                          <tr style="border-bottom:1px solid #e6e6e6;">
                            <td style="width:140px;color:#04a56a;padding:10px 0;"><strong>Emitido por:</strong></td>
                            <td style="padding:10px 0;">RESTAURANTE S.A.</td>
                          </tr>
                          <tr style="border-bottom:1px solid #e6e6e6;">
                            <td style="color:#04a56a;padding:10px 0;"><strong>Tipo:</strong></td>
                            <td style="padding:10px 0;">Factura Electrónica</td>
                          </tr>
                          <tr style="border-bottom:1px solid #e6e6e6;">
                            <td style="color:#04a56a;padding:10px 0;"><strong>Identificador:</strong></td>
                            <td style="padding:10px 0;word-break:break-all;">{factura.NumeroFactura}</td>
                          </tr>
                          <tr style="border-bottom:1px solid #e6e6e6;">
                            <td style="color:#04a56a;padding:10px 0;"><strong>Fecha:</strong></td>
                            <td style="padding:10px 0;">{fecha}</td>
                          </tr>
                          <tr>
                            <td style="color:#04a56a;padding:10px 0;"><strong>Total:</strong></td>
                            <td style="padding:10px 0;font-size:16px;font-weight:bold;color:#013f22;">₡{total}</td>
                          </tr>
                        </table>
                      </td>
                    </tr>

                    <!-- FOOTER -->
                    <tr>
                      <td style="background:#000000;padding:24px 40px;text-align:center;">
                        <p style="margin:0 0 8px;font-size:14px;color:#69ff93;">Para nosotros es un placer servirle</p>
                        <p style="margin:0;font-size:12px;color:#e6e6e6;">Documento tributario electrónico generado por RestauranteApp</p>
                      </td>
                    </tr>

                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    // 🔥🔥🔥 PDF
    public async Task<IActionResult> Pdf(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .Include(f => f.FacturaDetalles)
                .ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null)
            return NotFound();

        var pdf = _pdfService.Generate(factura);
        return File(pdf, "application/pdf", $"{factura.NumeroFactura}.pdf");
    }
}
