using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models.Entities
{
    public class UsuarioExtra
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } // FK a Identity

        public string Identificacion { get; set; }
        public string NombreCompleto { get; set; }
        public string Genero { get; set; }

        public string TipoTarjeta { get; set; }
        public string Ultimos4Tarjeta { get; set; }

        public decimal DineroDisponible { get; set; } = 0;
    }
}
