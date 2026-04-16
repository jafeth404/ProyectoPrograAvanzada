using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using proyectoprogra.Models;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace proyectoprogra.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/auth")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config      = config;
        }

        // POST /api/v1/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null || !user.Activo)
                return Unauthorized(ApiResponse<object>.Fail("Credenciales inválidas o usuario inactivo."));

            if (!await _userManager.CheckPasswordAsync(user, req.Password))
                return Unauthorized(ApiResponse<object>.Fail("Credenciales inválidas o usuario inactivo."));

            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateToken(user, roles);

            return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
            {
                Token     = token,
                Email     = user.Email!,
                Nombre    = user.NombreCompleto,
                Roles     = roles,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("Jwt:ExpirationMinutes"))
            }));
        }

        // POST /api/v1/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Datos inválidos",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            if (await _userManager.FindByEmailAsync(req.Email) != null)
                return Conflict(ApiResponse<object>.Fail("El correo ya está registrado."));

            var ultimos4 = ExtraerUltimos4(req.NumeroTarjeta);

            var user = new ApplicationUser
            {
                UserName        = req.Email,
                Email           = req.Email,
                EmailConfirmed  = true,
                Identificacion  = req.Identificacion,
                NombreCompleto  = req.NombreCompleto,
                Genero          = req.Genero,
                TipoTarjeta     = req.TipoTarjeta,
                Ultimos4Tarjeta = ultimos4,
                DineroDisponible = 0
            };

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return BadRequest(ApiResponse<object>.Fail("Error al crear usuario.",
                    result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Usuario");

            return StatusCode(201, ApiResponse<object>.Ok(null, "Usuario registrado correctamente."));
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private string GenerateToken(ApplicationUser user, IList<string> roles)
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var minutes = _config.GetValue<int>("Jwt:ExpirationMinutes");

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub,   user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new(ClaimTypes.Name,               user.NombreCompleto)
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var token = new JwtSecurityToken(
                issuer:   _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims:   claims,
                expires:  DateTime.UtcNow.AddMinutes(minutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string ExtraerUltimos4(string? numero)
        {
            if (string.IsNullOrWhiteSpace(numero)) return "0000";
            var digits = new string(numero.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : "0000";
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }

    public class RegisterRequest
    {
        [Required]
        public string Identificacion { get; set; } = null!;

        [Required]
        public string NombreCompleto { get; set; } = null!;

        [Required]
        public string Genero { get; set; } = "No especifica";

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string TipoTarjeta { get; set; } = null!;

        public string? NumeroTarjeta { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; } = null!;
    }

    public class LoginResponse
    {
        public string Token     { get; set; } = null!;
        public string Email     { get; set; } = null!;
        public string Nombre    { get; set; } = null!;
        public IList<string> Roles { get; set; } = [];
        public DateTime ExpiresAt { get; set; }
    }
}
