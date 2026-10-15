using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>
    /// A saved dashboard configuration: per-instance selections (locationIds, proxyId, loop)
    /// stored as JSON, mirroring the Base44 RadarConfig entity.
    /// </summary>
    public class RadarConfig
    {
        public int Id { get; set; }
        [Required, MaxLength(120)]
        public string Name { get; set; } = "";
        public string InstancesJson { get; set; } = "[]";

        public List<RadarInstance> GetInstances() =>
            JsonSerializer.Deserialize<List<RadarInstance>>(InstancesJson) ?? new();
        public void SetInstances(List<RadarInstance> instances) =>
            InstancesJson = JsonSerializer.Serialize(instances);
    }

    public class RadarInstance
    {
        public List<string> Names { get; set; } = new();
        public List<int> LocationIds { get; set; } = new(); // legacy (saved-name mode)
        public int? ProxyId { get; set; }
        public bool Loop { get; set; }
    }
}
