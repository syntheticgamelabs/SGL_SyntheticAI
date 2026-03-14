using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ScheduleEntry : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _scanType = "Quick";
    [ObservableProperty] private string _frequency = "Daily";
    [ObservableProperty] private TimeSpan _scheduledTime = new(9, 0, 0);
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private DateTime? _lastRun;
    [ObservableProperty] private DateTime? _nextRun;
    [ObservableProperty] private string _status = "Scheduled";
    [ObservableProperty] private string _targetPath = "C:\\";
    [ObservableProperty] private int _threatsFound;

    public string ScheduledTimeFormatted => $"{ScheduledTime:hh\\:mm}";
    public string LastRunFormatted => LastRun?.ToString("yyyy-MM-dd HH:mm") ?? "Never";
    public string NextRunFormatted => NextRun?.ToString("yyyy-MM-dd HH:mm") ?? "Not scheduled";
}

public partial class ScheduledScanViewModel : ViewModelBase
{
    private readonly IScanEngine _scanEngine;
    private DispatcherTimer? _checkTimer;
    private CancellationTokenSource? _scanCts;

    private static readonly string ScheduleFilePath =
        Path.Combine(AppContext.BaseDirectory, "data", "scheduled_scan.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [ObservableProperty] private int _activeSchedules;
    [ObservableProperty] private int _completedScans;
    [ObservableProperty] private int _threatsFoundBySchedule;
    [ObservableProperty] private string _schedulerStatus = "Scheduler active";
    [ObservableProperty] private bool _isRunning;

    // Form fields for creating new schedule
    [ObservableProperty] private string _newName = "My Scheduled Scan";
    [ObservableProperty] private string _newScanType = "Quick";
    [ObservableProperty] private int _newHour = 9;
    [ObservableProperty] private int _newMinute = 0;
    [ObservableProperty] private string _newFrequency = "Daily";
    [ObservableProperty] private string _newTargetPath = "C:\\";

    public ObservableCollection<ScheduleEntry> Schedules { get; } = [];
    public ObservableCollection<string> ScanLog { get; } = [];
    public string[] ScanTypes { get; } = ["Quick", "Full", "Custom Path"];
    public string[] Frequencies { get; } = ["Every 30 Min", "Hourly", "Every 6 Hours", "Daily", "Weekly"];

    public ScheduledScanViewModel(IScanEngine scanEngine)
    {
        _scanEngine = scanEngine;
        Title = "Scheduled Scanning";

        // Load persisted schedules or create defaults
        if (!LoadSchedules())
        {
            // Default schedules (only used on first run)
            Schedules.Add(new ScheduleEntry
            {
                Name = "Daily Quick Scan",
                ScanType = "Quick",
                Frequency = "Daily",
                ScheduledTime = new TimeSpan(9, 0, 0),
                IsEnabled = true,
                NextRun = DateTime.Today.AddDays(1).Add(new TimeSpan(9, 0, 0)),
                Status = "Scheduled",
            });
            Schedules.Add(new ScheduleEntry
            {
                Name = "Weekly Full Scan",
                ScanType = "Full",
                Frequency = "Weekly",
                ScheduledTime = new TimeSpan(2, 0, 0),
                IsEnabled = true,
                NextRun = GetNextWeeklyRun(DayOfWeek.Sunday, new TimeSpan(2, 0, 0)),
                Status = "Scheduled",
            });
            SaveSchedules();
        }

        ActiveSchedules = Schedules.Count(s => s.IsEnabled);
        LogScan("Scheduler initialized with default schedules");

        // Check every 30 seconds if any schedule needs to run
        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _checkTimer.Tick += async (_, _) => await CheckSchedulesAsync();
        _checkTimer.Start();
    }

    [RelayCommand]
    private void AddSchedule()
    {
        var time = new TimeSpan(Math.Clamp(NewHour, 0, 23), Math.Clamp(NewMinute, 0, 59), 0);
        var entry = new ScheduleEntry
        {
            Name = string.IsNullOrWhiteSpace(NewName) ? "Unnamed Scan" : NewName,
            ScanType = NewScanType,
            Frequency = NewFrequency,
            ScheduledTime = time,
            IsEnabled = true,
            TargetPath = NewTargetPath,
            NextRun = CalculateNextRun(NewFrequency, time),
            Status = "Scheduled",
        };
        Schedules.Add(entry);
        ActiveSchedules = Schedules.Count(s => s.IsEnabled);
        LogScan($"Added schedule: {entry.Name} ({entry.Frequency} at {entry.ScheduledTimeFormatted})");
        SchedulerStatus = $"Schedule added: {entry.Name}";
        SaveSchedules();
    }

    [RelayCommand]
    private void RemoveSchedule(ScheduleEntry? entry)
    {
        if (entry is null) return;
        Schedules.Remove(entry);
        ActiveSchedules = Schedules.Count(s => s.IsEnabled);
        LogScan($"Removed schedule: {entry.Name}");
        SchedulerStatus = $"Schedule removed: {entry.Name}";
        SaveSchedules();
    }

    [RelayCommand]
    private void ToggleSchedule(ScheduleEntry? entry)
    {
        if (entry is null) return;
        entry.IsEnabled = !entry.IsEnabled;
        entry.Status = entry.IsEnabled ? "Scheduled" : "Disabled";
        ActiveSchedules = Schedules.Count(s => s.IsEnabled);
        LogScan($"{(entry.IsEnabled ? "Enabled" : "Disabled")} schedule: {entry.Name}");
        SaveSchedules();
    }

    [RelayCommand]
    private async Task RunNowAsync(ScheduleEntry? entry)
    {
        if (entry is null || IsRunning) return;
        await ExecuteScheduledScan(entry);
    }

    private async Task CheckSchedulesAsync()
    {
        if (IsRunning) return;
        var now = DateTime.Now;
        foreach (var sched in Schedules.Where(s => s.IsEnabled && s.NextRun.HasValue && s.NextRun.Value <= now))
        {
            await ExecuteScheduledScan(sched);
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
        LogScan("Scan cancellation requested");
    }

    private async Task ExecuteScheduledScan(ScheduleEntry entry)
    {
        IsRunning = true;
        entry.Status = "Running...";
        entry.ThreatsFound = 0;
        SchedulerStatus = $"Running: {entry.Name}...";
        LogScan($"Started: {entry.Name} ({entry.ScanType})");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Scheduled scan running: {entry.Name}");

        _scanCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<ScanProgressEvent>(e =>
            {
                entry.Status = $"Scanning... {e.ProgressPercent:F0}% ({e.ScannedFiles}/{e.TotalFiles})";
                entry.ThreatsFound = e.ThreatsFound;
            });

            ScanSession session;
            switch (entry.ScanType)
            {
                case "Full":
                    session = await _scanEngine.ExtendedScanAsync(null, progress, _scanCts.Token);
                    break;
                case "Custom Path":
                    session = await _scanEngine.ScanDirectoryAsync(
                        entry.TargetPath, ScanType.Custom, progress, _scanCts.Token);
                    break;
                default: // Quick
                    session = await _scanEngine.QuickScanAsync(progress, _scanCts.Token);
                    break;
            }

            var threats = session.Results.Count(r => r.IsThreat);
            entry.ThreatsFound = threats;
            ThreatsFoundBySchedule += threats;

            entry.LastRun = DateTime.Now;
            entry.NextRun = CalculateNextRun(entry.Frequency, entry.ScheduledTime);
            entry.Status = threats > 0 ? $"Completed - {threats} threat(s)" : "Completed - Clean";
            CompletedScans++;

            LogScan($"Completed: {entry.Name} - Scanned {session.TotalFiles} files, {threats} threat(s) found");
            SchedulerStatus = $"Completed: {entry.Name}";
            SaveSchedules();

            AvatarViewModel.Instance.SetExpression(threats > 0 ? AvatarExpression.FoundMalware : AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                threats > 0
                    ? $"Scheduled scan complete: {entry.Name}. Found {threats} threat(s)!"
                    : $"Scheduled scan complete: {entry.Name}. All clear.");
        }
        catch (OperationCanceledException)
        {
            entry.Status = "Cancelled";
            LogScan($"Cancelled: {entry.Name}");
            SchedulerStatus = $"Scan cancelled: {entry.Name}";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
        catch (Exception ex)
        {
            entry.Status = "Failed";
            LogScan($"Failed: {entry.Name} - {ex.Message}");
            SchedulerStatus = $"Scan failed: {entry.Name}";
        }
        finally
        {
            IsRunning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private static DateTime CalculateNextRun(string frequency, TimeSpan time) => frequency switch
    {
        "Every 30 Min" => DateTime.Now.AddMinutes(30),
        "Hourly" => DateTime.Now.AddHours(1),
        "Every 6 Hours" => DateTime.Now.AddHours(6),
        "Weekly" => GetNextWeeklyRun(DayOfWeek.Sunday, time),
        _ => DateTime.Today.AddDays(1).Add(time),
    };

    private static DateTime GetNextWeeklyRun(DayOfWeek day, TimeSpan time)
    {
        var now = DateTime.Now;
        int daysUntil = ((int)day - (int)now.DayOfWeek + 7) % 7;
        if (daysUntil == 0 && now.TimeOfDay >= time) daysUntil = 7;
        return now.Date.AddDays(daysUntil).Add(time);
    }

    private void LogScan(string msg) =>
        ScanLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");

    /// <summary>
    /// Persists the current schedule list to data/scheduled_scan.json.
    /// </summary>
    private void SaveSchedules()
    {
        try
        {
            var dir = Path.GetDirectoryName(ScheduleFilePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var dtos = Schedules.Select(s => new ScheduleDto
            {
                Name = s.Name,
                ScanType = s.ScanType,
                Frequency = s.Frequency,
                ScheduledTimeHours = s.ScheduledTime.Hours,
                ScheduledTimeMinutes = s.ScheduledTime.Minutes,
                IsEnabled = s.IsEnabled,
                TargetPath = s.TargetPath,
                LastRun = s.LastRun,
                NextRun = s.NextRun,
            }).ToList();

            var json = JsonSerializer.Serialize(dtos, JsonOptions);
            File.WriteAllText(ScheduleFilePath, json);
        }
        catch
        {
            // Best effort - don't crash the UI if persistence fails
        }
    }

    /// <summary>
    /// Loads schedules from data/scheduled_scan.json. Returns true if loaded successfully.
    /// </summary>
    private bool LoadSchedules()
    {
        try
        {
            if (!File.Exists(ScheduleFilePath))
                return false;

            var json = File.ReadAllText(ScheduleFilePath);
            var dtos = JsonSerializer.Deserialize<List<ScheduleDto>>(json, JsonOptions);
            if (dtos == null || dtos.Count == 0)
                return false;

            Schedules.Clear();
            foreach (var dto in dtos)
            {
                var entry = new ScheduleEntry
                {
                    Name = dto.Name,
                    ScanType = dto.ScanType,
                    Frequency = dto.Frequency,
                    ScheduledTime = new TimeSpan(dto.ScheduledTimeHours, dto.ScheduledTimeMinutes, 0),
                    IsEnabled = dto.IsEnabled,
                    TargetPath = dto.TargetPath,
                    LastRun = dto.LastRun,
                    NextRun = dto.NextRun,
                    Status = dto.IsEnabled ? "Scheduled" : "Disabled",
                };

                // Recalculate NextRun if it's in the past
                if (entry.NextRun.HasValue && entry.NextRun.Value < DateTime.Now && entry.IsEnabled)
                {
                    entry.NextRun = CalculateNextRun(entry.Frequency, entry.ScheduledTime);
                }

                Schedules.Add(entry);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lightweight DTO for JSON serialization of schedule entries.
    /// </summary>
    private class ScheduleDto
    {
        public string Name { get; set; } = string.Empty;
        public string ScanType { get; set; } = "Quick";
        public string Frequency { get; set; } = "Daily";
        public int ScheduledTimeHours { get; set; }
        public int ScheduledTimeMinutes { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string TargetPath { get; set; } = "C:\\";
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
    }
}
