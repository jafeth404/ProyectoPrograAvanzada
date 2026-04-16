using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Data;
using proyectoprogra.Models;
using proyectoprogra.Models.Entities;

namespace proyectoprogra.Controllers
{
    [Route("WebAuthn")]
    public class WebAuthnController : Controller
    {
        private readonly IFido2 _fido2;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        // Session keys
        private const string AttestationOptionsKey = "fido2.attestationOptions";
        private const string AssertionOptionsKey   = "fido2.assertionOptions";

        public WebAuthnController(
            IFido2 fido2,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _fido2       = fido2;
            _context     = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ─────────────────────────────────────────────────────────────
        // MANAGE PAGE (requires login)
        // ─────────────────────────────────────────────────────────────

        [HttpGet("Manage")]
        public async Task<IActionResult> Manage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var credentials = await _context.FidoCredentials
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.RegDate)
                .ToListAsync();

            return View(credentials);
        }

        [HttpPost("DeleteCredential")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCredential(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var cred = await _context.FidoCredentials
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

            if (cred != null)
            {
                _context.FidoCredentials.Remove(cred);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Manage));
        }

        // ─────────────────────────────────────────────────────────────
        // REGISTRATION — Step 1: get options (requires login)
        // ─────────────────────────────────────────────────────────────

        [HttpPost("MakeCredentialOptions")]
        public async Task<IActionResult> MakeCredentialOptions()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fidoUser = new Fido2User
            {
                DisplayName = user.NombreCompleto,
                Name        = user.Email!,
                Id          = WebEncoders.Base64UrlDecode(
                                  WebEncoders.Base64UrlEncode(
                                      System.Text.Encoding.UTF8.GetBytes(user.Id)))
            };

            // Exclude credentials already registered
            var existingKeys = await _context.FidoCredentials
                .Where(c => c.UserId == user.Id)
                .Select(c => c.CredentialIdBase64)
                .ToListAsync();

            var excludeCredentials = existingKeys
                .Select(k => new PublicKeyCredentialDescriptor(WebEncoders.Base64UrlDecode(k)))
                .ToList();

            var authenticatorSelection = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                RequireResidentKey      = true,
                UserVerification        = UserVerificationRequirement.Required
            };

            var options = _fido2.RequestNewCredential(
                fidoUser,
                excludeCredentials,
                authenticatorSelection,
                AttestationConveyancePreference.None);

            HttpContext.Session.SetString(AttestationOptionsKey, options.ToJson());

            return Json(options);
        }

        // ─────────────────────────────────────────────────────────────
        // REGISTRATION — Step 2: verify & store (requires login)
        // ─────────────────────────────────────────────────────────────

        [HttpPost("MakeCredential")]
        public async Task<IActionResult> MakeCredential(
            [FromBody] AuthenticatorAttestationRawResponse attestationResponse)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var json = HttpContext.Session.GetString(AttestationOptionsKey);
            if (json == null) return BadRequest(new { error = "Sesión expirada, intente de nuevo." });

            var options = CredentialCreateOptions.FromJson(json);

            try
            {
                var makeResult = await _fido2.MakeNewCredentialAsync(
                    attestationResponse,
                    options,
                    async (args, ct) =>
                    {
                        var idB64 = WebEncoders.Base64UrlEncode(args.CredentialId);
                        return !await _context.FidoCredentials.AnyAsync(c => c.CredentialIdBase64 == idB64, ct);
                    });

                var credential = makeResult.Result
                    ?? throw new Exception("La verificación de credencial falló.");

                _context.FidoCredentials.Add(new FidoStoredCredential
                {
                    UserId             = user.Id,
                    Username           = user.Email!,
                    UserHandleBase64   = WebEncoders.Base64UrlEncode(credential.User.Id),
                    CredentialIdBase64 = WebEncoders.Base64UrlEncode(credential.CredentialId),
                    PublicKey          = credential.PublicKey,
                    SignatureCounter   = credential.Counter,
                    CredType           = credential.CredType,
                    AaGuid             = Guid.Empty,
                    RegDate            = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Json(new { status = "ok" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // AUTHENTICATION — Step 1: get challenge (anonymous)
        // ─────────────────────────────────────────────────────────────

        [HttpPost("AssertionOptions")]
        [AllowAnonymous]
        public IActionResult AssertionOptions()
        {
            // Empty allowCredentials → discoverable credential (passkey)
            var options = _fido2.GetAssertionOptions(
                Array.Empty<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required);

            HttpContext.Session.SetString(AssertionOptionsKey, options.ToJson());

            return Json(options);
        }

        // ─────────────────────────────────────────────────────────────
        // AUTHENTICATION — Step 2: verify & sign in (anonymous)
        // ─────────────────────────────────────────────────────────────

        [HttpPost("MakeAssertion")]
        [AllowAnonymous]
        public async Task<IActionResult> MakeAssertion(
            [FromBody] AuthenticatorAssertionRawResponse assertionResponse)
        {
            var json = HttpContext.Session.GetString(AssertionOptionsKey);
            if (json == null) return BadRequest(new { error = "Sesión expirada, intente de nuevo." });

            // Use fully-qualified name to avoid shadowing by the AssertionOptions() method above
            var options = Fido2NetLib.AssertionOptions.FromJson(json);

            // Find the stored credential by credential ID
            var credIdB64 = WebEncoders.Base64UrlEncode(assertionResponse.Id);
            var storedCred = await _context.FidoCredentials
                .FirstOrDefaultAsync(c => c.CredentialIdBase64 == credIdB64);

            if (storedCred == null)
                return BadRequest(new { error = "Credencial no encontrada." });

            try
            {
                var result = await _fido2.MakeAssertionAsync(
                    assertionResponse,
                    options,
                    storedCred.PublicKey,
                    storedCred.SignatureCounter,
                    async (args, ct) =>
                    {
                        var userHandleB64 = WebEncoders.Base64UrlEncode(args.UserHandle);
                        return await _context.FidoCredentials.AnyAsync(
                            c => c.UserHandleBase64 == userHandleB64 &&
                                 c.CredentialIdBase64 == WebEncoders.Base64UrlEncode(args.CredentialId),
                            ct);
                    });

                // Update signature counter to prevent replay attacks
                storedCred.SignatureCounter = result.Counter;
                await _context.SaveChangesAsync();

                // Sign the user in via Identity
                var appUser = await _userManager.FindByIdAsync(storedCred.UserId);
                if (appUser == null || !appUser.Activo)
                    return BadRequest(new { error = "Usuario no encontrado o inactivo." });

                await _signInManager.SignInAsync(appUser, isPersistent: false);

                return Json(new { status = "ok", redirectUrl = "/" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
