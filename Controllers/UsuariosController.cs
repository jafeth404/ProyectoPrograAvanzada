using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.ViewModels;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsuariosController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // INDEX
        public IActionResult Index()
        {
            var usuarios = _userManager.Users.ToList();
            return View(usuarios);
        }

        // CREATE GET
        public IActionResult Create()
        {
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(new CrearUsuarioVM());
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrearUsuarioVM vm)
        {
            // Password only required for Usuario role
            if (vm.Rol == "Usuario" && string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "La contraseña es obligatoria para el rol Usuario.");

            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(vm);
            }

            bool autoGenerada = vm.Rol != "Usuario";
            string password = autoGenerada ? GenerarPassword() : vm.Password!;

            string ultimos4 = ExtraerUltimos4(vm.NumeroTarjeta);

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                Identificacion = vm.Identificacion,
                NombreCompleto = vm.NombreCompleto,
                Genero = vm.Genero,
                TipoTarjeta = string.IsNullOrWhiteSpace(vm.TipoTarjeta) ? "N/A" : vm.TipoTarjeta,
                Ultimos4Tarjeta = ultimos4
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, vm.Rol);

                if (autoGenerada)
                {
                    TempData["PasswordGenerada"] = password;
                    TempData["UsuarioCreado"] = vm.Email;
                }

                TempData["Exito"] = $"Usuario {vm.Email} creado correctamente.";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(vm);
        }

        // EDIT GET
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var vm = new EditarUsuarioVM
            {
                Id = user.Id,
                NombreCompleto = user.NombreCompleto,
                Genero = user.Genero,
                Email = user.Email!,
                TipoTarjeta = user.TipoTarjeta,
                Rol = roles.FirstOrDefault() ?? ""
            };

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(vm);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditarUsuarioVM vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(vm);
            }

            var user = await _userManager.FindByIdAsync(vm.Id);
            if (user == null) return NotFound();

            user.NombreCompleto = vm.NombreCompleto;
            user.Genero = vm.Genero;
            user.TipoTarjeta = string.IsNullOrWhiteSpace(vm.TipoTarjeta) ? "N/A" : vm.TipoTarjeta;

            if (!string.IsNullOrWhiteSpace(vm.NumeroTarjeta))
                user.Ultimos4Tarjeta = ExtraerUltimos4(vm.NumeroTarjeta);

            if (user.Email != vm.Email)
            {
                user.Email = vm.Email;
                user.UserName = vm.Email;
                user.NormalizedEmail = vm.Email.ToUpperInvariant();
                user.NormalizedUserName = vm.Email.ToUpperInvariant();
            }

            await _userManager.UpdateAsync(user);

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, vm.Rol);

            TempData["Exito"] = $"Usuario {user.Email} actualizado correctamente.";
            return RedirectToAction("Index");
        }

        // RESET PASSWORD (AJAX POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Usuario no encontrado." });

            var newPassword = GenerarPassword();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
                return Json(new { success = true, password = newPassword, email = user.Email });

            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

        // DELETE GET
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return RedirectToAction("Index");

            if (user.HaIniciadoSesion)
            {
                TempData["Error"] = "No se puede eliminar este usuario porque ya ha iniciado sesión en el sistema.";
                return RedirectToAction("Delete", new { id });
            }

            bool tieneFacturas = await _context.Facturas.AnyAsync(f => f.UsuarioId == id);
            if (tieneFacturas)
            {
                TempData["Error"] = "No se puede eliminar este usuario porque tiene facturas registradas en el sistema.";
                return RedirectToAction("Delete", new { id });
            }

            bool esAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
            if (esAdmin)
            {
                var todosAdmins = await _userManager.GetUsersInRoleAsync("Administrador");
                int otrosAdminsActivos = todosAdmins.Count(u => u.Activo && u.Id != id);
                if (otrosAdminsActivos == 0)
                {
                    TempData["Error"] = "No se puede eliminar este usuario porque es el único Administrador activo del sistema.";
                    return RedirectToAction("Delete", new { id });
                }
            }

            user.Activo = false;
            await _userManager.UpdateAsync(user);
            TempData["Exito"] = $"El usuario {user.Email} fue desactivado correctamente.";
            return RedirectToAction("Index");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string ExtraerUltimos4(string? numeroTarjeta)
        {
            if (string.IsNullOrWhiteSpace(numeroTarjeta)) return "0000";
            var digits = new string(numeroTarjeta.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : "0000";
        }

        private static string GenerarPassword()
        {
            const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower   = "abcdefghjkmnpqrstuvwxyz";
            const string digits  = "23456789";
            const string special = "!@#$%";
            const string all     = upper + lower + digits + special;

            var rng = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            var chars = new char[12];

            // Guarantee at least one character from each required category
            chars[0] = upper[rng[0] % upper.Length];
            chars[1] = lower[rng[1] % lower.Length];
            chars[2] = digits[rng[2] % digits.Length];
            chars[3] = special[rng[3] % special.Length];
            for (int i = 4; i < 12; i++)
                chars[i] = all[rng[i] % all.Length];

            // Fisher-Yates shuffle
            for (int i = 11; i > 0; i--)
            {
                int j = rng[i % 16] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
    }
}
