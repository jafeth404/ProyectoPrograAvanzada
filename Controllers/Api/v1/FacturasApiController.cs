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
    [Route("api/v1/facturas")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
               Roles = "Administrador,Cajero,Contabilidad")]
    public class FacturasApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private const decimal IVA_RATE      = 0.13m;
        private const decimal PROPINA_RATE  = 0.10m;

        public FacturasApiController(ApplicationDbContext context) => _context = context;

        // GET /api/v1/facturas?page=1&pageSize=10&usuarioId=&fechaDesde=&fechaHasta=&reversada=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int      page       = 1,
            [FromQuery] int      pageSize   = 10,
            [FromQuery] string?  usuarioId  = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null,
            [FromQuery] bool?    reversada  = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Facturas
                .Include(f => f.Pedido)
                .Include(f => f.FacturaDetalles).ThenInclude(d => d.Producto)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(usuarioId))
                query = query.Where(f => f.UsuarioId == usuarioId);

            if (fechaDesde.HasValue)
                query = query.Where(f => f.Fecha >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(f => f.Fecha <= fechaHasta.Value);

            if (reversada.HasValue)
                query = query.Where(f => f.Reversada == reversada.Value);

            var total = await query.CountAsync();
            var facturas = await query
                .OrderByDescending(f => f.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = facturas.Select(MapToDto).ToList();

            return Ok(ApiResponse<PagedResult<FacturaDto>>.Ok(new PagedResult<FacturaDto>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            }));
        }

        // GET /api/v1/facturas/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Pedido)
                .Include(f => f.FacturaDetalles).ThenInclude(d => d.Producto)
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FacturaId == id);

            if (factura == null)
                return NotFound(ApiResponse<FacturaDto>.Fail("Factura no encontrada."));

            return Ok(ApiResponse<FacturaDto>.Ok(MapToDto(factura)));
        }

        // POST /api/v1/facturas
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
                   Roles = "Administrador,Cajero")]
        public async Task<IActionResult> Create([FromBody] CrearFacturaApiRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoDetalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == req.PedidoId);

            if (pedido == null)
                return NotFound(ApiResponse<object>.Fail("Pedido no encontrado."));

            if (await _context.Facturas.AnyAsync(f => f.PedidoId == req.PedidoId && !f.Reversada))
                return Conflict(ApiResponse<object>.Fail("Este pedido ya tiene una factura activa."));

            // Validate stock
            foreach (var det in pedido.PedidoDetalles)
            {
                if (det.Producto == null) continue;
                if (det.Producto.Stock < det.Cantidad)
                    return BadRequest(ApiResponse<object>.Fail(
                        $"Stock insuficiente para el producto '{det.Producto.Nombre}'."));
            }

            decimal subtotal       = pedido.PedidoDetalles.Sum(d => d.PrecioUnitario * d.Cantidad);
            decimal costoEmpaque   = pedido.TipoPedido == "takeout"
                ? pedido.PedidoDetalles
                    .Where(d => d.Producto?.RequiereEmpaque == true)
                    .Sum(d => (d.Producto!.CostoEmpaque ?? 0) * d.Cantidad)
                : 0;
            decimal costoDelivery  = pedido.TipoPedido == "delivery" ? req.CostoDelivery ?? 0 : 0;
            decimal propina        = pedido.TipoPedido == "dine-in" ? subtotal * PROPINA_RATE : 0;
            decimal baseIva        = subtotal + costoEmpaque + costoDelivery + propina;
            decimal iva            = baseIva * IVA_RATE;
            decimal total          = baseIva + iva;

            // Apply credit
            decimal creditoAplicado = 0;
            if (!string.IsNullOrEmpty(req.UsuarioId))
            {
                var usuario = await _context.Users.FindAsync(req.UsuarioId);
                if (usuario != null && usuario.DineroDisponible > 0)
                {
                    creditoAplicado = Math.Min(usuario.DineroDisponible, total);
                    usuario.DineroDisponible -= creditoAplicado;
                    total -= creditoAplicado;
                }
            }

            var numeroFactura = $"FAC-{DateTime.Now:yyyyMMddHHmmss}-{pedido.PedidoId}";

            var factura = new Factura
            {
                NumeroFactura   = numeroFactura,
                PedidoId        = pedido.PedidoId,
                Fecha           = DateTime.Now,
                Subtotal        = subtotal,
                Iva             = iva,
                Propina         = propina > 0 ? propina : null,
                CostoEmpaque    = costoEmpaque > 0 ? costoEmpaque : null,
                CostoDelivery   = costoDelivery > 0 ? costoDelivery : null,
                Total           = total + creditoAplicado, // original total
                CreditoAplicado = creditoAplicado,
                UsuarioId       = req.UsuarioId,
                Reversada       = false
            };

            foreach (var det in pedido.PedidoDetalles)
            {
                factura.FacturaDetalles.Add(new FacturaDetalle
                {
                    ProductoId     = det.ProductoId,
                    Cantidad       = det.Cantidad,
                    PrecioUnitario = det.PrecioUnitario,
                    TotalLinea     = det.PrecioUnitario * det.Cantidad
                });

                if (det.Producto != null)
                    det.Producto.Stock -= det.Cantidad;
            }

            pedido.Estado = "Facturado";

            _context.Facturas.Add(factura);
            await _context.SaveChangesAsync();

            return StatusCode(201, ApiResponse<FacturaDto>.Ok(
                MapToDto(factura), "Factura creada correctamente."));
        }

        private static FacturaDto MapToDto(Factura f) => new()
        {
            FacturaId       = f.FacturaId,
            NumeroFactura   = f.NumeroFactura,
            PedidoId        = f.PedidoId,
            TipoPedido      = f.Pedido?.TipoPedido,
            Fecha           = f.Fecha,
            Subtotal        = f.Subtotal,
            Iva             = f.Iva,
            Propina         = f.Propina,
            CostoEmpaque    = f.CostoEmpaque,
            CostoDelivery   = f.CostoDelivery,
            Total           = f.Total,
            CreditoAplicado = f.CreditoAplicado,
            UsuarioId       = f.UsuarioId,
            Reversada       = f.Reversada,
            Detalles        = f.FacturaDetalles.Select(d => new FacturaDetalleDto
            {
                DetalleFacturaId = d.DetalleFacturaId,
                ProductoId       = d.ProductoId,
                NombreProducto   = d.Producto?.Nombre,
                Cantidad         = d.Cantidad,
                PrecioUnitario   = d.PrecioUnitario,
                TotalLinea       = d.TotalLinea
            }).ToList()
        };
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class FacturaDto
    {
        public int      FacturaId       { get; set; }
        public string   NumeroFactura   { get; set; } = null!;
        public int      PedidoId        { get; set; }
        public string?  TipoPedido      { get; set; }
        public DateTime Fecha           { get; set; }
        public decimal  Subtotal        { get; set; }
        public decimal  Iva             { get; set; }
        public decimal? Propina         { get; set; }
        public decimal? CostoEmpaque    { get; set; }
        public decimal? CostoDelivery   { get; set; }
        public decimal  Total           { get; set; }
        public decimal  CreditoAplicado { get; set; }
        public string?  UsuarioId       { get; set; }
        public bool     Reversada       { get; set; }
        public List<FacturaDetalleDto> Detalles { get; set; } = [];
    }

    public class FacturaDetalleDto
    {
        public int     DetalleFacturaId { get; set; }
        public int     ProductoId       { get; set; }
        public string? NombreProducto   { get; set; }
        public int     Cantidad         { get; set; }
        public decimal PrecioUnitario   { get; set; }
        public decimal TotalLinea       { get; set; }
    }

    public class CrearFacturaApiRequest
    {
        [Required] public int     PedidoId      { get; set; }
        public string?  UsuarioId     { get; set; }
        public decimal? CostoDelivery { get; set; }
    }
}
