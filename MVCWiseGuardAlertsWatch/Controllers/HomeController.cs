using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch.Models;
using MVCWiseGuardAlertsWatch.ViewModels;

namespace MVCWiseGuardAlertsWatch.Controllers
{
    /// <summary>Dashboard: rotating instance panels (5 radar + WiseGuard) like the React Home page.</summary>
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db) => _db = db;

        public const int PanelCount = 6; // instances 1-5 radar maps, instance 6 WiseGuard

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                Settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync() ?? new AppSettings(),
                Locations = await _db.SearchLocations.AsNoTracking().OrderBy(l => l.Id).ToListAsync(),
                Proxies = await _db.ProxyLocations.AsNoTracking().OrderBy(p => p.Id).ToListAsync(),
                RadarConfigs = await _db.RadarConfigs.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(),
                DynamicData = await _db.DynamicDataSettings.AsNoTracking().FirstOrDefaultAsync() ?? new DynamicDataSettings()
            };
            return View(model);
        }

        [Route("/Home/Error")]
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Error() => View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
