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
    [Route("api/v1/productos")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Administrador")]
    public class ProductosApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache         _cache;

        private const string TokenKey = "productos_cache_token";

        public ProductosApiController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache   = cache;
        }

        // GET /api/v1/productos?page=1&pageSize=10&search=&categoriaId=&soloActivos=true
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int    page        = 1,
            [FromQuery] int    pageSize    = 10,
            [FromQuery] string? search     = null,
            [FromQuery] int?   categoriaId = null,
            [FromQuery] bool?  soloActivos = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var cacheKey = $"productos:list:p{page}:ps{pageSize}:s{search ?? ""}:cat{categoriaId}:act{soloActivos}";

            if (!_cache.TryGetValue(cacheKey, out PagedResult<ProductoDto>? result))
            {
                var query = _context.Productos
                    .Include(p => p.Categoria)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p =>
                        p.Codigo.Contains(search) ||
                        p.Nombre.Contains(search));

                if (categoriaId.HasValue)
                    query = query.Where(p => p.CategoriaId == categoriaId.Value);

                if (soloActivos.HasValue)
                    query = query.Where(p => p.Activo == soloActivos.Value);

                var total = await query.CountAsync();
                var items = await query
                    .OrderBy(p => p.ProductoId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => MapToDto(p))
                    .ToListAsync();

                result = new PagedResult<ProductoDto>
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

            return Ok(ApiResponse<PagedResult<ProductoDto>>.Ok(result));
        }

        // GET /api/v1/productos/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _context.Productos
                .Include(x => x.Categoria)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductoId == id);

            if (p == null)
                return NotFound(ApiResponse<ProductoDto>.Fail("Producto no encontrado."));

            return Ok(ApiResponse<ProductoDto>.Ok(MapToDto(p)));
        }

        // POST /api/v1/productos
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CrearProductoRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (await _context.Productos.AnyAsync(p => p.Codigo == req.Codigo))
                return Conflict(ApiResponse<object>.Fail("Ya existe un producto con ese código."));

            if (!await _context.Categorias.AnyAsync(c => c.CategoriaId == req.CategoriaId))
                return BadRequest(ApiResponse<object>.Fail("La categoría especificada no existe."));

            var producto = new Producto
            {
                Codigo          = req.Codigo,
                Nombre          = req.Nombre,
                CategoriaId     = req.CategoriaId,
                Precio          = req.Precio,
                RequiereEmpaque = req.RequiereEmpaque,
                CostoEmpaque    = req.CostoEmpaque,
                Stock           = req.Stock,
                Activo          = req.Activo
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            await _context.Entry(producto).Reference(p => p.Categoria).LoadAsync();

            InvalidateCache();

            return StatusCode(201, ApiResponse<ProductoDto>.Ok(MapToDto(producto),
                "Producto creado correctamente."));
        }

        // PUT /api/v1/productos/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ActualizarProductoRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.ProductoId == id);

            if (producto == null)
                return NotFound(ApiResponse<object>.Fail("Producto no encontrado."));

            if (req.CategoriaId.HasValue &&
                !await _context.Categorias.AnyAsync(c => c.CategoriaId == req.CategoriaId.Value))
                return BadRequest(ApiResponse<object>.Fail("La categoría especificada no existe."));

            if (!string.IsNullOrWhiteSpace(req.Nombre))   producto.Nombre = req.Nombre;
            if (req.CategoriaId.HasValue)                 producto.CategoriaId = req.CategoriaId.Value;
            if (req.Precio.HasValue)                      producto.Precio = req.Precio.Value;
            if (req.RequiereEmpaque.HasValue)             producto.RequiereEmpaque = req.RequiereEmpaque.Value;
            if (req.CostoEmpaque.HasValue)                producto.CostoEmpaque = req.CostoEmpaque.Value;
            if (req.Stock.HasValue)                       producto.Stock = req.Stock.Value;
            if (req.Activo.HasValue)                      producto.Activo = req.Activo.Value;

            await _context.SaveChangesAsync();
            await _context.Entry(producto).Reference(p => p.Categoria).LoadAsync();

            InvalidateCache();

            return Ok(ApiResponse<ProductoDto>.Ok(MapToDto(producto), "Producto actualizado correctamente."));
        }

        // DELETE /api/v1/productos/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(ApiResponse<object>.Fail("Producto no encontrado."));

            bool tieneFacturas = await _context.FacturaDetalles.AnyAsync(f => f.ProductoId == id);
            if (tieneFacturas)
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar este producto porque está asociado a facturas."));

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            InvalidateCache();

            return Ok(ApiResponse<object>.Ok(null, "Producto eliminado correctamente."));
        }

        // ── cache helpers ────────────────────────────────────────────────────

        private CancellationTokenSource GetCancelToken()
        {
            return _cache.GetOrCreate(TokenKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return new CancellationTokenSource();
            })!;
        }

        private void InvalidateCache()
        {
            if (_cache.TryGetValue(TokenKey, out CancellationTokenSource? cts))
            {
                cts?.Cancel();
                _cache.Remove(TokenKey);
            }
        }

        private static ProductoDto MapToDto(Producto p) => new()
        {
            ProductoId       = p.ProductoId,
            Codigo           = p.Codigo,
            Nombre           = p.Nombre,
            CategoriaId      = p.CategoriaId,
            CategoriaNombre  = p.Categoria?.Descripcion,
            Precio           = p.Precio,
            RequiereEmpaque  = p.RequiereEmpaque,
            CostoEmpaque     = p.CostoEmpaque,
            Stock            = p.Stock,
            Activo           = p.Activo
        };
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class ProductoDto
    {
        public int     ProductoId       { get; set; }
        public string  Codigo           { get; set; } = null!;
        public string  Nombre           { get; set; } = null!;
        public int     CategoriaId      { get; set; }
        public string? CategoriaNombre  { get; set; }
        public decimal Precio           { get; set; }
        public bool    RequiereEmpaque  { get; set; }
        public decimal? CostoEmpaque   { get; set; }
        public int     Stock            { get; set; }
        public bool    Activo           { get; set; }
    }

    public class CrearProductoRequest
    {
        [Required] public string  Codigo          { get; set; } = null!;
        [Required] public string  Nombre          { get; set; } = null!;
        [Required] public int     CategoriaId     { get; set; }
        [Required, Range(0, double.MaxValue)] public decimal Precio { get; set; }
        public bool    RequiereEmpaque  { get; set; }
        public decimal? CostoEmpaque   { get; set; }
        [Required, Range(0, int.MaxValue)] public int Stock { get; set; }
        public bool    Activo           { get; set; } = true;
    }

    public class ActualizarProductoRequest
    {
        public string?  Nombre          { get; set; }
        public int?     CategoriaId     { get; set; }
        [Range(0, double.MaxValue)] public decimal? Precio { get; set; }
        public bool?    RequiereEmpaque { get; set; }
        public decimal? CostoEmpaque    { get; set; }
        [Range(0, int.MaxValue)] public int? Stock { get; set; }
        public bool?    Activo          { get; set; }
    }
}
