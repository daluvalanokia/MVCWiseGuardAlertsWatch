using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCWiseGuardAlertsWatch.Models;
using MVCWiseGuardAlertsWatch.Services;

namespace MVCWiseGuardAlertsWatch.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GeocodeController : ControllerBase
    {
        private readonly GeocodeService _geocode;

        public GeocodeController(GeocodeService geocode) => _geocode = geocode;

        /// <summary>POST /api/geocode/search — mirrors the Base44 geocodeSearch function.</summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] GeocodeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Query))
                return BadRequest(new { error = "Query is required" });
            try
            {
                var results = await _geocode.SearchAsync(req.Query, req.Lat, req.Lon, req.Radius);
                return Ok(new GeocodeResponse { Results = results, Total = results.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>POST /api/geocode/citystate — mirrors the Base44 geocodeCityState function.</summary>
        [HttpPost("citystate")]
        public async Task<IActionResult> CityState([FromBody] CityStateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Query) || req.Query.Trim().Length < 2)
                return Ok(new { results = new List<CityStateResult>() });
            try
            {
                var results = await _geocode.GeocodeCityStateAsync(req.Query.Trim());
                return Ok(new { results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class GeocodeRequest
    {
        public string Query { get; set; } = "";
        public double Lat { get; set; }
        public double Lon { get; set; }
        public int Radius { get; set; }
    }

    public class CityStateRequest
    {
        public string Query { get; set; } = "";
    }
}
