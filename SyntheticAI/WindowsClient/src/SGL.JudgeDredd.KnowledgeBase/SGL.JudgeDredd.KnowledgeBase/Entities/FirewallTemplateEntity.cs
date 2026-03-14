namespace SGL.JudgeDredd.KnowledgeBase.Entities;

public class FirewallTemplateEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RulesJson { get; set; } = "[]";  // Serialized FirewallRule collection
    public bool IsBuiltIn { get; set; }
}
