namespace SGL.JudgeDredd.Core.Models;

public class ThreatAnalysisResult
{
    public string Verdict { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string ThreatType { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
