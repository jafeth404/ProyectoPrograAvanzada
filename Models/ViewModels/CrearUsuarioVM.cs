using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models.ViewModels
{
    public class CrearUsuarioVM
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; } = null!;

        /// <summary>Solo requerido cuando Rol == "Usuario". Para otros roles se autogenera.</summary>
        public string? Password { get; set; }

        [Required(ErrorMessage = "Seleccione un rol.")]
        public string Rol { get; set; } = null!;

        [Required(ErrorMessage = "La identificación es obligatoria.")]
        public string Identificacion { get; set; } = null!;

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        public string NombreCompleto { get; set; } = null!;

        [Required(ErrorMessage = "El género es obligatorio.")]
        public string Genero { get; set; } = "No especifica";

        public string TipoTarjeta { get; set; } = "N/A";

        /// <summary>Número de tarjeta completo (XXXX-XXXX-XXXX-XXXX). Se almacenan solo los últimos 4 dígitos.</summary>
        public string? NumeroTarjeta { get; set; }
    }
}
