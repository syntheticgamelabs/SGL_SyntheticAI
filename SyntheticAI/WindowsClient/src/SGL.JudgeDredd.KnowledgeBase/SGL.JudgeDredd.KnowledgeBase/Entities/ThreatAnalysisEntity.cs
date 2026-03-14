namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class ThreatAnalysisEntity
{
    public int Id { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string SystemDataSnapshot { get; set; } = string.Empty;
    public string LlmAnalysisResult { get; set; } = string.Empty;
    public string ThreatsSummary { get; set; } = string.Empty;
    public int SeverityScore { get; set; }
}
