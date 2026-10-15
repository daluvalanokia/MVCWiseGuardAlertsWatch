using System.Text.Json.Serialization;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>Nominatim search result DTO (mirrors Base44 geocodeSearch response shape).</summary>
    public class SearchResult
    {
        [JsonPropertyName("position")]
        public double[] Position { get; set; } = new double[2];
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("address")]
        public Dictionary<string, string>? Address { get; set; }
    }

    public class GeocodeResponse
    {
        [JsonPropertyName("results")]
        public List<SearchResult> Results { get; set; } = new();
        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class CityStateResult
    {
        [JsonPropertyName("position")]
        public double[] Position { get; set; } = new double[2];
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";
    }
}
