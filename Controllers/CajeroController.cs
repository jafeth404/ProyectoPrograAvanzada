using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.Entities;
using proyectoprogra.Services;
using System.Security.Claims;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Cajero,Administrador")]
    public class CajeroController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FacturaPdfService    _pdfService;
        private readonly IEmailSender         _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;

        public CajeroController(
            ApplicationDbContext context,
            FacturaPdfService    pdfService,
            IEmailSender         emailSender,
            UserManager<ApplicationUser> userManager)
        {
            _context     = context;
            _pdfService  = pdfService;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        // GET: Cajero/Index
        // Shows pedidos where ALL items are "Listo" and no factura exists
        public async Task<IActionResult> Index()
        {
            var facturadosIds = await _context.Facturas
                .Select(f => f.PedidoId)
                .ToListAsync();

            var pedidos = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                    .ThenInclude(d => d.Producto)
                .Where(p =>
                    p.PedidoDetalles.Any() &&
                    p.PedidoDetalles.All(d => d.Estado == "Listo") &&
                    !facturadosIds.Contains(p.PedidoId))
                .OrderBy(p => p.Fecha)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: Cajero/Cobrar/5
        // Preview screen with pre-calculated totals and email input
        public async Task<IActionResult> Cobrar(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Mesa)
                .Include(p => p.PedidoDetalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null) return NotFound();

            var yaFacturado = await _context.Facturas.AnyAsync(f => f.PedidoId == id);
            if (yaFacturado)
            {
                TempData["Error"] = "Este pedido ya fue facturado.";
                return RedirectToAction(nameof(Index));
            }

            decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
            bool esDineIn = pedido.MesaId.HasValue;

            ViewBag.Subtotal = subtotal;
            ViewBag.Iva = Math.Round(subtotal * 0.13m, 2);
            ViewBag.Propina = esDineIn ? Math.Round(subtotal * 0.10m, 2) : 0m;
            ViewBag.Empaque = !esDineIn
                ? pedido.PedidoDetalles
                    .Where(d => d.Producto!.RequiereEmpaque)
                    .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
                : 0m;
            ViewBag.EsDineIn = esDineIn;

            return View(pedido);
        }

        // POST: Cajero/ProcesarCobro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarCobro(int pedidoId, decimal? costoDelivery, string? emailCliente)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoId);

            if (pedido == null) return NotFound();

            // Stock validation
            foreach (var detalle in pedido.PedidoDetalles)
            {
                if (detalle.Producto == null) continue;
                if (detalle.Producto.Stock < detalle.Cantidad)
                {
                    TempData["Error"] = $"Stock insuficiente para \"{detalle.Producto.Nombre}\". " +
                        $"Disponible: {detalle.Producto.Stock}, Requerido: {detalle.Cantidad}.";
                    return RedirectToAction(nameof(Cobrar), new { id = pedidoId });
                }
            }

            // Calculate totals
            decimal subtotal = pedido.PedidoDetalles.Sum(d => d.Cantidad * d.PrecioUnitario);
            decimal iva      = Math.Round(subtotal * 0.13m, 2);
            bool esDineIn    = pedido.MesaId.HasValue;
            decimal propina  = esDineIn ? Math.Round(subtotal * 0.10m, 2) : 0m;
            decimal empaque  = !esDineIn
                ? pedido.PedidoDetalles
                    .Where(d => d.Producto!.RequiereEmpaque)
                    .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
                : 0m;
            decimal delivery = costoDelivery ?? 0m;
            decimal total    = subtotal + iva + propina + empaque + delivery;

            // Apply DineroDisponible credit
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ApplicationUser? usuario = userId != null
                ? await _userManager.FindByIdAsync(userId)
                : null;

            decimal creditoAplicado = 0m;
            if (usuario != null && usuario.DineroDisponible > 0)
            {
                creditoAplicado           = Math.Min(usuario.DineroDisponible, total);
                total                    -= creditoAplicado;
                usuario.DineroDisponible -= creditoAplicado;
            }

            // Persist factura
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
                UsuarioId       = userId
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

            // Deduct stock
            foreach (var detalle in pedido.PedidoDetalles)
            {
                if (detalle.Producto != null)
                    detalle.Producto.Stock -= detalle.Cantidad;
            }

            if (usuario != null && creditoAplicado > 0)
                await _userManager.UpdateAsync(usuario);

            await _context.SaveChangesAsync();

            // Send email if provided
            if (!string.IsNullOrWhiteSpace(emailCliente))
            {
                var html = BuildFacturaEmail(factura, emailCliente.Trim());
                await _emailSender.SendEmailAsync(
                    emailCliente.Trim(),
                    $"Factura – {factura.NumeroFactura}",
                    html);
            }

            TempData["FacturaId"]     = factura.FacturaId;
            TempData["NumeroFactura"] = factura.NumeroFactura;
            TempData["CreditoAplicado"] = creditoAplicado > 0
                ? $"Se aplicó ₡{creditoAplicado:N2} de crédito." : null;

            return RedirectToAction(nameof(Confirmacion), new { id = factura.FacturaId });
        }

        // GET: Cajero/Confirmacion/5
        public async Task<IActionResult> Confirmacion(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Pedido)
                .Include(f => f.FacturaDetalles)
                    .ThenInclude(fd => fd.Producto)
                .FirstOrDefaultAsync(f => f.FacturaId == id);

            if (factura == null) return NotFound();

            return View(factura);
        }

        // GET: Cajero/DescargarPdf/5
        public async Task<IActionResult> DescargarPdf(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Pedido)
                .Include(f => f.FacturaDetalles)
                    .ThenInclude(fd => fd.Producto)
                .FirstOrDefaultAsync(f => f.FacturaId == id);

            if (factura == null) return NotFound();

            var pdf = _pdfService.Generate(factura);
            return File(pdf, "application/pdf", $"{factura.NumeroFactura}.pdf");
        }

        // POST: Cajero/EnviarEmailFactura   (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarEmailFactura([FromBody] EnviarEmailRequest request)
        {
            if (request == null || request.FacturaId <= 0 || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { success = false, message = "Datos inválidos." });

            var factura = await _context.Facturas
                .Include(f => f.FacturaDetalles).ThenInclude(fd => fd.Producto)
                .FirstOrDefaultAsync(f => f.FacturaId == request.FacturaId);

            if (factura == null)
                return NotFound(new { success = false, message = "Factura no encontrada." });

            var html = BuildFacturaEmail(factura, request.Email);
            await _emailSender.SendEmailAsync(
                request.Email,
                $"Factura – {factura.NumeroFactura}",
                html);

            return Ok(new { success = true });
        }

        private static string BuildFacturaEmail(Factura factura, string email)
        {
            return $"""
                <!DOCTYPE html>
                <html lang="es"><head><meta charset="utf-8"></head>
                <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:30px 0;">
                    <tr><td align="center">
                      <table width="560" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:8px;overflow:hidden;">
                        <tr>
                          <td style="background:#696cff;padding:32px 40px;text-align:center;">
                            <h1 style="color:#fff;margin:0;font-size:22px;">Factura {factura.NumeroFactura}</h1>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:32px 40px;">
                            <p style="margin:0 0 16px;">Estimado(a): <strong>{email}</strong></p>
                            <p style="margin:0 0 8px;">Fecha: {factura.Fecha:dd/MM/yyyy HH:mm}</p>
                            <p style="margin:0 0 8px;">Subtotal: ₡{factura.Subtotal:N2}</p>
                            <p style="margin:0 0 8px;">IVA (13%): ₡{factura.Iva:N2}</p>
                            {(factura.Propina > 0 ? $"<p style='margin:0 0 8px;'>Propina (10%): ₡{factura.Propina:N2}</p>" : "")}
                            <p style="margin:16px 0 0;font-size:18px;font-weight:bold;color:#696cff;">Total: ₡{factura.Total:N2}</p>
                          </td>
                        </tr>
                        <tr>
                          <td style="background:#f4f4f4;padding:16px 40px;text-align:center;font-size:12px;color:#666;">
                            RestauranteApp – Gracias por su preferencia
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                  </table>
                </body></html>
                """;
        }
    }

    public class EnviarEmailRequest
    {
        public int FacturaId { get; set; }
        public string Email { get; set; } = null!;
    }
}
