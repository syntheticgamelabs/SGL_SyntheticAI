using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class SecurityAlert
{
    public Guid AlertId { get; set; }
    public AlertCategory Category { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
    public string? FilePath { get; set; }
    public string? RegistryKey { get; set; }
    public string? RemoteAddress { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public string? UserAction { get; set; }
}
