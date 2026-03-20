#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.Entities;

namespace proyectoprogra.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            public string Identificacion { get; set; }

            [Required]
            public string NombreCompleto { get; set; }

            [Required]
            public string Genero { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            public string TipoTarjeta { get; set; }

            [Required]
            public string NumeroTarjeta { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // 🔥 LIMPIAR TARJETA
                var numeroLimpio = Input.NumeroTarjeta.Replace("-", "").Replace(" ", "");

                if (numeroLimpio.Length < 4)
                {
                    ModelState.AddModelError("", "Número de tarjeta inválido");
                    return Page();
                }

                var ultimos4 = numeroLimpio.Substring(numeroLimpio.Length - 4);

                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Usuario creado correctamente.");

                    // 🔥 GUARDAR DATOS EXTRA
                    var extra = new UsuarioExtra
                    {
                        UserId = user.Id,
                        Identificacion = Input.Identificacion,
                        NombreCompleto = Input.NombreCompleto,
                        Genero = Input.Genero,
                        TipoTarjeta = Input.TipoTarjeta,
                        Ultimos4Tarjeta = $"****-****-****-{ultimos4}",
                        DineroDisponible = 0
                    };

                    _context.UsuariosExtra.Add(extra);
                    await _context.SaveChangesAsync();

                    // 🔥 ASIGNAR ROL
                    await _userManager.AddToRoleAsync(user, "Usuario");

                    // 🔥 CONFIRM EMAIL
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        null,
                        new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        Request.Scheme);

                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Confirmar cuenta",
                        $"Confirme su cuenta haciendo clic <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>aquí</a>.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException("No se puede crear una instancia de ApplicationUser.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("El sistema requiere soporte de email.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}