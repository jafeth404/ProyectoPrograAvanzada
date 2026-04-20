using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.ViewModels;
using System.Security.Claims;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Usuario")]
    public class MiCuentaController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MiCuentaController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: MiCuenta/MisDatos
        public async Task<IActionResult> MisDatos()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var vm = new MisDatosViewModel
            {
                NombreCompleto = user.NombreCompleto,
                Genero         = user.Genero,
                Email          = user.Email ?? string.Empty,
                TipoTarjeta    = user.TipoTarjeta,
                NumeroTarjeta  = null   // never pre-fill; user must re-enter to change
            };

            return View(vm);
        }

        // POST: MiCuenta/MisDatos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MisDatos(MisDatosViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.NombreCompleto = vm.NombreCompleto;
            user.Genero         = vm.Genero;
            user.TipoTarjeta    = vm.TipoTarjeta;

            // Update email / username if changed
            if (!string.Equals(user.Email, vm.Email, StringComparison.OrdinalIgnoreCase))
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, vm.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var err in setEmailResult.Errors)
                        ModelState.AddModelError("", err.Description);
                    return View(vm);
                }

                await _userManager.SetUserNameAsync(user, vm.Email);
            }

            // Update card number if a new one was provided
            if (!string.IsNullOrWhiteSpace(vm.NumeroTarjeta))
            {
                var raw = vm.NumeroTarjeta.Replace("-", "").Trim();
                if (raw.Length < 4)
                {
                    ModelState.AddModelError(nameof(vm.NumeroTarjeta), "Número de tarjeta inválido.");
                    return View(vm);
                }
                var last4 = raw[^4..];
                user.Ultimos4Tarjeta = $"****-****-****-{last4}";
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err.Description);
                return View(vm);
            }

            TempData["Success"] = "Datos actualizados correctamente.";
            return RedirectToAction(nameof(MisDatos));
        }

        // GET: MiCuenta/MiHistorial
        public async Task<IActionResult> MiHistorial()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var facturas = await _context.Facturas
                .Include(f => f.Pedido)
                .Include(f => f.FacturaDetalles)
                    .ThenInclude(fd => fd.Producto)
                .Where(f => f.UsuarioId == userId)
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();

            return View(facturas);
        }
    }
}
