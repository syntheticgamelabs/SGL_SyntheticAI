namespace SGL.JudgeDredd.Core.Models;

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? CommandLine { get; set; }
    public int ParentProcessId { get; set; }
    public string? UserName { get; set; }
    public DateTime? StartTime { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsageMb { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspicionReason { get; set; }
}
