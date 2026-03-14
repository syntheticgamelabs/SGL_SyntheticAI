using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class ThreatSignature
{
    public int Id { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? Md5Hash { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public DateTime FirstSeen { get; set; }
    public DateTime LastUpdated { get; set; }
}
