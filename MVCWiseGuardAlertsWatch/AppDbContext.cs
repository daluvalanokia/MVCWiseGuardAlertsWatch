using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch
{
    /// <summary>
    /// SQLite-backed database for the WiseGuardAlertsWatch modules, mirroring the
    /// Base44 entities: SearchLocation, ProxyLocation, AppSettings, RadarConfig,
    /// GuardVideo, GuardSettings, DynamicDataSettings, User.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<SearchLocation> SearchLocations { get; set; } = null!;
        public DbSet<ProxyLocation> ProxyLocations { get; set; } = null!;
        public DbSet<AppSettings> AppSettings { get; set; } = null!;
        public DbSet<RadarConfig> RadarConfigs { get; set; } = null!;
        public DbSet<GuardVideo> GuardVideos { get; set; } = null!;
        public DbSet<GuardSettings> GuardSettings { get; set; } = null!;
        public DbSet<DynamicDataSettings> DynamicDataSettings { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppSettings>().HasData(new AppSettings
            {
                Id = 1,
                SearchIntervalSeconds = 10,
                SearchRadiusStepMeters = 300,
                MaxRadiusMeters = 5000,
                UseCustomLocation = false
            });

            modelBuilder.Entity<GuardSettings>().HasData(new GuardSettings
            {
                Id = 1,
                TrackedWordsJson = "[]",
                CumulativeCountsJson = "{}"
            });

            modelBuilder.Entity<DynamicDataSettings>().HasData(new DynamicDataSettings
            {
                Id = 1,
                Enabled = false,
                Provider = "openrouter",
                OllamaModel = "gpt-oss:120b",
                OpenRouterModel = "google/gemma-4-26b-a4b-it:free"
            });

            modelBuilder.Entity<SearchLocation>().HasData(
                new SearchLocation { Id = 1, Name = "Temple", Color = "blue", Icon = "landmark", Enabled = true },
                new SearchLocation { Id = 2, Name = "Pharmacy", Color = "green", Icon = "pill", Enabled = true },
                new SearchLocation { Id = 3, Name = "Restaurant", Color = "amber", Icon = "utensils", Enabled = true }
            );
        }
    }
}
