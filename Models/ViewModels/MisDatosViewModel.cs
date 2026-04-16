using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models.ViewModels
{
    public class MisDatosViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [Display(Name = "Nombre Completo")]
        public string NombreCompleto { get; set; } = null!;

        [Required(ErrorMessage = "El género es obligatorio.")]
        public string Genero { get; set; } = null!;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "El tipo de tarjeta es obligatorio.")]
        [Display(Name = "Tipo de Tarjeta")]
        public string TipoTarjeta { get; set; } = null!;

        /// <summary>New card number as entered by the user (16 digits or formatted).
        /// We store only the last 4 masked. Leave blank to keep existing.</summary>
        [Display(Name = "Número de Tarjeta")]
        [RegularExpression(@"^(\d{4}-\d{4}-\d{4}-\d{4}|\d{16})?$",
            ErrorMessage = "Ingrese el número en formato 0000-0000-0000-0000.")]
        public string? NumeroTarjeta { get; set; }
    }
}
