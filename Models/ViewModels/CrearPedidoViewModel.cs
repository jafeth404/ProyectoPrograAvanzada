namespace proyectoprogra.Models.ViewModels
{
    public class CrearPedidoViewModel
    {
        public int MesaId { get; set; }
        public List<ItemPedidoVM> Items { get; set; } = new();
    }

    public class ItemPedidoVM
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}

