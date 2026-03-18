using System;
using System.Collections.Generic;

namespace proyectoprogra.Models.Entities;

public partial class Categoria
{
    public int CategoriaId { get; set; }

    public string Codigo { get; set; } = null!;

    public string Descripcion { get; set; } = null!;

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
