namespace SGL.JudgeDredd.Core.Interfaces;

using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Models;

public interface INotificationService
{
    void ShowNotification(string title, string message, ThreatSeverity severity);
    void ShowThreatAlert(ThreatInfo threat);
    void ShowScanComplete(ScanSession session);
}
