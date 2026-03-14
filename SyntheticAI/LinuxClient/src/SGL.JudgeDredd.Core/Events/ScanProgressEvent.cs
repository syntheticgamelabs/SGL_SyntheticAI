namespace SGL.JudgeDredd.Core.Events;

public class ScanProgressEvent : EventArgs
{
    public int TotalFiles { get; init; }
    public int ScannedFiles { get; init; }
    public int ThreatsFound { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public int ErrorCount { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    public double ProgressPercent => TotalFiles > 0 ? (double)ScannedFiles / TotalFiles * 100.0 : 0.0;
}
