using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class MonitoredPath : ObservableObject
{
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private int _fileCount;
    [ObservableProperty] private int _changeCount;
    [ObservableProperty] private DateTime _lastChecked;
}

public partial class FimAlert : ObservableObject
{
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _changeType = string.Empty;
    [ObservableProperty] private string _severity = "Medium";
    [ObservableProperty] private DateTime _detectedAt;
    [ObservableProperty] private string _details = string.Empty;
    [ObservableProperty] private bool _isAcknowledged;
}

public partial class FileIntegrityViewModel : ViewModelBase
{
    private DispatcherTimer? _fimTimer;
    private readonly Dictionary<string, string> _fileHashes = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly string _baselinePath;

    [ObservableProperty] private bool _monitoringActive;
    [ObservableProperty] private int _monitoredFiles;
    [ObservableProperty] private int _changeDetections;
    [ObservableProperty] private int _criticalAlerts;
    [ObservableProperty] private string _fimStatus = "Monitoring stopped";
    [ObservableProperty] private string _newWatchPath = string.Empty;

    public ObservableCollection<MonitoredPath> MonitoredPaths { get; } = [];
    public ObservableCollection<FimAlert> Alerts { get; } = [];
    public ObservableCollection<string> FimLog { get; } = [];

    // Critical system paths to monitor by default
    private static readonly string[] CriticalPaths =
    [
        @"C:\Windows\System32\drivers\etc\hosts",
        @"C:\Windows\System32\config",
    ];

    public FileIntegrityViewModel()
    {
        Title = "File Integrity Monitor";
        _baselinePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGL-SyntheticAI", "FIM");
        Directory.CreateDirectory(_baselinePath);

        // Add default monitored paths
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        MonitoredPaths.Add(new MonitoredPath { Path = Path.Combine(winDir, "System32", "drivers", "etc"), IsActive = true });
        MonitoredPaths.Add(new MonitoredPath { Path = Environment.GetFolderPath(Environment.SpecialFolder.Startup), IsActive = true });

        LogFim("FIM module initialized");
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (MonitoringActive) return;
        MonitoringActive = true;
        FimStatus = "Building baseline...";
        LogFim("Starting file integrity monitoring...");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("File integrity monitoring activated. Building baseline checksums.");

        await Task.Run(() =>
        {
            foreach (var mp in MonitoredPaths.Where(p => p.IsActive))
            {
                try
                {
                    if (Directory.Exists(mp.Path))
                    {
                        var files = Directory.GetFiles(mp.Path, "*", SearchOption.TopDirectoryOnly);
                        int count = 0;
                        foreach (var file in files)
                        {
                            try
                            {
                                var hash = ComputeFileHash(file);
                                lock (_fileHashes) { _fileHashes[file] = hash; }
                                count++;
                            }
                            catch { }
                        }
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            mp.FileCount = count;
                            mp.LastChecked = DateTime.Now;
                        });
                        SetupWatcher(mp.Path);
                    }
                    else if (File.Exists(mp.Path))
                    {
                        var hash = ComputeFileHash(mp.Path);
                        lock (_fileHashes) { _fileHashes[mp.Path] = hash; }
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            mp.FileCount = 1;
                            mp.LastChecked = DateTime.Now;
                        });
                    }
                }
                catch { }
            }
        });

        MonitoredFiles = _fileHashes.Count;
        FimStatus = $"Monitoring {MonitoredFiles} files across {MonitoredPaths.Count} paths";
        LogFim($"Baseline established: {MonitoredFiles} files hashed");

        // Periodic integrity check every 60 seconds
        _fimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _fimTimer.Tick += async (_, _) => await RunIntegrityCheckAsync();
        _fimTimer.Start();
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        MonitoringActive = false;
        _fimTimer?.Stop();
        _fimTimer = null;
        foreach (var w in _watchers) { w.EnableRaisingEvents = false; w.Dispose(); }
        _watchers.Clear();
        FimStatus = "Monitoring stopped";
        LogFim("File integrity monitoring stopped");
    }

    [RelayCommand]
    private void AddWatchPath()
    {
        if (string.IsNullOrWhiteSpace(NewWatchPath)) return;
        if (MonitoredPaths.Any(p => p.Path.Equals(NewWatchPath, StringComparison.OrdinalIgnoreCase))) return;

        MonitoredPaths.Add(new MonitoredPath { Path = NewWatchPath, IsActive = true });
        LogFim($"Added watch path: {NewWatchPath}");
        NewWatchPath = string.Empty;

        if (MonitoringActive) _ = StartMonitoringAsync();
    }

    [RelayCommand]
    private void RemoveWatchPath(MonitoredPath? path)
    {
        if (path is null) return;
        MonitoredPaths.Remove(path);
        LogFim($"Removed watch path: {path.Path}");
    }

    [RelayCommand]
    private void AcknowledgeAlert(FimAlert? alert)
    {
        if (alert is null) return;
        alert.IsAcknowledged = true;
        LogFim($"Acknowledged alert: {alert.FilePath}");
    }

    [RelayCommand]
    private async Task RunIntegrityCheckAsync()
    {
        if (!MonitoringActive) return;

        await Task.Run(() =>
        {
            Dictionary<string, string> snapshot;
            lock (_fileHashes) { snapshot = new Dictionary<string, string>(_fileHashes); }

            foreach (var (file, expectedHash) in snapshot)
            {
                try
                {
                    if (!File.Exists(file))
                    {
                        RaiseAlert(file, "Deleted", "Critical", $"Monitored file was deleted: {file}");
                        continue;
                    }
                    var currentHash = ComputeFileHash(file);
                    if (currentHash != expectedHash)
                    {
                        RaiseAlert(file, "Modified", "High", $"File hash changed: {file}");
                        lock (_fileHashes) { _fileHashes[file] = currentHash; }
                    }
                }
                catch { }
            }
        });

        FimStatus = $"Integrity check complete - {MonitoredFiles} files, {ChangeDetections} changes detected";
    }

    private void SetupWatcher(string dir)
    {
        try
        {
            var watcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };
            watcher.Changed += (_, e) => RaiseAlert(e.FullPath, "Modified", "High", $"Real-time change detected: {e.FullPath}");
            watcher.Created += (_, e) => RaiseAlert(e.FullPath, "Created", "Medium", $"New file created: {e.FullPath}");
            watcher.Deleted += (_, e) => RaiseAlert(e.FullPath, "Deleted", "Critical", $"File deleted: {e.FullPath}");
            watcher.Renamed += (_, e) => RaiseAlert(e.FullPath, "Renamed", "High", $"File renamed: {e.OldFullPath} -> {e.FullPath}");
            _watchers.Add(watcher);
        }
        catch { }
    }

    private void RaiseAlert(string filePath, string changeType, string severity, string details)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = Alerts.FirstOrDefault(a => a.FilePath == filePath && a.ChangeType == changeType && !a.IsAcknowledged);
            if (existing != null) return;

            Alerts.Insert(0, new FimAlert
            {
                FilePath = filePath,
                ChangeType = changeType,
                Severity = severity,
                DetectedAt = DateTime.Now,
                Details = details,
            });
            ChangeDetections++;
            if (severity == "Critical") CriticalAlerts++;
            LogFim($"[{severity}] {changeType}: {Path.GetFileName(filePath)}");
        });
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private void LogFim(string msg) => FimLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
}
