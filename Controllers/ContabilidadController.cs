using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.ViewModels;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Contabilidad,Administrador")]
    public class ContabilidadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ContabilidadController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Contabilidad/Index
        public async Task<IActionResult> Index(
            string? usuarioId,
            DateTime? desde,
            DateTime? hasta)
        {
            var query = _context.Facturas
                .Include(f => f.Pedido)
                .AsQueryable();

            if (!string.IsNullOrEmpty(usuarioId))
                query = query.Where(f => f.UsuarioId == usuarioId);

            if (desde.HasValue)
                query = query.Where(f => f.Fecha >= desde.Value.Date);

            if (hasta.HasValue)
                query = query.Where(f => f.Fecha < hasta.Value.Date.AddDays(1));

            var vm = new ContabilidadIndexVM
            {
                UsuarioId = usuarioId,
                Desde     = desde,
                Hasta     = hasta,
                Facturas  = await query.OrderByDescending(f => f.Fecha).ToListAsync(),
                Usuarios  = _userManager.Users.OrderBy(u => u.NombreCompleto).ToList()
            };

            return View(vm);
        }

        // POST: Contabilidad/Reversar   (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reversar([FromBody] ReversarRequest request)
        {
            if (request == null || request.FacturaId <= 0)
                return BadRequest(new { success = false, message = "Solicitud inválida." });

            var factura = await _context.Facturas
                .Include(f => f.Pedido)
                .FirstOrDefaultAsync(f => f.FacturaId == request.FacturaId);

            if (factura == null)
                return NotFound(new { success = false, message = "Factura no encontrada." });

            if (factura.Reversada)
                return BadRequest(new { success = false, message = "Esta factura ya fue reversada." });

            var horasTranscurridas = (DateTime.Now - factura.Fecha).TotalHours;
            if (horasTranscurridas > 24)
                return BadRequest(new { success = false, message = "Solo se pueden reversar facturas de menos de 24 horas." });

            string mensaje;

            if (!string.IsNullOrEmpty(factura.UsuarioId))
            {
                // Registered user: add total back to DineroDisponible
                var usuario = await _userManager.FindByIdAsync(factura.UsuarioId);
                if (usuario != null)
                {
                    // Also restore the credit that was applied in the original purchase
                    var montoTotal = factura.Total + factura.CreditoAplicado;
                    usuario.DineroDisponible += montoTotal;
                    await _userManager.UpdateAsync(usuario);
                    mensaje = $"Monto ₡{montoTotal:N2} acreditado a {usuario.Email}.";
                }
                else
                {
                    mensaje = "Devolver el dinero en efectivo al cliente (usuario no encontrado).";
                }
            }
            else
            {
                // Generic/anonymous user: instruct cashier to return cash
                mensaje = "Devolver ₡" + factura.Total.ToString("N2") + " en efectivo al cliente.";
            }

            factura.Reversada = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = mensaje });
        }
    }

    public class ReversarRequest
    {
        public int FacturaId { get; set; }
    }
}
