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
    public class GuardController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TranscribeService _transcribe;

        public GuardController(AppDbContext db, TranscribeService transcribe)
        {
            _db = db;
            _transcribe = transcribe;
        }

        /// <summary>POST /api/guard/transcribe — mirrors the Base44 guardTranscribe function (WAV upload).</summary>
        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe(IFormFile audio)
        {
            if (audio is null || audio.Length == 0)
                return BadRequest(new { error = "audio segment is required" });
            try
            {
                using var stream = audio.OpenReadStream();
                var (text, error) = await _transcribe.TranscribeAsync(stream, audio.FileName);
                if (error is not null) return StatusCode(502, new { error });
                return Ok(new { text });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("state")]
        public async Task<IActionResult> GetState()
        {
            var settings = await _db.GuardSettings.AsNoTracking().FirstOrDefaultAsync() ?? new GuardSettings();
            var videos = await _db.GuardVideos.AsNoTracking().OrderBy(v => v.Id).ToListAsync();
            return Ok(new
            {
                trackedWords = settings.GetTrackedWords(),
                cumulativeCounts = settings.GetCumulativeCounts(),
                videos
            });
        }

        [HttpPost("state")]
        public async Task<IActionResult> SaveState([FromBody] GuardStateRequest req)
        {
            var settings = await _db.GuardSettings.FirstOrDefaultAsync();
            if (settings is null) { settings = new GuardSettings(); _db.GuardSettings.Add(settings); }
            settings.TrackedWordsJson = System.Text.Json.JsonSerializer.Serialize(req.TrackedWords ?? new());
            settings.CumulativeCountsJson = System.Text.Json.JsonSerializer.Serialize(req.CumulativeCounts ?? new());
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpPost("videos")]
        public async Task<IActionResult> AddVideo([FromBody] AddVideoRequest req)
        {
            string? ytId = null;
            if (!string.IsNullOrWhiteSpace(req.Url))
            {
                var match = System.Text.RegularExpressions.Regex.Match(req.Url,
                    @"(?:youtu\.be/|youtube\.com/(?:watch\?v=|embed/|shorts/))([\w-]{11})");
                if (match.Success) ytId = match.Groups[1].Value;
            }
            var video = new GuardVideo
            {
                Name = req.Name ?? "Video",
                Url = req.Url,
                YoutubeId = ytId,
                StoragePath = req.StoragePath,
                Selected = false
            };
            _db.GuardVideos.Add(video);
            await _db.SaveChangesAsync();
            return Ok(video);
        }

        [HttpPost("videos/{id}/select")]
        public async Task<IActionResult> SelectVideo(int id)
        {
            var all = await _db.GuardVideos.ToListAsync();
            foreach (var v in all) v.Selected = v.Id == id;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpDelete("videos/{id}")]
        public async Task<IActionResult> DeleteVideo(int id)
        {
            var video = await _db.GuardVideos.FindAsync(id);
            if (video is null) return NotFound(new { error = "Video not found" });
            if (!string.IsNullOrEmpty(video.StoragePath))
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", video.StoragePath.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            _db.GuardVideos.Remove(video);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }

    public class GuardStateRequest
    {
        public List<string> TrackedWords { get; set; } = new();
        public Dictionary<string, int> CumulativeCounts { get; set; } = new();
    }

    public class AddVideoRequest
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? StoragePath { get; set; }
    }
}
