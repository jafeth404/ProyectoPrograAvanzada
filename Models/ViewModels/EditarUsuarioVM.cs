using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models.ViewModels
{
    public class EditarUsuarioVM
    {
        public string Id { get; set; } = null!;

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        public string NombreCompleto { get; set; } = null!;

        [Required(ErrorMessage = "El género es obligatorio.")]
        public string Genero { get; set; } = null!;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; } = null!;

        public string TipoTarjeta { get; set; } = "N/A";

        /// <summary>Current masked last-4 stored in DB (read-only display).</summary>
        public string? Ultimos4Tarjeta { get; set; }

        /// <summary>Número de tarjeta completo para actualizar los últimos 4 dígitos. Opcional.</summary>
        public string? NumeroTarjeta { get; set; }

        [Required(ErrorMessage = "Seleccione un rol.")]
        public string Rol { get; set; } = null!;

        public bool Activo { get; set; } = true;

        public bool HaIniciadoSesion { get; set; } = false;

        /// <summary>Read-only balance shown to the admin.</summary>
        public decimal DineroDisponible { get; set; } = 0m;
    }
}
