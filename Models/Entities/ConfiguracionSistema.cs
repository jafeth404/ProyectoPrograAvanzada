namespace proyectoprogra.Models.Entities;

public class ConfiguracionSistema
{
    public int Id { get; set; }

    /// <summary>"Monto" or "Porcentaje"</summary>
    public string TipoCargoDelivery { get; set; } = "Monto";

    public decimal CargoDelivery { get; set; } = 0;
}
