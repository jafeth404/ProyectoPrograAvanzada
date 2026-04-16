using proyectoprogra.Models.Entities;
using proyectoprogra.Models;

namespace proyectoprogra.Models.ViewModels
{
    public class ContabilidadIndexVM
    {
        public string? UsuarioId { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public List<Factura> Facturas { get; set; } = new();
        public List<ApplicationUser> Usuarios { get; set; } = new();
    }
}
