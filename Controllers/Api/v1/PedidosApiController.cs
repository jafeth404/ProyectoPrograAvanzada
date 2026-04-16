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
    [Route("api/v1/pedidos")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
               Roles = "Administrador,Salonero,Cajero,Cocina")]
    public class PedidosApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PedidosApiController(ApplicationDbContext context) => _context = context;

        // GET /api/v1/pedidos?page=1&pageSize=10&estado=&tipo=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 10,
            [FromQuery] string? estado   = null,
            [FromQuery] string? tipo     = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Pedidos
                .Include(p => p.PedidoDetalles).ThenInclude(d => d.Producto)
                .Include(p => p.Mesa)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(p => p.Estado == estado);

            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(p => p.TipoPedido == tipo);

            var total = await query.CountAsync();
            var pedidos = await query
                .OrderByDescending(p => p.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = pedidos.Select(MapToDto).ToList();

            return Ok(ApiResponse<PagedResult<PedidoDto>>.Ok(new PagedResult<PedidoDto>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            }));
        }

        // GET /api/v1/pedidos/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles).ThenInclude(d => d.Producto)
                .Include(p => p.Mesa)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null)
                return NotFound(ApiResponse<PedidoDto>.Fail("Pedido no encontrado."));

            return Ok(ApiResponse<PedidoDto>.Ok(MapToDto(pedido)));
        }

        // POST /api/v1/pedidos
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
                   Roles = "Administrador,Salonero,Usuario")]
        public async Task<IActionResult> Create([FromBody] CrearPedidoApiRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (req.TipoPedido == "dine-in" && req.MesaId == null)
                return BadRequest(ApiResponse<object>.Fail(
                    "Se requiere MesaId para pedidos de tipo dine-in."));

            if (req.MesaId.HasValue &&
                !await _context.Mesas.AnyAsync(m => m.MesaId == req.MesaId.Value))
                return BadRequest(ApiResponse<object>.Fail("La mesa especificada no existe."));

            if (req.Detalles == null || !req.Detalles.Any())
                return BadRequest(ApiResponse<object>.Fail("El pedido debe tener al menos un producto."));

            var productoIds = req.Detalles.Select(d => d.ProductoId).Distinct().ToList();
            var productos   = await _context.Productos
                .Where(p => productoIds.Contains(p.ProductoId))
                .ToListAsync();

            if (productos.Count != productoIds.Count)
                return BadRequest(ApiResponse<object>.Fail("Uno o más productos no existen."));

            var pedido = new Pedido
            {
                TipoPedido = req.TipoPedido,
                MesaId     = req.MesaId,
                Fecha      = DateTime.Now,
                Estado     = "Pendiente"
            };

            foreach (var det in req.Detalles)
            {
                var prod = productos.First(p => p.ProductoId == det.ProductoId);
                pedido.PedidoDetalles.Add(new PedidoDetalle
                {
                    ProductoId     = det.ProductoId,
                    Cantidad       = det.Cantidad,
                    PrecioUnitario = prod.Precio,
                    Estado         = "Pendiente"
                });
            }

            if (req.MesaId.HasValue)
            {
                var mesa = await _context.Mesas.FindAsync(req.MesaId.Value);
                if (mesa != null) mesa.Estado = "ocupada";
            }

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            await _context.Entry(pedido).Collection(p => p.PedidoDetalles)
                .Query().Include(d => d.Producto).LoadAsync();

            return StatusCode(201, ApiResponse<PedidoDto>.Ok(MapToDto(pedido),
                "Pedido creado correctamente."));
        }

        // PUT /api/v1/pedidos/{id}
        [HttpPut("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
                   Roles = "Administrador,Salonero,Cocina")]
        public async Task<IActionResult> Update(int id, [FromBody] ActualizarPedidoRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null)
                return NotFound(ApiResponse<object>.Fail("Pedido no encontrado."));

            if (!string.IsNullOrWhiteSpace(req.Estado)) pedido.Estado = req.Estado;

            // Update individual item states if provided
            if (req.EstadosDetalles != null)
            {
                foreach (var ed in req.EstadosDetalles)
                {
                    var det = pedido.PedidoDetalles.FirstOrDefault(d => d.DetalleId == ed.DetalleId);
                    if (det != null) det.Estado = ed.Estado;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, "Pedido actualizado correctamente."));
        }

        // DELETE /api/v1/pedidos/{id}
        [HttpDelete("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
                   Roles = "Administrador")]
        public async Task<IActionResult> Delete(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Facturas)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pedido == null)
                return NotFound(ApiResponse<object>.Fail("Pedido no encontrado."));

            if (pedido.Facturas.Any())
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar este pedido porque tiene facturas asociadas."));

            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, "Pedido eliminado correctamente."));
        }

        private static PedidoDto MapToDto(Pedido p) => new()
        {
            PedidoId   = p.PedidoId,
            TipoPedido = p.TipoPedido,
            MesaId     = p.MesaId,
            NumeroMesa = p.Mesa?.NumeroMesa,
            Fecha      = p.Fecha,
            Estado     = p.Estado,
            Detalles   = p.PedidoDetalles.Select(d => new PedidoDetalleDto
            {
                DetalleId      = d.DetalleId,
                ProductoId     = d.ProductoId,
                NombreProducto = d.Producto?.Nombre,
                Cantidad       = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Estado         = d.Estado
            }).ToList()
        };
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class PedidoDto
    {
        public int      PedidoId   { get; set; }
        public string   TipoPedido { get; set; } = null!;
        public int?     MesaId     { get; set; }
        public int?     NumeroMesa { get; set; }
        public DateTime Fecha      { get; set; }
        public string?  Estado     { get; set; }
        public List<PedidoDetalleDto> Detalles { get; set; } = [];
    }

    public class PedidoDetalleDto
    {
        public int     DetalleId      { get; set; }
        public int     ProductoId     { get; set; }
        public string? NombreProducto { get; set; }
        public int     Cantidad       { get; set; }
        public decimal PrecioUnitario { get; set; }
        public string? Estado         { get; set; }
    }

    public class CrearPedidoApiRequest
    {
        [Required] public string TipoPedido { get; set; } = null!;
        public int? MesaId { get; set; }

        [Required, MinLength(1)]
        public List<DetalleRequest> Detalles { get; set; } = [];
    }

    public class DetalleRequest
    {
        [Required] public int ProductoId { get; set; }
        [Required, Range(1, int.MaxValue)] public int Cantidad { get; set; }
    }

    public class ActualizarPedidoRequest
    {
        public string? Estado { get; set; }
        public List<EstadoDetalleRequest>? EstadosDetalles { get; set; }
    }

    public class EstadoDetalleRequest
    {
        public int    DetalleId { get; set; }
        public string Estado    { get; set; } = null!;
    }
}
