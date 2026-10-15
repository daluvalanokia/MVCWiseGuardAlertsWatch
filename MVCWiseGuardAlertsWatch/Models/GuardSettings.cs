using System.Text.Json;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>WiseGuard panel state: tracked words and cumulative word repeat counts.</summary>
    public class GuardSettings
    {
        public int Id { get; set; }
        public string TrackedWordsJson { get; set; } = "[]";
        public string CumulativeCountsJson { get; set; } = "{}";

        public List<string> GetTrackedWords() =>
            JsonSerializer.Deserialize<List<string>>(TrackedWordsJson) ?? new();
        public Dictionary<string, int> GetCumulativeCounts() =>
            JsonSerializer.Deserialize<Dictionary<string, int>>(CumulativeCountsJson) ?? new();
    }
}
