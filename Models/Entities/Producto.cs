using System;
using System.Collections.Generic;

namespace proyectoprogra.Models.Entities;

public partial class Producto
{
    public int ProductoId { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public int CategoriaId { get; set; }

    public decimal Precio { get; set; }

    public bool RequiereEmpaque { get; set; }

    public decimal? CostoEmpaque { get; set; }

    public int Stock { get; set; }

    public bool Activo { get; set; }

    public virtual Categoria? Categoria { get; set; } 

    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();
}
