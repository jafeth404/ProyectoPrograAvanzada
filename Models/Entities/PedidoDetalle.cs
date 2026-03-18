using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace proyectoprogra.Models.Entities;

public partial class PedidoDetalle
{
    [Key]
    public int DetalleId { get; set; }

    public int PedidoId { get; set; }

    public int ProductoId { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public string? Estado { get; set; }

    public virtual Pedido? Pedido { get; set; } 

    public virtual Producto? Producto { get; set; } 
}
