using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch.Models;
using MVCWiseGuardAlertsWatch.Services;

namespace MVCWiseGuardAlertsWatch.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DynamicDataController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly DynamicDataService _dynamic;

        public DynamicDataController(AppDbContext db, DynamicDataService dynamic)
        {
            _db = db;
            _dynamic = dynamic;
        }

        private async Task<DynamicDataSettings> GetSettingsAsync() =>
            await _db.DynamicDataSettings.AsNoTracking().FirstAsync();

        /// <summary>POST /api/dynamicdata/models — mirrors the Base44 dynamicDataModels function.</summary>
        [HttpPost("models")]
        public async Task<IActionResult> Models([FromBody] ModelsRequest req)
        {
            var settings = await GetSettingsAsync();
            string apiKey = string.IsNullOrWhiteSpace(req.ApiKey) ? _dynamic.GetApiKey(settings) : req.ApiKey;
            var (models, error) = await _dynamic.GetModelsAsync(req.Provider ?? settings.Provider, apiKey);
            if (error is not null) return StatusCode(502, new { error });
            return Ok(new { models });
        }

        /// <summary>POST /api/dynamicdata/search — mirrors the Base44 dynamicDataSearch function.</summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] SearchRequest req)
        {
            var settings = await GetSettingsAsync();
            string apiKey = _dynamic.GetApiKey(settings);
            var (content, error) = await _dynamic.SearchAsync(
                settings, apiKey, req.Model, req.Prompt ?? "",
                req.Context?.Location, req.Context?.Names ?? new(), req.Context?.RadiusMiles);
            if (error is not null) return StatusCode(502, new { error });
            return Ok(new { content });
        }
    }

    public class ModelsRequest
    {
        public string? Provider { get; set; }
        public string? ApiKey { get; set; }
    }

    public class SearchRequest
    {
        public string? Model { get; set; }
        public string? Prompt { get; set; }
        public SearchContext? Context { get; set; }
    }

    public class SearchContext
    {
        public string? Location { get; set; }
        public List<string> Names { get; set; } = new();
        public int? RadiusMiles { get; set; }
    }
}
