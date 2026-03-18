using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace proyectoprogra.Models.Entities;

public partial class FacturaDetalle
{
    [Key]
    public int DetalleFacturaId { get; set; }

    public int FacturaId { get; set; }

    public int ProductoId { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal TotalLinea { get; set; }

    public virtual Factura Factura { get; set; } = null!;

    public virtual Producto Producto { get; set; } = null!;
}
