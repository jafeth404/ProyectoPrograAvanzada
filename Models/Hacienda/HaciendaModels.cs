using System.Text.Json.Serialization;

namespace proyectoprogra.Models.Hacienda
{
    // Response from /fe/ae?identificacion=XXX
    public class ContribuyenteDto
    {
        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }

        [JsonPropertyName("tipoIdentificacion")]
        public string? TipoIdentificacion { get; set; }

        [JsonPropertyName("situacion")]
        public SituacionDto? Situacion { get; set; }

        [JsonPropertyName("actividades")]
        public List<ActividadDto>? Actividades { get; set; }
    }

    public class SituacionDto
    {
        // Hacienda returns "SI" / "NO" strings, not booleans
        [JsonPropertyName("moroso")]
        public string? Moroso { get; set; }

        [JsonPropertyName("omiso")]
        public string? Omiso { get; set; }

        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("administracionTributaria")]
        public string? Administracion { get; set; }
    }

    public class ActividadDto
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("tipo")]
        public string? Tipo { get; set; }

        [JsonPropertyName("codigo")]
        public string? Codigo { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }
    }

    // Response from /fe/cabys?q=XXX or /fe/cabys?codigo=XXX
    public class CabysResultDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("cabys")]
        public List<CabysItemDto>? Items { get; set; }
    }

    public class CabysItemDto
    {
        [JsonPropertyName("codigo")]
        public string? Codigo { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("impuesto")]
        public decimal Impuesto { get; set; }

        [JsonPropertyName("categoria")]
        public string? Categoria { get; set; }
    }

    // Response from /indicadores/tc/dolar
    public class TipoCambioDto
    {
        [JsonPropertyName("venta")]
        public TipoCambioValorDto? Venta { get; set; }

        [JsonPropertyName("compra")]
        public TipoCambioValorDto? Compra { get; set; }
    }

    public class TipoCambioValorDto
    {
        [JsonPropertyName("fecha")]
        public string? Fecha { get; set; }

        [JsonPropertyName("valor")]
        public decimal Valor { get; set; }
    }
}
