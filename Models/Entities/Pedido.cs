using System;
using System.Collections.Generic;

namespace proyectoprogra.Models.Entities;

public partial class Pedido
{
    public int PedidoId { get; set; }

    public string TipoPedido { get; set; } = null!;

    public int? MesaId { get; set; }

    public DateTime Fecha { get; set; }

    public string? Estado { get; set; }

    /// <summary>Set when a web Usuario creates the order so they can only see their own.</summary>
    public string? UsuarioId { get; set; }

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual Mesa? Mesa { get; set; }

    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();
}
