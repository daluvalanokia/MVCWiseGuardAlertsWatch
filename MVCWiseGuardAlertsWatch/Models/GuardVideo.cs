using System.ComponentModel.DataAnnotations;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>A guard video (YouTube link or uploaded file) shown in the WiseGuard panel.</summary>
    public class GuardVideo
    {
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string Name { get; set; } = "";
        [MaxLength(500)]
        public string? Url { get; set; }
        [MaxLength(40)]
        public string? YoutubeId { get; set; }
        public bool Selected { get; set; }
        [MaxLength(500)]
        public string? StoragePath { get; set; }
    }
}
