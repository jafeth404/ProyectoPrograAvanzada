using System;
using System.Collections.Generic;

namespace proyectoprogra.Models.Entities;

public partial class Factura
{
    public int FacturaId { get; set; }

    public string NumeroFactura { get; set; } = null!;

    public int PedidoId { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Iva { get; set; }

    public decimal? Propina { get; set; }

    public decimal? CostoEmpaque { get; set; }

    public decimal? CostoDelivery { get; set; }

    public decimal Total { get; set; }

    public string? UsuarioId { get; set; }

    public decimal CreditoAplicado { get; set; } = 0;

    public bool Reversada { get; set; } = false;

    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    public virtual Pedido Pedido { get; set; } = null!;
}
