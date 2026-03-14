namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class SystemPromptEntity
{
    public int Id { get; set; }
    public string ContextKey { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
}
