namespace SGL.JudgeDredd.Core.Models;

public class FirewallPreset
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<FirewallRule> Rules { get; set; } = new();
    public bool IsActive { get; set; }
}
