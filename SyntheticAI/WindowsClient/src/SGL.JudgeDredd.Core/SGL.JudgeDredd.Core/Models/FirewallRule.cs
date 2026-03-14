using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class FirewallRule
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApplicationPath { get; set; }
    public FirewallAction Action { get; set; }
    public FirewallDirection Direction { get; set; }
    public FirewallProtocol Protocol { get; set; }
    public string? LocalPorts { get; set; }
    public string? RemotePorts { get; set; }
    public string? RemoteAddresses { get; set; }
    public bool Enabled { get; set; } = true;
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; }
}
