namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class ScanHistoryEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int ScanType { get; set; }  // Maps to ScanType enum
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ThreatsFound { get; set; }
    public string ResultsJson { get; set; } = "{}";  // Serialized scan summary
}
