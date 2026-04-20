using proyectoprogra.Models.Hacienda;
using System.Text.Json;

namespace proyectoprogra.Services
{
    public class HaciendaApiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<HaciendaApiService> _logger;
        private static readonly JsonSerializerOptions _json =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public HaciendaApiService(HttpClient http, ILogger<HaciendaApiService> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>Looks up a taxpayer by Costa Rica national ID (cédula).</summary>
        public async Task<ContribuyenteDto?> GetContribuyenteAsync(string identificacion)
        {
            var url = $"/fe/ae?identificacion={Uri.EscapeDataString(identificacion)}";
            try
            {
                _logger.LogInformation("Hacienda contribuyente request: {Url}", _http.BaseAddress + url.TrimStart('/'));
                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Hacienda contribuyente returned {StatusCode}. Body: {Body}",
                        (int)response.StatusCode, body);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Hacienda contribuyente response: {Json}", json);
                return JsonSerializer.Deserialize<ContribuyenteDto>(json, _json);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Hacienda contribuyente request timed out for identificacion={Id}", identificacion);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Hacienda contribuyente HTTP error for identificacion={Id}", identificacion);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hacienda contribuyente unexpected error for identificacion={Id}", identificacion);
                return null;
            }
        }

        /// <summary>Searches the CABYS catalogue for goods/services codes.</summary>
        public async Task<CabysResultDto?> GetCabysAsync(string query)
        {
            var url = query.Length == 13 && query.All(char.IsDigit)
                ? $"/fe/cabys?codigo={Uri.EscapeDataString(query)}"
                : $"/fe/cabys?q={Uri.EscapeDataString(query)}";
            try
            {
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Hacienda CABYS returned {StatusCode}. Body: {Body}",
                        (int)response.StatusCode, body);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CabysResultDto>(json, _json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hacienda CABYS error for query={Query}", query);
                return null;
            }
        }

        /// <summary>Returns the current USD → CRC exchange rate from Banco Central.</summary>
        public async Task<TipoCambioDto?> GetTipoCambioDolarAsync()
        {
            try
            {
                var response = await _http.GetAsync("/indicadores/tc/dolar");
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Hacienda tipo de cambio returned {StatusCode}. Body: {Body}",
                        (int)response.StatusCode, body);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TipoCambioDto>(json, _json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hacienda tipo de cambio error");
                return null;
            }
        }
    }
}
