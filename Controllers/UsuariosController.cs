using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using proyectoprogra.Models;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsuariosController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // INDEX
        public IActionResult Index()
        {
            var usuarios = _userManager.Users.ToList();
            return View(usuarios);
        }

        // CREATE
        public IActionResult Create()
        {
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string email, string password, string rol)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,

                // valores obligatorios por tu modelo
                Identificacion = "000000000",
                NombreCompleto = "Usuario creado por admin",
                Genero = "No especifica",
                TipoTarjeta = "N/A"
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, rol);
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        // EDIT
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.CurrentRoles = await _userManager.GetRolesAsync(user);

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(string id, string rol)
        {
            var user = await _userManager.FindByIdAsync(id);

            var currentRoles = await _userManager.GetRolesAsync(user);

            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, rol);

            return RedirectToAction("Index");
        }

        // DELETE
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
                await _userManager.DeleteAsync(user);

            return RedirectToAction("Index");
        }
    }
}