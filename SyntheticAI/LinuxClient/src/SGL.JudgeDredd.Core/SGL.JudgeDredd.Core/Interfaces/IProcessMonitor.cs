namespace SGL.JudgeDredd.Core.Interfaces;

using SGL.JudgeDredd.Core.Models;

public interface IProcessMonitor
{
    IReadOnlyList<ProcessInfo> GetRunningProcesses();
    ProcessInfo? GetProcessByName(string name);
    bool IsProcessSuspicious(ProcessInfo process);
    event EventHandler<ProcessInfo>? SuspiciousProcessDetected;
}
