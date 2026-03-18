using System;
using System.Collections.Generic;

namespace proyectoprogra.Models.Entities;

public partial class Mesa
{
    public int MesaId { get; set; }

    public int NumeroMesa { get; set; }

    public int Capacidad { get; set; }

    public string? Estado { get; set; }

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
