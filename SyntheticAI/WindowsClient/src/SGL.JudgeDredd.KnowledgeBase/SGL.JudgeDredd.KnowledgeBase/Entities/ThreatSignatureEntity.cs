namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class ThreatSignatureEntity
{
    public int Id { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? Md5Hash { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public int Severity { get; set; }  // Maps to ThreatSeverity enum
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = "[]";  // JSON array stored as string
    public DateTime FirstSeen { get; set; }
    public DateTime LastUpdated { get; set; }

    public ICollection<SolutionEntity> Solutions { get; set; } = new List<SolutionEntity>();
}
