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

        /// <summary>Número de tarjeta completo para actualizar los últimos 4 dígitos. Opcional.</summary>
        public string? NumeroTarjeta { get; set; }

        [Required(ErrorMessage = "Seleccione un rol.")]
        public string Rol { get; set; } = null!;
    }
}
