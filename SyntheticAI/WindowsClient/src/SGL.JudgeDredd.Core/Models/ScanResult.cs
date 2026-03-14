using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class ScanResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public bool IsThreat { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string? ThreatName { get; set; }
    public DetectionMethod DetectionMethod { get; set; }
    public double HeuristicScore { get; set; }
    public string? LlmVerdict { get; set; }
    public string? LlmReasoning { get; set; }
    public DateTime ScannedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
}
