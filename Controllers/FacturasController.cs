using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly FacturaPdfService    _pdfService;
    private readonly EmailSender          _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;

    public FacturasController(
        ApplicationDbContext context,
        FacturaPdfService pdfService,
        EmailSender emailSender,
        UserManager<ApplicationUser> userManager)
    {
        _context     = context;
        _pdfService  = pdfService;
        _emailSender = emailSender;
        _userManager = userManager;
    }

    // INDEX
    public async Task<IActionResult> Index()
    {
        var facturas = await _context.Facturas
            .Include(f => f.Pedido)
            .OrderByDescending(f => f.Fecha)
            .ToListAsync();

        return View(facturas);
    }

    // DETAILS
    public async Task<IActionResult> Details(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .Include(f => f.FacturaDetalles)
            .ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null) return NotFound();
        return View(factura);
    }

    // CREATE GET
    public async Task<IActionResult> Create(int? pedidoId)
    {
        ViewBag.Pedidos = new SelectList(
            await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);

        if (pedidoId == null) return View(null);

        var pedido = await _context.Pedidos
            .Include(p => p.PedidoDetalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

        if (pedido == null) return NotFound();

        decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        bool esMesa = pedido.MesaId.HasValue;

        // Load configured delivery charge
        var config = await _context.ConfiguracionSistema.FirstOrDefaultAsync()
                     ?? new ConfiguracionSistema();
        decimal deliveryConfig = config.TipoCargoDelivery == "Porcentaje"
            ? Math.Round(subtotal * config.CargoDelivery / 100, 2)
            : config.CargoDelivery;

        ViewBag.EsMesa           = esMesa;
        ViewBag.PropinaAuto      = esMesa ? Math.Round(subtotal * 0.10m, 2) : 0m;
        ViewBag.CostoEmpaqueAuto = !esMesa
            ? pedido.PedidoDetalles
                .Where(d => d.Producto!.RequiereEmpaque)
                .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
            : 0m;
        ViewBag.DeliveryConfig = deliveryConfig;
        ViewBag.TipoDelivery   = config.TipoCargoDelivery;
        ViewBag.CargoDeliveryRaw = config.CargoDelivery;
        ViewBag.IsAdmin        = User.IsInRole("Administrador");

        return View(pedido);
    }

    // CREATE POST — guarda como Pendiente (carrito) o Completada según botón
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int pedidoId,
        decimal? costoDelivery,
        string? emailCliente,
        string? clienteUserId,
        string accion) // "carrito" or "facturar"
    {
        var pedido = await _context.Pedidos
            .Include(p => p.PedidoDetalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

        if (pedido == null)
        {
            ViewBag.Pedidos = new SelectList(await _context.Pedidos.ToListAsync(), "PedidoId", "PedidoId", pedidoId);
            ModelState.AddModelError("", "El pedido no existe.");
            return View(null);
        }

        if (!pedido.PedidoDetalles.Any())
        {
            ModelState.AddModelError("", "El pedido no tiene productos.");
            return View(pedido);
        }

        if (await _context.Facturas.AnyAsync(f => f.PedidoId == pedidoId && f.Estado != "Cancelada"))
        {
            ModelState.AddModelError("", "Ese pedido ya tiene una factura activa.");
            return View(pedido);
        }

        bool finalizar = accion == "facturar";

        // Stock validation only when finalizing
        if (finalizar)
        {
            foreach (var det in pedido.PedidoDetalles)
            {
                if (det.Producto == null) continue;
                if (det.Producto.Stock < det.Cantidad)
                {
                    ModelState.AddModelError("",
                        $"Stock insuficiente para \"{det.Producto.Nombre}\". " +
                        $"Disponible: {det.Producto.Stock}, Requerido: {det.Cantidad}.");
                    return View(pedido);
                }
            }
        }

        decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        decimal iva      = subtotal * 0.13m;
        bool esMesa      = pedido.MesaId.HasValue;
        decimal propina  = esMesa ? Math.Round(subtotal * 0.10m, 2) : 0m;
        decimal empaque  = !esMesa
            ? pedido.PedidoDetalles
                .Where(d => d.Producto!.RequiereEmpaque)
                .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
            : 0m;
        decimal delivery = costoDelivery ?? 0m;
        decimal total    = subtotal + iva + propina + empaque + delivery;

        // Resolve client user (not the logged-in cashier)
        ApplicationUser? cliente = string.IsNullOrWhiteSpace(clienteUserId)
            ? null
            : await _userManager.FindByIdAsync(clienteUserId);

        decimal creditoAplicado = 0m;
        if (finalizar && cliente != null && cliente.DineroDisponible > 0)
        {
            creditoAplicado          = Math.Min(cliente.DineroDisponible, total);
            total                   -= creditoAplicado;
            cliente.DineroDisponible -= creditoAplicado;
        }

        var factura = new Factura
        {
            NumeroFactura   = $"FAC-{DateTime.Now:yyyyMMddHHmmss}",
            PedidoId        = pedidoId,
            Fecha           = DateTime.Now,
            Subtotal        = subtotal,
            Iva             = iva,
            Propina         = propina,
            CostoEmpaque    = empaque,
            CostoDelivery   = delivery,
            CreditoAplicado = creditoAplicado,
            Total           = total,
            UsuarioId       = cliente?.Id,
            Estado          = finalizar ? "Completada" : "Pendiente"
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

        if (finalizar)
        {
            foreach (var det in pedido.PedidoDetalles)
                if (det.Producto != null) det.Producto.Stock -= det.Cantidad;

            if (cliente != null && creditoAplicado > 0)
                await _userManager.UpdateAsync(cliente);
        }

        await _context.SaveChangesAsync();

        if (finalizar && !string.IsNullOrWhiteSpace(emailCliente))
        {
            var pdf  = _pdfService.Generate(factura);
            var html = BuildFacturaEmail(factura, emailCliente.Trim());
            await _emailSender.SendEmailWithPdfAsync(
                emailCliente.Trim(),
                $"Factura – {factura.NumeroFactura}",
                html,
                pdf,
                $"{factura.NumeroFactura}.pdf");
        }

        TempData["Success"] = finalizar
            ? $"Factura {factura.NumeroFactura} generada." + (creditoAplicado > 0 ? $" Crédito aplicado: ₡{creditoAplicado:N2}." : "")
            : $"Compra guardada en carrito como {factura.NumeroFactura}. Puede completarla o cancelarla desde el listado.";

        return RedirectToAction(nameof(Index));
    }

    // COMPLETAR carrito
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Completar(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.FacturaDetalles).ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null) return NotFound();
        if (factura.Estado != "Pendiente")
        {
            TempData["Error"] = "Esta factura no está en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        // Stock check
        foreach (var fd in factura.FacturaDetalles)
        {
            if (fd.Producto == null) continue;
            if (fd.Producto.Stock < fd.Cantidad)
            {
                TempData["Error"] = $"Stock insuficiente para \"{fd.Producto.Nombre}\".";
                return RedirectToAction(nameof(Index));
            }
        }

        // Apply DineroDisponible
        decimal creditoExtra = 0m;
        if (!string.IsNullOrEmpty(factura.UsuarioId))
        {
            var cliente = await _userManager.FindByIdAsync(factura.UsuarioId);
            if (cliente != null && cliente.DineroDisponible > 0)
            {
                creditoExtra              = Math.Min(cliente.DineroDisponible, factura.Total);
                factura.Total            -= creditoExtra;
                factura.CreditoAplicado  += creditoExtra;
                cliente.DineroDisponible -= creditoExtra;
                await _userManager.UpdateAsync(cliente);
            }
        }

        // Deduct stock
        foreach (var fd in factura.FacturaDetalles)
            if (fd.Producto != null) fd.Producto.Stock -= fd.Cantidad;

        factura.Estado = "Completada";
        await _context.SaveChangesAsync();

        // Send email with PDF
        if (!string.IsNullOrEmpty(factura.UsuarioId))
        {
            var cliente = await _userManager.FindByIdAsync(factura.UsuarioId);
            if (cliente?.Email != null)
            {
                var pdf  = _pdfService.Generate(factura);
                var html = BuildFacturaEmail(factura, cliente.Email);
                await _emailSender.SendEmailWithPdfAsync(
                    cliente.Email,
                    $"Factura – {factura.NumeroFactura}",
                    html, pdf,
                    $"{factura.NumeroFactura}.pdf");
            }
        }

        TempData["Success"] = $"Factura {factura.NumeroFactura} completada.";
        return RedirectToAction(nameof(Index));
    }

    // CANCELAR carrito
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.FacturaDetalles)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null) return NotFound();
        if (factura.Estado != "Pendiente")
        {
            TempData["Error"] = "Solo se pueden cancelar facturas en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        _context.FacturaDetalles.RemoveRange(factura.FacturaDetalles);
        _context.Facturas.Remove(factura);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Carrito cancelado y vaciado.";
        return RedirectToAction(nameof(Index));
    }

    // EDIT
    public async Task<IActionResult> Edit(int id)
    {
        var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.FacturaId == id);
        if (factura == null) return NotFound();
        return View(factura);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("FacturaId,Propina,CostoEmpaque,CostoDelivery")] Factura factura)
    {
        if (id != factura.FacturaId) return NotFound();

        var facturaDb = await _context.Facturas.FirstOrDefaultAsync(f => f.FacturaId == id);
        if (facturaDb == null) return NotFound();

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

    // DELETE
    public async Task<IActionResult> Delete(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null) return NotFound();
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

    // PDF download
    public async Task<IActionResult> Pdf(int id)
    {
        var factura = await _context.Facturas
            .Include(f => f.Pedido)
            .Include(f => f.FacturaDetalles).ThenInclude(fd => fd.Producto)
            .FirstOrDefaultAsync(f => f.FacturaId == id);

        if (factura == null) return NotFound();

        var pdf = _pdfService.Generate(factura);
        return File(pdf, "application/pdf", $"{factura.NumeroFactura}.pdf");
    }

    // ── Email helper ─────────────────────────────────────────────────────────
    private static string BuildFacturaEmail(Factura factura, string emailCliente)
    {
        var fecha = factura.Fecha.ToString("dd-MM-yyyy HH:mm");
        var total = factura.Total.ToString("N2");

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"></head>
            <body style="margin:0;padding:0;background:#000000;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#000000;padding:30px 0;">
                <tr><td align="center">
                  <table width="580" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                    <tr>
                      <td style="background:#013f22;padding:36px 40px;text-align:center;">
                        <h1 style="color:#ffffff;font-size:20px;margin:0;">Factura {factura.NumeroFactura}</h1>
                        <p style="color:#69ff93;margin:8px 0 0;font-size:14px;">RestauranteApp</p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:36px 40px;">
                        <p style="margin:0 0 16px;font-size:15px;">Estimado(a): <strong>{emailCliente}</strong></p>
                        <table width="100%" cellpadding="8" cellspacing="0" style="font-size:14px;border-top:2px solid #69ff93;">
                          <tr><td style="color:#04a56a;"><strong>Fecha:</strong></td><td>{fecha}</td></tr>
                          <tr><td style="color:#04a56a;"><strong>Subtotal:</strong></td><td>₡{factura.Subtotal:N2}</td></tr>
                          <tr><td style="color:#04a56a;"><strong>IVA (13%):</strong></td><td>₡{factura.Iva:N2}</td></tr>
                          {(factura.Propina > 0 ? $"<tr><td style='color:#04a56a;'><strong>Propina (10%):</strong></td><td>₡{factura.Propina:N2}</td></tr>" : "")}
                          {(factura.CostoEmpaque > 0 ? $"<tr><td style='color:#04a56a;'><strong>Empaque:</strong></td><td>₡{factura.CostoEmpaque:N2}</td></tr>" : "")}
                          {(factura.CostoDelivery > 0 ? $"<tr><td style='color:#04a56a;'><strong>Delivery:</strong></td><td>₡{factura.CostoDelivery:N2}</td></tr>" : "")}
                          {(factura.CreditoAplicado > 0 ? $"<tr><td style='color:#04a56a;'><strong>Crédito aplicado:</strong></td><td>− ₡{factura.CreditoAplicado:N2}</td></tr>" : "")}
                          <tr style="border-top:2px solid #013f22;">
                            <td style="color:#013f22;font-size:16px;"><strong>TOTAL:</strong></td>
                            <td style="color:#013f22;font-size:16px;font-weight:bold;">₡{total}</td>
                          </tr>
                        </table>
                        <p style="margin:24px 0 0;font-size:13px;color:#666;">El PDF de su factura se adjunta a este correo.</p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#000000;padding:20px 40px;text-align:center;">
                        <p style="margin:0;font-size:12px;color:#e6e6e6;">RestauranteApp — Gracias por su preferencia</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
