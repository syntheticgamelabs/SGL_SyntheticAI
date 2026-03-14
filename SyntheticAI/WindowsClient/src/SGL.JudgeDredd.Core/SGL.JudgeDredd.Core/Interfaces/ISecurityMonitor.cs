namespace SGL.JudgeDredd.Core.Interfaces;

using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Models;

public interface ISecurityMonitor
{
    void StartAllMonitors();
    void StopAllMonitors();
    IReadOnlyList<SecurityAlert> GetActiveAlerts();
    event EventHandler<SecurityAlertEvent>? AlertRaised;
}
