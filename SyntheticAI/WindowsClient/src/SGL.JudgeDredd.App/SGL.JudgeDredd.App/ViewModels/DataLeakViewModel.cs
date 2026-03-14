using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class DataLeakViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;
    private readonly Dictionary<int, long> _previousBytesSent = new();
    private readonly Dictionary<int, long> _previousBytesReceived = new();

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _monitorStatus = "Idle - Click Start to begin monitoring data transfers";

    [ObservableProperty]
    private int _activeTransfers;

    [ObservableProperty]
    private string _totalUploadRate = "0 B/s";

    [ObservableProperty]
    private string _totalDownloadRate = "0 B/s";

    public ObservableCollection<DataTransferEntry> ActiveTransfers_ { get; } = [];
    public ObservableCollection<DataTransferEntry> TransferHistory { get; } = [];

    // Processes known for data exfiltration risk
    private static readonly HashSet<string> SensitiveProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave",
        "OneDrive", "Dropbox", "GoogleDrive", "iCloudDrive",
        "Teams", "Slack", "Discord", "Telegram", "WhatsApp",
        "outlook", "thunderbird",
        "ftp", "sftp", "scp", "curl", "wget", "powershell", "cmd",
        "git", "svn",
        "robocopy", "xcopy",
        "TeamViewer", "AnyDesk", "rustdesk",
    };

    public DataLeakViewModel()
    {
        Title = "Data Leak";
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        MonitorStatus = "Monitoring data transfers...";
        ActiveTransfers_.Clear();
        _previousBytesSent.Clear();
        _previousBytesReceived.Clear();

        // Initial scan
        await ScanNetworkActivityAsync();

        // Start periodic monitoring every 3 seconds
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _monitorTimer.Tick += async (_, _) => await ScanNetworkActivityAsync();
        _monitorTimer.Start();
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;
        IsMonitoring = false;
        MonitorStatus = $"Monitoring stopped. {TransferHistory.Count} transfers logged.";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        TransferHistory.Clear();
    }

    [RelayCommand]
    private void KillProcess(DataTransferEntry? entry)
    {
        if (entry is null) return;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(entry.Pid);
            proc.Kill();
            proc.Dispose();
            entry.Status = "Killed";
            ActiveTransfers_.Remove(entry);
            MonitorStatus = $"Process {entry.ProcessName} (PID {entry.Pid}) terminated.";
        }
        catch (ArgumentException)
        {
            entry.Status = "Exited";
            MonitorStatus = $"Process {entry.ProcessName} already exited.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MonitorStatus = $"Access denied: Cannot kill {entry.ProcessName}. Run as administrator.";
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to kill {entry.ProcessName}: {ex.Message}";
        }
    }

    private async Task ScanNetworkActivityAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // Get active TCP connections with their owning processes
                var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();

                // Get network interface stats for overall rates
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                long totalSent = 0;
                long totalReceived = 0;

                foreach (var iface in interfaces)
                {
                    var stats = iface.GetIPStatistics();
                    totalSent += stats.BytesSent;
                    totalReceived += stats.BytesReceived;
                }

                // Scan processes with network activity
                // Build a set of PIDs that own established TCP connections
                var pidsWithConnections = new HashSet<int>();
                try
                {
                    var netstatProc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netstat.exe",
                            Arguments = "-ano",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    netstatProc.Start();
                    string output = netstatProc.StandardOutput.ReadToEnd();
                    netstatProc.WaitForExit(5000);

                    foreach (var line in output.Split('\n'))
                    {
                        if (!line.Contains("ESTABLISHED")) continue;
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && int.TryParse(parts[^1], out int pid))
                            pidsWithConnections.Add(pid);
                    }
                }
                catch { }

                var processes = Process.GetProcesses();
                var newEntries = new List<DataTransferEntry>();

                foreach (var proc in processes)
                {
                    try
                    {
                        if (!SensitiveProcesses.Contains(proc.ProcessName))
                            continue;

                        // Check if THIS specific process has active TCP connections
                        bool hasConnection = pidsWithConnections.Contains(proc.Id);

                        if (hasConnection)
                        {
                            var entry = new DataTransferEntry
                            {
                                Pid = proc.Id,
                                ProcessName = proc.ProcessName,
                                Direction = "Upload/Download",
                                Status = "Active",
                                DetectedAt = DateTime.Now,
                                MemoryUsage = FormatBytes(proc.WorkingSet64),
                            };

                            // Try to get meaningful source/dest info
                            try
                            {
                                var mainModule = proc.MainModule;
                                entry.SourcePath = mainModule?.FileName ?? "Unknown";
                            }
                            catch
                            {
                                entry.SourcePath = proc.ProcessName;
                            }

                            entry.DestinationInfo = "Network / Cloud";

                            newEntries.Add(entry);
                        }
                    }
                    catch { /* Access denied */ }
                }

                foreach (var p in processes) { try { p.Dispose(); } catch { } }

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ActiveTransfers_.Clear();
                    foreach (var e in newEntries)
                    {
                        ActiveTransfers_.Add(e);

                        // Add to history if not already there
                        if (!TransferHistory.Any(h => h.Pid == e.Pid && h.ProcessName == e.ProcessName
                                                      && (DateTime.Now - h.DetectedAt).TotalSeconds < 30))
                        {
                            TransferHistory.Insert(0, e);
                            // Limit history to 500 entries
                            while (TransferHistory.Count > 500)
                                TransferHistory.RemoveAt(TransferHistory.Count - 1);
                        }
                    }

                    ActiveTransfers = ActiveTransfers_.Count;

                    // Calculate rough network rates using interface stats
                    TotalUploadRate = FormatRate(totalSent);
                    TotalDownloadRate = FormatRate(totalReceived);

                    MonitorStatus = $"Monitoring active - {ActiveTransfers} process(es) with network activity - {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    MonitorStatus = $"Scan error: {ex.Message}";
                });
            }
        });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private static string FormatRate(long totalBytes)
    {
        // Display cumulative transferred
        if (totalBytes < 1024) return $"{totalBytes} B";
        if (totalBytes < 1024 * 1024) return $"{totalBytes / 1024.0:F1} KB";
        if (totalBytes < 1024L * 1024 * 1024) return $"{totalBytes / (1024.0 * 1024.0):F1} MB";
        return $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

public class DataTransferEntry
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationInfo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MemoryUsage { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
