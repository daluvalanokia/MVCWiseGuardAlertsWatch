using System.Text;
using System.Text.Json;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>
    /// Dynamic data provider: lists models and answers prompts via OpenRouter or Ollama.
    /// Mirrors the Base44 dynamicDataModels / dynamicDataSearch functions.
    /// </summary>
    public class DynamicDataService
    {
        private readonly HttpClient _http;
        private readonly CryptoService _crypto;
        private readonly ILogger<DynamicDataService> _logger;

        public DynamicDataService(HttpClient http, CryptoService crypto, ILogger<DynamicDataService> logger)
        {
            _http = http;
            _crypto = crypto;
            _logger = logger;
        }

        private static string UpstreamMessage(JsonDocument? data, string raw)
        {
            if (data is not null)
            {
                if (data.RootElement.TryGetProperty("error", out var err))
                {
                    if (err.ValueKind == JsonValueKind.String) return err.GetString() ?? "";
                    if (err.ValueKind == JsonValueKind.Object &&
                        err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        return msg.GetString() ?? "";
                }
            }
            return raw.Length > 300 ? raw[..300] : raw;
        }

        private async Task<(JsonDocument? data, string raw, bool ok, int status)> SendAsync(
            HttpRequestMessage request)
        {
            using var resp = await _http.SendAsync(request);
            string raw = await resp.Content.ReadAsStringAsync();
            JsonDocument? data = null;
            try { data = JsonDocument.Parse(raw); } catch { /* non-JSON upstream body */ }
            return (data, raw, resp.IsSuccessStatusCode, (int)resp.StatusCode);
        }

        /// <summary>Lists available models for the given provider (mirrors dynamicDataModels).</summary>
        public async Task<(List<string> models, string? error)> GetModelsAsync(string provider, string apiKey)
        {
            try
            {
                if (provider == "ollama")
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                        return (new(), "Enter your Ollama API key to load available models.");
                    var req = new HttpRequestMessage(HttpMethod.Get, "https://ollama.com/api/tags");
                    req.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var (data, _, ok, status) = await SendAsync(req);
                    if (!ok)
                        return (new(), $"Ollama models request failed ({status}){(data is null ? "" : ": " + UpstreamMessage(data, ""))}");
                    var models = (data!.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array
                        ? arr.EnumerateArray()
                        : Enumerable.Empty<JsonElement>())
                        .Select(m => (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                                  ?? (m.TryGetProperty("model", out var m2) && m2.ValueKind == JsonValueKind.String ? m2.GetString() : null))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!)
                        .ToList();
                    return (models, null);
                }
                else
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        req.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var (data, _, ok, status) = await SendAsync(req);
                    if (!ok)
                        return (new(), $"OpenRouter models request failed ({status}){(data is null ? "" : ": " + UpstreamMessage(data, ""))}");
                    var models = (data!.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array
                        ? arr.EnumerateArray() : Enumerable.Empty<JsonElement>())
                        .Select(m => m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                        .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)
                        .ToList();
                    return (models, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetModelsAsync failed");
                return (new(), ex.Message);
            }
        }

        /// <summary>Sends a prompt (with search context) to the provider (mirrors dynamicDataSearch).</summary>
        public async Task<(string content, string? error)> SearchAsync(
            DynamicDataSettings settings, string apiKey, string? model, string prompt,
            string? location, List<string> names, int? radiusMiles)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return ("", "No provider API key configured. Add one in Settings.");
            if (string.IsNullOrWhiteSpace(prompt))
                return ("", "A prompt is required");

            var contextParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(location))
                contextParts.Add($"Location of interest: {location}");
            if (names is { Count: > 0 })
                contextParts.Add($"Names being searched near this location: {string.Join(", ", names)}");
            if (radiusMiles is > 0)
                contextParts.Add($"Search radius: up to {radiusMiles} miles");
            string userContent = contextParts.Count > 0
                ? $"{prompt}\n\n---\nRelevant search context:\n{string.Join("\n", contextParts)}"
                : prompt;

            bool isOllama = settings.Provider == "ollama";
            string providerName = isOllama ? "Ollama" : "OpenRouter";
            string url = isOllama ? "https://ollama.com/api/chat" : "https://openrouter.ai/api/v1/chat/completions";
            string useModel = !string.IsNullOrWhiteSpace(model) ? model!
                : isOllama ? settings.OllamaModel : settings.OpenRouterModel;

            var payload = new
            {
                model = useModel,
                messages = new[] { new { role = "user", content = userContent } },
                stream = false
            };

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                var (data, raw, ok, status) = await SendAsync(req);
                if (!ok)
                    return ("", $"{providerName} request failed ({status}){(data is null ? "" : ": " + UpstreamMessage(data, raw))}");

                string content = "";
                var root = data!.RootElement;
                if (isOllama)
                {
                    if (root.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        content = c.GetString() ?? "";
                }
                else if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in choices.EnumerateArray())
                        if (ch.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        { content = c.GetString() ?? ""; break; }
                }
                if (string.IsNullOrWhiteSpace(content))
                    return ("", $"{providerName} returned an empty response");
                return (content, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamicDataSearch failed");
                return ("", ex.Message);
            }
        }

        public string GetApiKey(DynamicDataSettings settings) =>
            settings.Provider == "ollama"
                ? _crypto.Decrypt(settings.OllamaKeyEnc)
                : _crypto.Decrypt(settings.OpenRouterKeyEnc);
    }
}
