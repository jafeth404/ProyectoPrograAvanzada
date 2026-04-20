using Microsoft.AspNetCore.Mvc;
using proyectoprogra.Services;

namespace proyectoprogra.Controllers
{
    /// <summary>
    /// Thin AJAX proxy to the Hacienda API so the browser never calls it directly
    /// (avoids CORS and rate-limit attribution issues).
    /// </summary>
    [Route("api/hacienda")]
    public class HaciendaController : Controller
    {
        private readonly HaciendaApiService _hacienda;
        private readonly ILogger<HaciendaController> _logger;

        public HaciendaController(HaciendaApiService hacienda, ILogger<HaciendaController> logger)
        {
            _hacienda = hacienda;
            _logger = logger;
        }

        /// <summary>GET /api/hacienda/contribuyente?id=XXXXXXXXX</summary>
        [HttpGet("contribuyente")]
        public async Task<IActionResult> Contribuyente([FromQuery] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { error = "Identificación requerida." });

            var result = await _hacienda.GetContribuyenteAsync(id.Trim());

            if (result == null)
            {
                _logger.LogWarning("No result from Hacienda API for id={Id}", id);
                return NotFound(new { error = $"No se encontró el contribuyente con cédula '{id.Trim()}'. Verifique el número o intente de nuevo." });
            }

            return Json(new
            {
                nombre = result.Nombre,
                tipoIdentificacion = result.TipoIdentificacion,
                estado = result.Situacion?.Estado,
                moroso = string.Equals(result.Situacion?.Moroso, "SI", StringComparison.OrdinalIgnoreCase),
                omiso  = string.Equals(result.Situacion?.Omiso,  "SI", StringComparison.OrdinalIgnoreCase),
                administracion = result.Situacion?.Administracion,
                actividades = result.Actividades?.Select(a => new
                {
                    codigo = a.Codigo,
                    descripcion = a.Descripcion,
                    estado = a.Estado
                })
            });
        }

        /// <summary>GET /api/hacienda/cabys?q=alimentos</summary>
        [HttpGet("cabys")]
        public async Task<IActionResult> Cabys([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
                return BadRequest(new { error = "Ingrese al menos 3 caracteres." });

            var result = await _hacienda.GetCabysAsync(q.Trim());

            if (result == null || result.Items == null || result.Items.Count == 0)
                return NotFound(new { error = "No se encontraron resultados CABYS." });

            return Json(new
            {
                total = result.Total,
                items = result.Items.Take(20).Select(i => new
                {
                    codigo = i.Codigo,
                    descripcion = i.Descripcion,
                    impuesto = i.Impuesto
                })
            });
        }

        /// <summary>GET /api/hacienda/tipocambio</summary>
        [HttpGet("tipocambio")]
        public async Task<IActionResult> TipoCambio()
        {
            var result = await _hacienda.GetTipoCambioDolarAsync();

            if (result == null)
                return StatusCode(503, new { error = "No se pudo obtener el tipo de cambio." });

            return Json(new
            {
                fecha   = result.Venta?.Fecha ?? result.Compra?.Fecha,
                venta   = result.Venta?.Valor,
                compra  = result.Compra?.Valor
            });
        }
    }
}
