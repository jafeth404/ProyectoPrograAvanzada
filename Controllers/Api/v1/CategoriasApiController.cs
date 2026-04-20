using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using proyectoprogra.Data;
using proyectoprogra.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/categorias")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Administrador")]
    public class CategoriasApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache         _cache;

        // Well-known key under which the invalidation token lives.
        private const string TokenKey = "categorias_cache_token";

        public CategoriasApiController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache   = cache;
        }

        // GET /api/v1/categorias?page=1&pageSize=10&search=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var cacheKey = $"categorias:list:p{page}:ps{pageSize}:s{search ?? ""}";

            if (!_cache.TryGetValue(cacheKey, out PagedResult<CategoriaDto>? result))
            {
                var query = _context.Categorias.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(c =>
                        c.Codigo.Contains(search) ||
                        c.Descripcion.Contains(search));

                var total = await query.CountAsync();
                var items = await query
                    .OrderBy(c => c.CategoriaId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CategoriaDto
                    {
                        CategoriaId = c.CategoriaId,
                        Codigo      = c.Codigo,
                        Descripcion = c.Descripcion
                    })
                    .ToListAsync();

                result = new PagedResult<CategoriaDto>
                {
                    Items      = items,
                    TotalCount = total,
                    Page       = page,
                    PageSize   = pageSize
                };

                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .AddExpirationToken(new CancellationChangeToken(GetCancelToken().Token));

                _cache.Set(cacheKey, result, options);
            }

            return Ok(ApiResponse<PagedResult<CategoriaDto>>.Ok(result));
        }

        // GET /api/v1/categorias/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await _context.Categorias.AsNoTracking()
                .Where(c => c.CategoriaId == id)
                .Select(c => new CategoriaDto
                {
                    CategoriaId = c.CategoriaId,
                    Codigo      = c.Codigo,
                    Descripcion = c.Descripcion
                })
                .FirstOrDefaultAsync();

            if (cat == null)
                return NotFound(ApiResponse<CategoriaDto>.Fail("Categoría no encontrada."));

            return Ok(ApiResponse<CategoriaDto>.Ok(cat));
        }

        // POST /api/v1/categorias
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CrearCategoriaRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (await _context.Categorias.AnyAsync(c => c.Codigo == req.Codigo))
                return Conflict(ApiResponse<object>.Fail("Ya existe una categoría con ese código."));

            var cat = new Categoria { Codigo = req.Codigo, Descripcion = req.Descripcion };
            _context.Categorias.Add(cat);
            await _context.SaveChangesAsync();

            InvalidateCache();

            return StatusCode(201, ApiResponse<CategoriaDto>.Ok(new CategoriaDto
            {
                CategoriaId = cat.CategoriaId,
                Codigo      = cat.Codigo,
                Descripcion = cat.Descripcion
            }, "Categoría creada correctamente."));
        }

        // PUT /api/v1/categorias/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ActualizarCategoriaRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var cat = await _context.Categorias.FindAsync(id);
            if (cat == null)
                return NotFound(ApiResponse<object>.Fail("Categoría no encontrada."));

            cat.Descripcion = req.Descripcion;
            await _context.SaveChangesAsync();

            InvalidateCache();

            return Ok(ApiResponse<CategoriaDto>.Ok(new CategoriaDto
            {
                CategoriaId = cat.CategoriaId,
                Codigo      = cat.Codigo,
                Descripcion = cat.Descripcion
            }, "Categoría actualizada correctamente."));
        }

        // DELETE /api/v1/categorias/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _context.Categorias.FindAsync(id);
            if (cat == null)
                return NotFound(ApiResponse<object>.Fail("Categoría no encontrada."));

            if (await _context.Productos.AnyAsync(p => p.CategoriaId == id))
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar esta categoría porque tiene productos asociados."));

            _context.Categorias.Remove(cat);
            await _context.SaveChangesAsync();

            InvalidateCache();

            return Ok(ApiResponse<object>.Ok(null, "Categoría eliminada correctamente."));
        }

        // ── cache helpers ────────────────────────────────────────────────────

        // Returns the live CancellationTokenSource, creating one if it doesn't exist.
        private CancellationTokenSource GetCancelToken()
        {
            return _cache.GetOrCreate(TokenKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return new CancellationTokenSource();
            })!;
        }

        // Cancels the current token (which expires all list entries that used it),
        // then removes it so GetCancelToken() creates a fresh one next request.
        private void InvalidateCache()
        {
            if (_cache.TryGetValue(TokenKey, out CancellationTokenSource? cts))
            {
                cts?.Cancel();
                _cache.Remove(TokenKey);
            }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class CategoriaDto
    {
        public int    CategoriaId { get; set; }
        public string Codigo      { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
    }

    public class CrearCategoriaRequest
    {
        [Required] public string Codigo      { get; set; } = null!;
        [Required] public string Descripcion { get; set; } = null!;
    }

    public class ActualizarCategoriaRequest
    {
        [Required] public string Descripcion { get; set; } = null!;
    }
}
