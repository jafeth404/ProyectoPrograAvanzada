using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/mesas")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Administrador")]
    public class MesasApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MesasApiController(ApplicationDbContext context) => _context = context;

        // GET /api/v1/mesas?page=1&pageSize=10&estado=libre
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 10,
            [FromQuery] string? estado   = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Mesas.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(m => m.Estado == estado);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(m => m.NumeroMesa)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MesaDto
                {
                    MesaId      = m.MesaId,
                    NumeroMesa  = m.NumeroMesa,
                    Capacidad   = m.Capacidad,
                    Estado      = m.Estado
                })
                .ToListAsync();

            return Ok(ApiResponse<PagedResult<MesaDto>>.Ok(new PagedResult<MesaDto>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            }));
        }

        // GET /api/v1/mesas/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var mesa = await _context.Mesas.AsNoTracking()
                .Where(m => m.MesaId == id)
                .Select(m => new MesaDto
                {
                    MesaId     = m.MesaId,
                    NumeroMesa = m.NumeroMesa,
                    Capacidad  = m.Capacidad,
                    Estado     = m.Estado
                })
                .FirstOrDefaultAsync();

            if (mesa == null)
                return NotFound(ApiResponse<MesaDto>.Fail("Mesa no encontrada."));

            return Ok(ApiResponse<MesaDto>.Ok(mesa));
        }

        // POST /api/v1/mesas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CrearMesaRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (await _context.Mesas.AnyAsync(m => m.NumeroMesa == req.NumeroMesa))
                return Conflict(ApiResponse<object>.Fail("Ya existe una mesa con ese número."));

            var mesa = new Mesa
            {
                NumeroMesa = req.NumeroMesa,
                Capacidad  = req.Capacidad,
                Estado     = "libre"
            };

            _context.Mesas.Add(mesa);
            await _context.SaveChangesAsync();

            return StatusCode(201, ApiResponse<MesaDto>.Ok(new MesaDto
            {
                MesaId     = mesa.MesaId,
                NumeroMesa = mesa.NumeroMesa,
                Capacidad  = mesa.Capacidad,
                Estado     = mesa.Estado
            }, "Mesa creada correctamente."));
        }

        // PUT /api/v1/mesas/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ActualizarMesaRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var mesa = await _context.Mesas.FindAsync(id);
            if (mesa == null)
                return NotFound(ApiResponse<object>.Fail("Mesa no encontrada."));

            if (req.Capacidad.HasValue) mesa.Capacidad = req.Capacidad.Value;
            if (!string.IsNullOrWhiteSpace(req.Estado)) mesa.Estado = req.Estado;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<MesaDto>.Ok(new MesaDto
            {
                MesaId     = mesa.MesaId,
                NumeroMesa = mesa.NumeroMesa,
                Capacidad  = mesa.Capacidad,
                Estado     = mesa.Estado
            }, "Mesa actualizada correctamente."));
        }

        // DELETE /api/v1/mesas/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var mesa = await _context.Mesas.FindAsync(id);
            if (mesa == null)
                return NotFound(ApiResponse<object>.Fail("Mesa no encontrada."));

            bool tienePedidos = await _context.Pedidos.AnyAsync(p => p.MesaId == id);
            if (tienePedidos)
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar esta mesa porque tiene pedidos asociados."));

            _context.Mesas.Remove(mesa);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, "Mesa eliminada correctamente."));
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class MesaDto
    {
        public int     MesaId     { get; set; }
        public int     NumeroMesa { get; set; }
        public int     Capacidad  { get; set; }
        public string? Estado     { get; set; }
    }

    public class CrearMesaRequest
    {
        [Required, Range(1, int.MaxValue)] public int NumeroMesa { get; set; }
        [Required, Range(1, int.MaxValue)] public int Capacidad  { get; set; }
    }

    public class ActualizarMesaRequest
    {
        [Range(1, int.MaxValue)] public int?    Capacidad { get; set; }
        public string? Estado    { get; set; }
    }
}
