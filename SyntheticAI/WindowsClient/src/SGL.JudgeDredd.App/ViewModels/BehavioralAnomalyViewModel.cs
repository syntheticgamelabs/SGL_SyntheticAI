using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security.Monitors;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class BehavioralAnomalyViewModel : ViewModelBase
{
    private readonly BehavioralAnomalyService _service = new();

    [ObservableProperty] private int _totalAnomalies;
    [ObservableProperty] private int _criticalAnomalies;
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _lastCheckTime = "Never";
    [ObservableProperty] private double _systemCpuPercent;
    [ObservableProperty] private double _systemMemoryPercent;
    [ObservableProperty] private string _monitoringStatus = "Stopped";

    public ObservableCollection<AnomalyEvent> Anomalies => _service.Anomalies;

    public BehavioralAnomalyViewModel()
    {
        Title = "Anomaly Detector";
    }

    [RelayCommand]
    private void ToggleMonitoring()
    {
        if (_service.IsMonitoring)
        {
            _service.StopMonitoring();
            IsMonitoring = false;
            MonitoringStatus = "Stopped";
        }
        else
        {
            _service.StartMonitoring();
            IsMonitoring = true;
            MonitoringStatus = "Active - Monitoring...";
        }
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        MonitoringStatus = "Checking...";
        try
        {
            await _service.CheckNowAsync();
            TotalAnomalies = _service.TotalAnomalies;
            CriticalAnomalies = _service.CriticalAnomalies;
            LastCheckTime = _service.LastCheckTime;
            SystemCpuPercent = _service.SystemCpuPercent;
            SystemMemoryPercent = _service.SystemMemoryPercent;
            MonitoringStatus = _service.IsMonitoring ? "Active - Monitoring..." : $"Check complete. {TotalAnomalies} anomalies detected.";
        }
        catch (Exception ex)
        {
            MonitoringStatus = $"Check failed: {ex.Message}";
        }
    }
}
