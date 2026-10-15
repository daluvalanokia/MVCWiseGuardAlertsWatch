using System.ComponentModel.DataAnnotations;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>A saved center override; found results are offset relative to the proxy (Base44 ProxyLocation).</summary>
    public class ProxyLocation
    {
        public int Id { get; set; }
        [Required, MaxLength(120)]
        public string Name { get; set; } = "";
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
