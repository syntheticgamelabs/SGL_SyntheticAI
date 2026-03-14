namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class SolutionEntity
{
    public int Id { get; set; }
    public int ThreatId { get; set; }
    public int StepOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AutomatedAction { get; set; }
    public string? Script { get; set; }

    // Navigation property
    public ThreatSignatureEntity Threat { get; set; } = null!;
}
