namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>Global search parameters + optional custom location (Base44 AppSettings).</summary>
    public class AppSettings
    {
        public int Id { get; set; }
        public int SearchIntervalSeconds { get; set; } = 10;
        public int SearchRadiusStepMeters { get; set; } = 300;
        public int MaxRadiusMeters { get; set; } = 5000;
        public bool UseCustomLocation { get; set; }
        public double? CustomLat { get; set; }
        public double? CustomLon { get; set; }
        public string? CustomLocationLabel { get; set; }
    }
}
