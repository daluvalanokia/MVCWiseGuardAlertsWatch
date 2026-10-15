using System.Globalization;
using System.Text.Json;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>
    /// Nominatim geocoding (mirrors the Base44 geocodeSearch / geocodeCityState
    /// functions) with a photon.komoot.io fallback: Nominatim aggressively
    /// rate-limits (HTTP 429), so when it is unavailable the same OSM data is
    /// served through Photon's API instead.
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
            var (doc, ok) = await TryGetJsonAsync(url);
            if (ok && doc is not null)
            {
                using (doc)
                {
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
                }
                return results;
            }

            // Nominatim unavailable (rate-limited / down): Photon fallback (same OSM data).
            string bbox = $"{lon - lonOffset},{lat - latOffset},{lon + lonOffset},{lat + latOffset}"; // minLon,minLat,maxLon,maxLat
            string photonUrl = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(query)}&limit=50&bbox={bbox}";
            var (pdoc, pok) = await TryGetJsonAsync(photonUrl);
            if (pok && pdoc is not null)
            {
                using (pdoc)
                {
                    foreach (var feature in pdoc.RootElement.GetProperty("features").EnumerateArray())
                    {
                        if (!feature.TryGetProperty("geometry", out var geom)) continue;
                        var coords = geom.GetProperty("coordinates");
                        if (coords.ValueKind != JsonValueKind.Array || coords.GetArrayLength() < 2) continue;
                        double resultLon = coords[0].GetDouble();
                        double resultLat = coords[1].GetDouble();
                        if (Haversine(lat, lon, resultLat, resultLon) > radiusMeters) continue;

                        var props = feature.TryGetProperty("properties", out var pr) && pr.ValueKind == JsonValueKind.Object ? pr : default;
                        string name = GetStringProp(props, "name");
                        string street = GetStringProp(props, "street");
                        string housenumber = GetStringProp(props, "housenumber");
                        string city = GetStringProp(props, "city");
                        string state = GetStringProp(props, "state");
                        string postcode = GetStringProp(props, "postcode");
                        string country = GetStringProp(props, "country");
                        string displayName = string.Join(", ", new[] { name, street, housenumber, city, state, postcode, country }.Where(s => !string.IsNullOrEmpty(s)));
                        var address = new Dictionary<string, string>();
                        foreach (var p in props.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.String) address[p.Name] = p.Value.GetString() ?? "";

                        results.Add(new SearchResult
                        {
                            Position = new[] { resultLat, resultLon },
                            Name = string.IsNullOrEmpty(name) ? displayName.Split(',')[0].Trim() : name,
                            DisplayName = displayName,
                            Type = GetStringProp(props, "osm_value"),
                            Address = address.Count > 0 ? address : null
                        });
                    }
                }
            }
            return results;
        }

        public async Task<List<CityStateResult>> GeocodeCityStateAsync(string query)
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=10&addressdetails=1";
            var results = new List<CityStateResult>();
            var (doc, ok) = await TryGetJsonAsync(url);
            if (ok && doc is not null)
            {
                using (doc)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        string displayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
                        results.Add(new CityStateResult
                        {
                            Position = new[] { ParseCoordinate(item, "lat"), ParseCoordinate(item, "lon") },
                            Label = displayName
                        });
                    }
                }
                return results;
            }

            // Photon fallback
            string photonUrl = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(query)}&limit=10";
            var (pdoc, pok) = await TryGetJsonAsync(photonUrl);
            if (pok && pdoc is not null)
            {
                using (pdoc)
                {
                    foreach (var feature in pdoc.RootElement.GetProperty("features").EnumerateArray())
                    {
                        if (!feature.TryGetProperty("geometry", out var geom)) continue;
                        var coords = geom.GetProperty("coordinates");
                        if (coords.ValueKind != JsonValueKind.Array || coords.GetArrayLength() < 2) continue;
                        double resultLon = coords[0].GetDouble();
                        double resultLat = coords[1].GetDouble();
                        var props = feature.TryGetProperty("properties", out var pr) && pr.ValueKind == JsonValueKind.Object ? pr : default;
                        string label = string.Join(", ", new[]
                        {
                            GetStringProp(props, "name"), GetStringProp(props, "city"),
                            GetStringProp(props, "state"), GetStringProp(props, "country")
                        }.Where(s => !string.IsNullOrEmpty(s)));
                        results.Add(new CityStateResult
                        {
                            Position = new[] { resultLat, resultLon },
                            Label = label
                        });
                    }
                }
            }
            return results;
        }

        /// <summary>GET JSON; a non-2xx upstream (e.g. Nominatim 429) returns (null, false) instead of throwing.</summary>
        private async Task<(JsonDocument? Doc, bool Ok)> TryGetJsonAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", _userAgent);
                request.Headers.Add("Accept-Language", "en");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[geocode] upstream {new Uri(url).Host} returned {(int)response.StatusCode}");
                    return (null, false);
                }
                var content = await response.Content.ReadAsStringAsync();
                return (JsonDocument.Parse(content), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[geocode] upstream {new Uri(url).Host} failed: {ex.Message}");
                return (null, false);
            }
        }

        private static string GetStringProp(JsonElement props, string name)
            => props.ValueKind == JsonValueKind.Object && props.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

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
