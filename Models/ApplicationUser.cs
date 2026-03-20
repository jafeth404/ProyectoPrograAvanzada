using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Identificacion { get; set; } = null!;

        [Required]
        public string NombreCompleto { get; set; } = null!;

        [Required]
        public string Genero { get; set; } = "No especifica";

        [Required]
        public string TipoTarjeta { get; set; } = null!;

        public decimal DineroDisponible { get; set; } = 0;

        [Required]
        public string Ultimos4Tarjeta { get; set; } = null!;

        public bool HaIniciadoSesion { get; set; } = false;

        public bool Activo { get; set; } = true;
    }
}