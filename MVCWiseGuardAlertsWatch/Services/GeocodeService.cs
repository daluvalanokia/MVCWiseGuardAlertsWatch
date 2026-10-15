using System.Globalization;
using System.Text.Json;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>
    /// Nominatim geocoding — mirrors the Base44 geocodeSearch / geocodeCityState functions.
    /// </summary>
    public class GeocodeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _userAgent;

        public GeocodeService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _userAgent = config["Nominatim:UserAgent"] ?? "MVCWiseGuardAlertsWatch/1.0";
        }

        public async Task<List<SearchResult>> SearchAsync(string query, double lat, double lon, int radiusMeters)
        {
            double viewboxRadius = Math.Max(radiusMeters, 50000);
            double latOffset = viewboxRadius / 111320.0;
            double lonOffset = viewboxRadius / (111320.0 * Math.Cos(lat * Math.PI / 180.0));
            string viewbox = $"{lon - lonOffset},{lat + latOffset},{lon + lonOffset},{lat - latOffset}";
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&bounded=1&viewbox={viewbox}&limit=50&addressdetails=1";

            var results = new List<SearchResult>();
            using var doc = await GetJsonAsync(url);
            if (doc is null) return results;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                double resultLat = ParseCoordinate(item, "lat");
                double resultLon = ParseCoordinate(item, "lon");
                if (Haversine(lat, lon, resultLat, resultLon) > radiusMeters) continue;

                string displayName = item.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String
                    ? dn.GetString() ?? "" : "";
                results.Add(new SearchResult
                {
                    Position = new[] { resultLat, resultLon },
                    Name = displayName.Split(',')[0].Trim(),
                    DisplayName = displayName,
                    Type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
                    Address = ParseAddress(item)
                });
            }
            return results;
        }

        public async Task<List<CityStateResult>> GeocodeCityStateAsync(string query)
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=10&addressdetails=1";
            var results = new List<CityStateResult>();
            using var doc = await GetJsonAsync(url);
            if (doc is null) return results;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string displayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
                results.Add(new CityStateResult
                {
                    Position = new[] { ParseCoordinate(item, "lat"), ParseCoordinate(item, "lon") },
                    Label = displayName
                });
            }
            return results;
        }

        private async Task<JsonDocument?> GetJsonAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", _userAgent);
            request.Headers.Add("Accept-Language", "en");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content);
        }

        private static double ParseCoordinate(JsonElement item, string prop)
        {
            if (item.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String &&
                double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            return 0;
        }

        private static Dictionary<string, string>? ParseAddress(JsonElement item)
        {
            if (!item.TryGetProperty("address", out var addr) || addr.ValueKind != JsonValueKind.Object)
                return null;
            var dict = new Dictionary<string, string>();
            foreach (var p in addr.EnumerateObject())
                dict[p.Name] = p.Value.ToString();
            return dict;
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }
    }
}
