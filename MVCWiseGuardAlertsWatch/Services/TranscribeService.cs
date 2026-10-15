using System.Text;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>
    /// Speech-to-text for the WiseGuard panel via a configurable OpenAI
    /// Whisper-compatible endpoint (appsettings: Transcription:*).
    /// Mirrors the Base44 guardTranscribe function.
    /// </summary>
    public class TranscribeService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<TranscribeService> _logger;

        public TranscribeService(HttpClient http, IConfiguration config, ILogger<TranscribeService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task<(string text, string? error)> TranscribeAsync(Stream wav, string fileName)
        {
            string? baseUrl = _config["Transcription:BaseUrl"];
            string? apiKey = _config["Transcription:ApiKey"];
            string model = _config["Transcription:Model"] ?? "whisper-1";
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
                return ("", "Transcription is not configured. Set Transcription:BaseUrl and Transcription:ApiKey in appsettings.json.");

            try
            {
                using var form = new MultipartFormDataContent();
                wav.Position = 0;
                var fileContent = new StreamContent(wav);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(model), "model");
                form.Add(new StringContent("telugu"), "language");

                var req = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/audio/transcriptions")
                { Content = form };
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var resp = await _http.SendAsync(req);
                string raw = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return ("", $"Transcription failed ({(int)resp.StatusCode}): {(raw.Length > 300 ? raw[..300] : raw)}");

                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                string text = doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String
                    ? t.GetString() ?? "" : "";
                return (text, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TranscribeAsync failed");
                return ("", ex.Message);
            }
        }
    }
}
