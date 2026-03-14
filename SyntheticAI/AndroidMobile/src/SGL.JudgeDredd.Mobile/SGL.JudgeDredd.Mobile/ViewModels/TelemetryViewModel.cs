using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class TelemetryViewModel : ObservableObject
{
    private readonly TelemetryService _telemetryService;
    private readonly ThreatKnowledgeService _knowledgeService;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready to scan";
    [ObservableProperty] private int _totalFound;
    [ObservableProperty] private int _highThreatCount;
    [ObservableProperty] private int _runningCount;
    [ObservableProperty] private string _filterType = "All";

    public ObservableCollection<TelemetryItem> Items { get; } = new();
    private List<TelemetryItem> _allItems = new();

    public TelemetryViewModel(TelemetryService telemetryService, ThreatKnowledgeService knowledgeService)
    {
        _telemetryService = telemetryService;
        _knowledgeService = knowledgeService;
        _telemetryService.StatusChanged += (_, s) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = s);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for telemetry and data collectors...";
        Items.Clear();
        _allItems.Clear();

        var results = await _telemetryService.ScanForTelemetryAsync();
        _allItems = results;

        foreach (var item in results)
            Items.Add(item);

        TotalFound = results.Count;
        HighThreatCount = results.Count(i => i.ThreatLevel == "High");
        RunningCount = results.Count(i => i.IsRunning);
        StatusText = $"Found {TotalFound} apps with sensitive permissions ({HighThreatCount} high threat)";

        foreach (var item in results.Where(i => i.ThreatLevel == "High"))
        {
            await _knowledgeService.AddThreatEntryAsync(new ThreatKnowledgeEntry
            {
                ThreatType = "telemetry",
                ThreatName = item.AppName,
                FilePath = item.PackageName,
                Description = item.Description,
                Severity = item.ThreatLevel,
                Permissions = item.DangerousPermissions,
                Source = "telemetry_scan"
            });
        }

        IsScanning = false;
    }

    [RelayCommand]
    private async Task KillAppAsync(TelemetryItem? item)
    {
        if (item == null) return;
        var success = await _telemetryService.ForceStopAppAsync(item.PackageName);
        if (success)
        {
            item.IsRunning = false;
            StatusText = $"Killed {item.AppName}";
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync(TelemetryItem? item)
    {
        if (item == null) return;
        await _telemetryService.OpenAppSettingsAsync(item.PackageName);
    }

    [RelayCommand]
    private void FilterBy(string type)
    {
        FilterType = type;
        Items.Clear();
        var filtered = type switch
        {
            "Audio" => _allItems.Where(i => i.Category == "Audio Listener"),
            "Camera" => _allItems.Where(i => i.Category == "Camera Access"),
            "Location" => _allItems.Where(i => i.Category == "Location Tracker"),
            "High" => _allItems.Where(i => i.ThreatLevel == "High"),
            "Running" => _allItems.Where(i => i.IsRunning),
            _ => _allItems.AsEnumerable()
        };
        foreach (var item in filtered)
            Items.Add(item);
    }
}
