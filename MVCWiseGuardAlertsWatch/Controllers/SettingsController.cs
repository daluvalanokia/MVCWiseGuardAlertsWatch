using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch.Models;
using MVCWiseGuardAlertsWatch.Services;
using MVCWiseGuardAlertsWatch.ViewModels;

namespace MVCWiseGuardAlertsWatch.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly GeocodeService _geocode;
        private readonly CryptoService _crypto;
        private readonly DynamicDataService _dynamic;

        public SettingsController(AppDbContext db, GeocodeService geocode, CryptoService crypto, DynamicDataService dynamic)
        {
            _db = db;
            _geocode = geocode;
            _crypto = crypto;
            _dynamic = dynamic;
        }

        public async Task<IActionResult> Index(string tab = "locations")
        {
            var model = new SettingsViewModel
            {
                ActiveTab = tab,
                Settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync() ?? new AppSettings(),
                Locations = await _db.SearchLocations.AsNoTracking().OrderBy(l => l.Id).ToListAsync(),
                Proxies = await _db.ProxyLocations.AsNoTracking().OrderBy(p => p.Id).ToListAsync(),
                RadarConfigs = await _db.RadarConfigs.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(),
                Guard = await _db.GuardSettings.AsNoTracking().FirstOrDefaultAsync() ?? new GuardSettings(),
                Videos = await _db.GuardVideos.AsNoTracking().OrderBy(v => v.Id).ToListAsync(),
                DynamicData = await _db.DynamicDataSettings.AsNoTracking().FirstOrDefaultAsync() ?? new DynamicDataSettings()
            };
            return View(model);
        }

        // ---------- Locations (search names) ----------
        [HttpPost]
        public async Task<IActionResult> AddLocation(SearchLocation model)
        {
            if (!string.IsNullOrWhiteSpace(model.Name))
            {
                _db.SearchLocations.Add(new SearchLocation
                {
                    Name = model.Name.Trim(), Color = model.Color, Icon = model.Icon, Enabled = true
                });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { tab = "locations" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLocationColorIcon(int id, string color, string icon)
        {
            var loc = await _db.SearchLocations.FindAsync(id);
            if (loc is not null) { loc.Color = color; loc.Icon = icon; await _db.SaveChangesAsync(); }
            return RedirectToAction("Index", new { tab = "locations" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var loc = await _db.SearchLocations.FindAsync(id);
            if (loc is not null) { _db.SearchLocations.Remove(loc); await _db.SaveChangesAsync(); }
            return RedirectToAction("Index", new { tab = "locations" });
        }

        // ---------- Proxy locations ----------
        [HttpPost]
        public async Task<IActionResult> AddProxyLocation([FromBody] AddProxyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required" });
            _db.ProxyLocations.Add(new ProxyLocation
            {
                Name = req.Name.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Name.Trim() : req.DisplayName.Trim(),
                Lat = req.Lat, Lon = req.Lon
            });
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProxyLocation(int id)
        {
            var proxy = await _db.ProxyLocations.FindAsync(id);
            if (proxy is not null) { _db.ProxyLocations.Remove(proxy); await _db.SaveChangesAsync(); }
            return RedirectToAction("Index", new { tab = "proxy" });
        }

        // ---------- App settings + custom location ----------
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(AppSettings model)
        {
            var settings = await _db.AppSettings.FirstOrDefaultAsync();
            if (settings is not null)
            {
                settings.SearchIntervalSeconds = Math.Max(1, model.SearchIntervalSeconds);
                settings.SearchRadiusStepMeters = Math.Max(50, model.SearchRadiusStepMeters);
                settings.MaxRadiusMeters = Math.Max(model.SearchRadiusStepMeters, model.MaxRadiusMeters);
                settings.UseCustomLocation = model.UseCustomLocation;
                settings.CustomLat = model.CustomLat;
                settings.CustomLon = model.CustomLon;
                settings.CustomLocationLabel = model.CustomLocationLabel;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { tab = "app" });
        }

        // ---------- Dynamic data provider ----------
        [HttpPost]
        public async Task<IActionResult> UpdateDynamicData(DynamicDataSettings model, string? openrouterKey, string? ollamaKey)
        {
            var settings = await _db.DynamicDataSettings.FirstOrDefaultAsync();
            if (settings is null) { settings = new DynamicDataSettings(); _db.DynamicDataSettings.Add(settings); }
            settings.Enabled = model.Enabled;
            settings.Provider = model.Provider == "ollama" ? "ollama" : "openrouter";
            settings.OllamaBaseUrl = model.OllamaBaseUrl;
            settings.OllamaModel = model.OllamaModel;
            settings.OpenRouterModel = model.OpenRouterModel;
            if (!string.IsNullOrWhiteSpace(openrouterKey)) settings.OpenRouterKeyEnc = _crypto.Encrypt(openrouterKey);
            if (!string.IsNullOrWhiteSpace(ollamaKey)) settings.OllamaKeyEnc = _crypto.Encrypt(ollamaKey);
            await _db.SaveChangesAsync();
            return RedirectToAction("Index", new { tab = "dynamic" });
        }
    }

    public class AddProxyRequest
    {
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
