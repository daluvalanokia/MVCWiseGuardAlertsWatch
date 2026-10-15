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

            modelBuilder.Entity<ProxyLocation>().HasData(
                new ProxyLocation { Id = 1, Name = "Tallada", DisplayName = "Tallada, Thallada mandal, Khammam, Telangana, 507167, India", Lat = 17.2181818, Lon = 80.4199516 },
                new ProxyLocation { Id = 2, Name = "Technology Park", DisplayName = "Technology Park, Lake Mary, Seminole County, Florida, 32795, United States", Lat = 28.7366409, Lon = -81.3623939 },
                new ProxyLocation { Id = 3, Name = "Choppadandi", DisplayName = "Choppadandi, Choppadandi mandal, Karimnagar, Telangana, 557654, India", Lat = 18.5773287, Lon = 79.1675524 },
                new ProxyLocation { Id = 4, Name = "Agali", DisplayName = "Agali, Mannarkad, Palakkad, Kerala, 678581, India", Lat = 11.1057095, Lon = 76.6547629 },
                new ProxyLocation { Id = 5, Name = "Venice", DisplayName = "Venice, Venezia, Veneto, 30121-30176, Italy", Lat = 45.4371908, Lon = 12.3345898 }
            );

            modelBuilder.Entity<SearchLocation>().HasData(
                new SearchLocation { Id = 1, Name = "Temple", Color = "blue", Icon = "landmark", Enabled = true },
                new SearchLocation { Id = 2, Name = "Pharmacy", Color = "green", Icon = "pill", Enabled = true },
                new SearchLocation { Id = 3, Name = "Restaurant", Color = "amber", Icon = "utensils", Enabled = true }
            );
        }
    }
}
