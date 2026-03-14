using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class TrackerViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _trackersDetected;

    [ObservableProperty]
    private string _monitorStatus = "Idle - Click Start to begin monitoring";

    public ObservableCollection<TrackerEntry> Trackers { get; } = [];

    // Known tracker/telemetry hosts and process names
    private static readonly Dictionary<string, string> KnownTrackerProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DiagTrack", "Microsoft Telemetry (Connected User Experiences)" },
        { "CompatTelRunner", "Microsoft Compatibility Telemetry" },
        { "WerFault", "Windows Error Reporting" },
        { "WerSvc", "Windows Error Reporting Service" },
        { "dmwappushservice", "Microsoft WAP Push Message Routing" },
        { "SensorDataService", "Microsoft Sensor Data Service" },
        { "DeviceCensus", "Microsoft Device Census" },
        { "InventoryAgent", "Microsoft Inventory Agent" },
        { "backgroundTaskHost", "Background Telemetry Host" },
        { "OneDrive", "Microsoft OneDrive Sync (data collection)" },
        { "OfficeClickToRun", "Microsoft Office Telemetry" },
        { "msedge", "Microsoft Edge (telemetry/tracking)" },
        { "MicrosoftEdgeUpdate", "Microsoft Edge Auto-Update (telemetry)" },
        { "GoogleUpdate", "Google Update Service (telemetry)" },
        { "GoogleCrashHandler", "Google Crash Reporter" },
        { "chrome", "Google Chrome (tracking/telemetry)" },
        { "CrashReportSender", "Crash Report Sender" },
        { "software_reporter_tool", "Chrome Software Reporter (scanning)" },
        { "WmiPrvSE", "WMI Provider (potential data collection)" },
        { "SgrmBroker", "System Guard Runtime Monitor" },
        { "uhssvc", "Microsoft Update Health Service" },
        { "TabTip", "Windows Ink/Handwriting Telemetry" },
        { "AdobeARM", "Adobe Telemetry (ARM)" },
        { "AdobeUpdateService", "Adobe Update Service (telemetry)" },
        { "Cortana", "Microsoft Cortana (voice data collection)" },
        { "SearchUI", "Windows Search (data indexing/collection)" },
        { "SearchHost", "Windows Search Host (data indexing)" },
        { "MusNotification", "Microsoft Update Notification (telemetry)" },
    };

    private static readonly HashSet<string> KnownTrackerDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "telemetry.microsoft.com",
        "vortex.data.microsoft.com",
        "settings-win.data.microsoft.com",
        "watson.telemetry.microsoft.com",
        "oca.telemetry.microsoft.com",
        "v10.events.data.microsoft.com",
        "v10c.events.data.microsoft.com",
        "v20.events.data.microsoft.com",
        "diagnostics.support.microsoft.com",
        "survey.watson.microsoft.com",
        "choice.microsoft.com",
        "analytics.google.com",
        "www.google-analytics.com",
        "ssl.google-analytics.com",
        "crashlyticsreports-pa.googleapis.com",
        "firebaselogging-pa.googleapis.com",
        "app-measurement.com",
        "facebook.net",
        "connect.facebook.net",
        "pixel.facebook.com",
        "ads.linkedin.com",
        "bat.bing.com",
        "clarity.ms",
        "browser.events.data.microsoft.com",
    };

    public TrackerViewModel()
    {
        Title = "Trackers";
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        MonitorStatus = "Scanning for trackers and telemetry services...";
        Trackers.Clear();
        TrackersDetected = 0;

        // Initial full scan
        await ScanForTrackersAsync();

        // Start periodic monitoring
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _monitorTimer.Tick += async (_, _) => await ScanForTrackersAsync();
        _monitorTimer.Start();

        MonitorStatus = $"Monitoring active - {TrackersDetected} tracker(s) detected - Refreshing every 10s";
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;
        IsMonitoring = false;
        MonitorStatus = $"Monitoring stopped - {TrackersDetected} tracker(s) were detected";
    }

    [RelayCommand]
    private async Task KillTrackerAsync(TrackerEntry? entry)
    {
        if (entry is null) return;

        try
        {
            var process = Process.GetProcessById(entry.Pid);
            process.Kill();
            process.Dispose();
            Trackers.Remove(entry);
            TrackersDetected = Trackers.Count;
            MonitorStatus = $"Killed: {entry.ProcessName} (PID {entry.Pid})";
        }
        catch (ArgumentException)
        {
            Trackers.Remove(entry);
            TrackersDetected = Trackers.Count;
            MonitorStatus = $"Process already exited: {entry.ProcessName}";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MonitorStatus = $"Access denied: Cannot kill {entry.ProcessName}. Requires elevated privileges.";
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to kill {entry.ProcessName}: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task ScanForTrackersAsync()
    {
        await Task.Run(() =>
        {
            var processes = Process.GetProcesses();
            var currentPids = new HashSet<int>(Trackers.Select(t => t.Pid));

            foreach (var proc in processes)
            {
                try
                {
                    if (KnownTrackerProcesses.TryGetValue(proc.ProcessName, out var description))
                    {
                        if (!currentPids.Contains(proc.Id))
                        {
                            var entry = new TrackerEntry
                            {
                                Pid = proc.Id,
                                ProcessName = proc.ProcessName,
                                Description = description,
                                DetectedAt = DateTime.Now,
                                Category = CategorizeTracker(proc.ProcessName),
                                MemoryUsage = FormatBytes(proc.WorkingSet64),
                            };

                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                Trackers.Add(entry);
                            });
                        }
                    }
                }
                catch { /* Access denied or exited */ }
            }

            // Clean up entries for processes that no longer exist
            var activePids = new HashSet<int>(processes.Select(p => { try { return p.Id; } catch { return 0; } }));
            var dead = Trackers.Where(t => !activePids.Contains(t.Pid)).ToList();
            foreach (var d in dead)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => Trackers.Remove(d));
            }

            foreach (var p in processes) { try { p.Dispose(); } catch { } }
        });

        TrackersDetected = Trackers.Count;
        if (IsMonitoring)
            MonitorStatus = $"Monitoring active - {TrackersDetected} tracker(s) detected - Last scan: {DateTime.Now:HH:mm:ss}";
    }

    private static string CategorizeTracker(string processName)
    {
        var lower = processName.ToLowerInvariant();
        if (lower.Contains("microsoft") || lower.Contains("diag") || lower.Contains("compat") || lower.Contains("wer") ||
            lower.Contains("cortana") || lower.Contains("search") || lower.Contains("census") || lower.Contains("sensor") ||
            lower.Contains("onedrive") || lower.Contains("office") || lower.Contains("edge") || lower.Contains("mus"))
            return "Microsoft Telemetry";
        if (lower.Contains("google") || lower.Contains("chrome"))
            return "Google Telemetry";
        if (lower.Contains("adobe"))
            return "Adobe Telemetry";
        return "Third-Party Tracker";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public class TrackerEntry
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string MemoryUsage { get; set; } = string.Empty;
}
