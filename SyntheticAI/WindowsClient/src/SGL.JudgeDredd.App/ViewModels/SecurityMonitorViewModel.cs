using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class SecurityMonitorViewModel : ViewModelBase
{
    private readonly ISecurityMonitor _securityMonitor;

    [ObservableProperty]
    private bool _isRemoteAccessMonitorActive = true;

    [ObservableProperty]
    private bool _isBadUsbMonitorActive = true;

    [ObservableProperty]
    private bool _isAiDetectionActive = true;

    [ObservableProperty]
    private bool _isRegistryWatcherActive = true;

    public ObservableCollection<SecurityAlert> Alerts { get; } = [];

    public SecurityMonitorViewModel(ISecurityMonitor securityMonitor)
    {
        _securityMonitor = securityMonitor;
        Title = "Security Monitor";

        _securityMonitor.AlertRaised += OnAlertRaised;

        LoadActiveAlerts();
    }

    private void LoadActiveAlerts()
    {
        Alerts.Clear();
        foreach (var alert in _securityMonitor.GetActiveAlerts())
        {
            Alerts.Add(alert);
        }
    }

    private void OnAlertRaised(object? sender, SecurityAlertEvent e)
    {
        // Marshal to UI thread via dispatcher if needed
        var alert = new SecurityAlert
        {
            AlertId = Guid.NewGuid(),
            Category = AlertCategory.FileSystemThreat,
            Severity = ThreatSeverity.High,
            Title = "Security Alert",
            Description = e.ToString() ?? "A security event was detected.",
            DetectedAt = DateTime.UtcNow,
            IsAcknowledged = false
        };

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Alerts.Insert(0, alert);
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            _ = AvatarViewModel.Instance.ShowSpeechBubble($"Alert: {alert.Title}");
        });
    }

    [RelayCommand]
    private async Task AcknowledgeAlertAsync(SecurityAlert? alert)
    {
        if (alert is null) return;

        alert.IsAcknowledged = true;
        alert.UserAction = "Acknowledged";

        // Refresh the collection to reflect the change
        var index = Alerts.IndexOf(alert);
        if (index >= 0)
        {
            Alerts.RemoveAt(index);
            Alerts.Insert(index, alert);
        }

        await AvatarViewModel.Instance.ShowSpeechBubble("Alert acknowledged.");
    }

    [RelayCommand]
    private async Task DismissAlertAsync(SecurityAlert? alert)
    {
        if (alert is null) return;

        alert.UserAction = "Dismissed";
        Alerts.Remove(alert);
        await AvatarViewModel.Instance.ShowSpeechBubble("Alert dismissed.");

        if (Alerts.Count == 0)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }
}
