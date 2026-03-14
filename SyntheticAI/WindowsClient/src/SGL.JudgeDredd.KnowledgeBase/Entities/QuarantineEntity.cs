namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class QuarantineEntity
{
    public int Id { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
    public string QuarantinePath { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public string? ThreatName { get; set; }
    public DateTime QuarantinedAt { get; set; }
    public DateTime? RestoredAt { get; set; }
}
