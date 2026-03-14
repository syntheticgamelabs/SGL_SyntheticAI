namespace SGL.JudgeDredd.Mobile.Models;

public class TelemetryItem
{
    public string PackageName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string Category { get; set; } = "Unknown";
    public string Description { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool IsSystemApp { get; set; }
    public List<string> DangerousPermissions { get; set; } = new();
    public string ThreatLevel { get; set; } = "Low";
    public bool CanDisable { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
    public long MemoryUsageKb { get; set; }
    public string DataCollectionType { get; set; } = string.Empty;
}
