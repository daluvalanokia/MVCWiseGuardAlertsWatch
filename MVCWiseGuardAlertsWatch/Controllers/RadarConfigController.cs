using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RadarConfigController : ControllerBase
    {
        private readonly AppDbContext _db;

        public RadarConfigController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var configs = await _db.RadarConfigs.AsNoTracking()
                .OrderByDescending(c => c.Id).ToListAsync();
            return Ok(configs);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var config = await _db.RadarConfigs.FindAsync(id);
            return config is null ? NotFound(new { error = "Radar config not found" }) : Ok(config);
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] RadarConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Name))
                return BadRequest(new { error = "Config name is required" });

            if (config.Id == 0)
            {
                _db.RadarConfigs.Add(config);
            }
            else
            {
                var existing = await _db.RadarConfigs.FindAsync(config.Id);
                if (existing is null) return NotFound(new { error = "Radar config not found" });
                existing.Name = config.Name;
                existing.InstancesJson = config.InstancesJson;
            }
            await _db.SaveChangesAsync();
            return Ok(config);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var config = await _db.RadarConfigs.FindAsync(id);
            if (config is null) return NotFound(new { error = "Radar config not found" });
            _db.RadarConfigs.Remove(config);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }
}
