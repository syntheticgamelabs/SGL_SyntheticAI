namespace SGL.JudgeDredd.Core.Events;

using SGL.JudgeDredd.Core.Models;

public class ThreatDetectedEvent : EventArgs
{
    public ScanResult Result { get; init; } = null!;
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
