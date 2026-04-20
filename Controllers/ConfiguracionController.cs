using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;

namespace proyectoprogra.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ConfiguracionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ConfiguracionController(ApplicationDbContext context)
            => _context = context;

        public async Task<IActionResult> Index()
        {
            var config = await GetOrCreateAsync();
            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ConfiguracionSistema model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var config = await GetOrCreateAsync();
            config.TipoCargoDelivery = model.TipoCargoDelivery;
            config.CargoDelivery     = model.CargoDelivery;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Configuración guardada.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<ConfiguracionSistema> GetOrCreateAsync()
        {
            var config = await _context.ConfiguracionSistema.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new ConfiguracionSistema();
                _context.ConfiguracionSistema.Add(config);
                await _context.SaveChangesAsync();
            }
            return config;
        }
    }
}
