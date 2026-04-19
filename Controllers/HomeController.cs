using Microsoft.AspNetCore.Mvc;
using proyectoprogra.Models;
using proyectoprogra.Data;
using System.Diagnostics;

namespace proyectoprogra.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // Usuario (web) has no dashboard — send straight to their order page
            if (User.IsInRole("Usuario") && !User.IsInRole("Administrador"))
                return RedirectToAction("Create", "Pedidos");

            // TARJETAS DEL DASHBOARD
            ViewBag.TotalProductos = _context.Productos.Count();
            ViewBag.TotalPedidos = _context.Pedidos.Count();

            // GRÁFICO SIMPLE (cantidad de pedidos por día)
            var pedidosPorDia = _context.Pedidos
                .GroupBy(p => p.Fecha)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Cantidad = g.Count()
                })
                .OrderBy(x => x.Fecha)
                .Take(7)
                .ToList();

            ViewBag.VentasLabels = pedidosPorDia.Select(x => x.Fecha.ToString("dd/MM")).ToList();
            ViewBag.VentasData = pedidosPorDia.Select(x => x.Cantidad).ToList();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}