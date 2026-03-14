using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class ScanSession
{
    public Guid SessionId { get; set; }
    public ScanType Type { get; set; }
    public ScanStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ScannedFiles { get; set; }
    public int ThreatsFound { get; set; }
    public int ErrorCount { get; set; }
    public List<ScanResult> Results { get; set; } = new();

    public List<ScanResult> Threats => Results.Where(r => r.IsThreat).ToList();

    public double ProgressPercent => TotalFiles > 0
        ? Math.Round((double)ScannedFiles / TotalFiles * 100, 2)
        : 0;
}
