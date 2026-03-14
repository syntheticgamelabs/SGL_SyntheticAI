namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Represents a single scan result item.
/// </summary>
public class ScanResultItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ThreatName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Unknown";
    public bool IsThreat { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}
