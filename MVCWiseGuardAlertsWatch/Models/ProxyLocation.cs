using System.ComponentModel.DataAnnotations;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>A saved center override shown with its full geocoded display name
    /// (Base44 ProxyLocation, e.g. "Tallada, Thallada mandal, Khammam, Telangana, India").</summary>
    public class ProxyLocation
    {
        public int Id { get; set; }
        [Required, MaxLength(120)]
        public string Name { get; set; } = "";
        [MaxLength(300)]
        public string? DisplayName { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
