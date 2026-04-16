using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/usuarios")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Administrador")]
    public class UsuariosApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole>    _roleManager;
        private readonly ApplicationDbContext         _context;

        public UsuariosApiController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole>    roleManager,
            ApplicationDbContext         context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context     = context;
        }

        // GET /api/v1/usuarios?page=1&pageSize=10&search=&rol=&soloActivos=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int     page       = 1,
            [FromQuery] int     pageSize   = 10,
            [FromQuery] string? search     = null,
            [FromQuery] string? rol        = null,
            [FromQuery] bool?   soloActivos = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.NombreCompleto.Contains(search) ||
                    (u.Email != null && u.Email.Contains(search)) ||
                    u.Identificacion.Contains(search));

            if (soloActivos.HasValue)
                query = query.Where(u => u.Activo == soloActivos.Value);

            var allUsers = await query.OrderBy(u => u.NombreCompleto).ToListAsync();

            // Filter by role (requires async lookup per user — done in memory after DB fetch)
            if (!string.IsNullOrWhiteSpace(rol))
            {
                var filtered = new List<ApplicationUser>();
                foreach (var u in allUsers)
                {
                    if (await _userManager.IsInRoleAsync(u, rol))
                        filtered.Add(u);
                }
                allUsers = filtered;
            }

            var total = allUsers.Count;
            var paged = allUsers.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var items = new List<UsuarioDto>();
            foreach (var u in paged)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(MapToDto(u, roles));
            }

            return Ok(ApiResponse<PagedResult<UsuarioDto>>.Ok(new PagedResult<UsuarioDto>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            }));
        }

        // GET /api/v1/usuarios/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<UsuarioDto>.Fail("Usuario no encontrado."));

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(ApiResponse<UsuarioDto>.Ok(MapToDto(user, roles)));
        }

        // POST /api/v1/usuarios
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CrearUsuarioApiRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (await _userManager.FindByEmailAsync(req.Email) != null)
                return Conflict(ApiResponse<object>.Fail("El correo ya está registrado."));

            if (!await _roleManager.RoleExistsAsync(req.Rol))
                return BadRequest(ApiResponse<object>.Fail($"El rol '{req.Rol}' no existe."));

            bool autoGenerada = req.Rol != "Usuario";
            string password   = autoGenerada ? GenerarPassword() : req.Password ?? GenerarPassword();
            string ultimos4   = ExtraerUltimos4(req.NumeroTarjeta);

            var user = new ApplicationUser
            {
                UserName        = req.Email,
                Email           = req.Email,
                EmailConfirmed  = true,
                Identificacion  = req.Identificacion,
                NombreCompleto  = req.NombreCompleto,
                Genero          = req.Genero,
                TipoTarjeta     = string.IsNullOrWhiteSpace(req.TipoTarjeta) ? "N/A" : req.TipoTarjeta,
                Ultimos4Tarjeta = ultimos4,
                DineroDisponible = 0
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return BadRequest(ApiResponse<object>.Fail("Error al crear usuario.",
                    result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, req.Rol);

            var resp = MapToDto(user, [req.Rol]);
            return StatusCode(201, ApiResponse<object>.Ok(new
            {
                usuario         = resp,
                passwordGenerada = autoGenerada ? password : null
            }, "Usuario creado correctamente."));
        }

        // PUT /api/v1/usuarios/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ActualizarUsuarioApiRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.Fail("Usuario no encontrado."));

            if (!string.IsNullOrWhiteSpace(req.NombreCompleto)) user.NombreCompleto = req.NombreCompleto;
            if (!string.IsNullOrWhiteSpace(req.Genero))         user.Genero = req.Genero;
            if (!string.IsNullOrWhiteSpace(req.TipoTarjeta))    user.TipoTarjeta = req.TipoTarjeta;

            if (!string.IsNullOrWhiteSpace(req.NumeroTarjeta))
                user.Ultimos4Tarjeta = ExtraerUltimos4(req.NumeroTarjeta);

            if (!string.IsNullOrWhiteSpace(req.Email) && user.Email != req.Email)
            {
                user.Email           = req.Email;
                user.UserName        = req.Email;
                user.NormalizedEmail = req.Email.ToUpperInvariant();
                user.NormalizedUserName = req.Email.ToUpperInvariant();
            }

            await _userManager.UpdateAsync(user);

            if (!string.IsNullOrWhiteSpace(req.Rol))
            {
                if (!await _roleManager.RoleExistsAsync(req.Rol))
                    return BadRequest(ApiResponse<object>.Fail($"El rol '{req.Rol}' no existe."));

                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, req.Rol);
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(ApiResponse<UsuarioDto>.Ok(MapToDto(user, roles), "Usuario actualizado correctamente."));
        }

        // DELETE /api/v1/usuarios/{id}  (soft-delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.Fail("Usuario no encontrado."));

            if (user.HaIniciadoSesion)
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar este usuario porque ya ha iniciado sesión."));

            if (await _context.Facturas.AnyAsync(f => f.UsuarioId == id))
                return Conflict(ApiResponse<object>.Fail(
                    "No se puede eliminar este usuario porque tiene facturas asociadas."));

            if (await _userManager.IsInRoleAsync(user, "Administrador"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Administrador");
                if (admins.Count(u => u.Activo && u.Id != id) == 0)
                    return Conflict(ApiResponse<object>.Fail(
                        "No se puede eliminar el único Administrador activo del sistema."));
            }

            user.Activo = false;
            await _userManager.UpdateAsync(user);

            return Ok(ApiResponse<object>.Ok(null, "Usuario desactivado correctamente."));
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static UsuarioDto MapToDto(ApplicationUser u, IList<string> roles) => new()
        {
            Id              = u.Id,
            Identificacion  = u.Identificacion,
            NombreCompleto  = u.NombreCompleto,
            Email           = u.Email ?? "",
            Genero          = u.Genero,
            TipoTarjeta     = u.TipoTarjeta,
            Ultimos4Tarjeta = u.Ultimos4Tarjeta,
            DineroDisponible = u.DineroDisponible,
            Activo          = u.Activo,
            Roles           = roles
        };

        private static string ExtraerUltimos4(string? numero)
        {
            if (string.IsNullOrWhiteSpace(numero)) return "0000";
            var digits = new string(numero.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : "0000";
        }

        private static string GenerarPassword()
        {
            const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower   = "abcdefghjkmnpqrstuvwxyz";
            const string digits  = "23456789";
            const string special = "!@#$%";
            const string all     = upper + lower + digits + special;

            var rng   = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            var chars = new char[12];
            chars[0] = upper[rng[0]  % upper.Length];
            chars[1] = lower[rng[1]  % lower.Length];
            chars[2] = digits[rng[2] % digits.Length];
            chars[3] = special[rng[3] % special.Length];
            for (int i = 4; i < 12; i++)
                chars[i] = all[rng[i] % all.Length];

            for (int i = 11; i > 0; i--)
            {
                int j = rng[i % 16] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars);
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class UsuarioDto
    {
        public string   Id              { get; set; } = null!;
        public string   Identificacion  { get; set; } = null!;
        public string   NombreCompleto  { get; set; } = null!;
        public string   Email           { get; set; } = null!;
        public string   Genero          { get; set; } = null!;
        public string   TipoTarjeta     { get; set; } = null!;
        public string   Ultimos4Tarjeta { get; set; } = null!;
        public decimal  DineroDisponible { get; set; }
        public bool     Activo          { get; set; }
        public IList<string> Roles      { get; set; } = [];
    }

    public class CrearUsuarioApiRequest
    {
        [Required] public string Identificacion  { get; set; } = null!;
        [Required] public string NombreCompleto  { get; set; } = null!;
        [Required] public string Genero          { get; set; } = "No especifica";
        [Required, EmailAddress] public string Email { get; set; } = null!;
        public string? TipoTarjeta  { get; set; }
        public string? NumeroTarjeta { get; set; }
        public string? Password     { get; set; }
        [Required] public string Rol { get; set; } = "Usuario";
    }

    public class ActualizarUsuarioApiRequest
    {
        public string? NombreCompleto  { get; set; }
        public string? Genero          { get; set; }
        [EmailAddress] public string? Email { get; set; }
        public string? TipoTarjeta     { get; set; }
        public string? NumeroTarjeta   { get; set; }
        public string? Rol             { get; set; }
    }
}
