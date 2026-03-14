namespace SGL.JudgeDredd.Core.Models;

public class NetworkConnection
{
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int OwningProcessId { get; set; }
    public string? ProcessName { get; set; }
}
