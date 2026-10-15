namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>
    /// AI provider settings for dynamic data (OpenRouter / Ollama).
    /// API keys are stored AES-GCM encrypted, never in plaintext (Base44 DynamicDataSettings).
    /// </summary>
    public class DynamicDataSettings
    {
        public int Id { get; set; }
        public bool Enabled { get; set; }
        public string Provider { get; set; } = "openrouter";
        public string? OllamaBaseUrl { get; set; }
        public string? OllamaKeyEnc { get; set; }
        public string OllamaModel { get; set; } = "gpt-oss:120b";
        public string? OpenRouterKeyEnc { get; set; }
        public string OpenRouterModel { get; set; } = "google/gemma-4-26b-a4b-it:free";
    }
}
