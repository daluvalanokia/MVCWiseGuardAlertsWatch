using System.ComponentModel.DataAnnotations;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>A search name with an assigned color and marker icon (Base44 SearchLocation).</summary>
    public class SearchLocation
    {
        public int Id { get; set; }
        [Required, MaxLength(80)]
        public string Name { get; set; } = "";
        [MaxLength(20)]
        public string Color { get; set; } = "blue";
        [MaxLength(30)]
        public string Icon { get; set; } = "map-pin";
        public bool Enabled { get; set; } = true;
    }
}
