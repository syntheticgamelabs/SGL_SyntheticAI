using System.Diagnostics;
using System.Management;

namespace SGL.JudgeDredd.Antivirus.Monitoring;

public class BehavioralAlert
{
    public DateTime Timestamp { get; set; }
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public string AlertType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "Medium";
}

public class BehavioralMonitor : IDisposable
{
    private readonly List<BehavioralAlert> _alerts = new();
    private ManagementEventWatcher? _processWatcher;
    private FileSystemWatcher? _criticalFolderWatcher;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // Thread-safe ransomware rapid-change detection fields
    private int _rapidChangeCount;
    private long _lastChangeTicks;
    private readonly object _rapidChangeLock = new();

    public event Action<BehavioralAlert>? AlertRaised;
    public IReadOnlyList<BehavioralAlert> Alerts => _alerts;
    public bool IsRunning => _isRunning;

    // Suspicious process names that indicate potential threats
    private static readonly HashSet<string> SuspiciousProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell", "cmd", "wscript", "cscript", "mshta", "regsvr32",
        "rundll32", "certutil", "bitsadmin", "msbuild"
    };

    // Suspicious command-line patterns
    private static readonly string[] SuspiciousCommandPatterns = new[]
    {
        "-encodedcommand", "-enc ", "bypass", "hidden", "downloadstring",
        "invoke-expression", "iex ", "invoke-webrequest", "webclient",
        "net user ", "net localgroup", "reg add", "schtasks /create",
        "vssadmin delete", "wmic shadowcopy", "bcdedit /set"
    };

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();

        // Monitor new process creation via WMI
        try
        {
            _processWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _processWatcher.EventArrived += OnProcessCreated;
            _processWatcher.Start();
        }
        catch { /* WMI may not be available - continue without */ }

        // Monitor critical folders for rapid file changes (ransomware indicator)
        MonitorCriticalFolders();
    }

    private void OnProcessCreated(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"]?.Value?.ToString() ?? "";
            var pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"]?.Value ?? 0);

            // Check if process name is suspicious
            var baseName = Path.GetFileNameWithoutExtension(processName);
            if (SuspiciousProcessNames.Contains(baseName))
            {
                // Get command line
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    string? cmdLine = null;
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
                    foreach (var obj in searcher.Get())
                    {
                        cmdLine = obj["CommandLine"]?.ToString();
                        break;
                    }

                    if (!string.IsNullOrEmpty(cmdLine))
                    {
                        foreach (var pattern in SuspiciousCommandPatterns)
                        {
                            if (cmdLine.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                RaiseAlert(new BehavioralAlert
                                {
                                    Timestamp = DateTime.Now,
                                    ProcessName = processName,
                                    ProcessId = pid,
                                    AlertType = "SuspiciousCommand",
                                    Description = $"Process '{processName}' launched with suspicious arguments: {pattern}",
                                    Severity = "High"
                                });
                                break;
                            }
                        }
                    }
                }
                catch { /* Process may have exited */ }
            }
        }
        catch { /* Ignore individual event errors */ }
    }

    private void MonitorCriticalFolders()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (Directory.Exists(documentsPath))
        {
            _criticalFolderWatcher = new FileSystemWatcher(documentsPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _rapidChangeCount = 0;
            _lastChangeTicks = DateTime.MinValue.Ticks;

            _criticalFolderWatcher.Changed += (s, e) =>
            {
                var now = DateTime.Now;
                lock (_rapidChangeLock)
                {
                    var lastChange = new DateTime(Interlocked.Read(ref _lastChangeTicks));
                    if ((now - lastChange).TotalMilliseconds < 500)
                    {
                        _rapidChangeCount++;
                        if (_rapidChangeCount > 50) // 50+ file changes in rapid succession
                        {
                            RaiseAlert(new BehavioralAlert
                            {
                                Timestamp = now,
                                ProcessName = "Unknown",
                                AlertType = "RansomwareIndicator",
                                Description = $"Rapid file modification detected in {documentsPath}: {_rapidChangeCount} files changed in quick succession",
                                Severity = "Critical"
                            });
                            _rapidChangeCount = 0;
                        }
                    }
                    else
                    {
                        _rapidChangeCount = 1;
                    }
                    Interlocked.Exchange(ref _lastChangeTicks, now.Ticks);
                }
            };

            _criticalFolderWatcher.Renamed += (s, e) =>
            {
                // Check for ransomware extension changes
                var ext = Path.GetExtension(e.Name)?.ToLower();
                string[] ransomwareExtensions = { ".encrypted", ".locked", ".crypt", ".locky",
                    ".cerber", ".zepto", ".thor", ".zzzzz", ".aaa", ".abc", ".xyz",
                    ".micro", ".crypto", ".enc", ".r5a", ".WNCRY", ".wnry" };

                if (ext != null && ransomwareExtensions.Contains(ext))
                {
                    RaiseAlert(new BehavioralAlert
                    {
                        Timestamp = DateTime.Now,
                        ProcessName = "Unknown",
                        AlertType = "RansomwareExtension",
                        Description = $"File renamed to ransomware extension: {e.Name}",
                        Severity = "Critical"
                    });
                }

                // Renamed events also count toward rapid-change detection
                lock (_rapidChangeLock)
                {
                    var now = DateTime.Now;
                    var lastChange = new DateTime(Interlocked.Read(ref _lastChangeTicks));
                    if ((now - lastChange).TotalMilliseconds < 500)
                    {
                        _rapidChangeCount++;
                    }
                    else
                    {
                        _rapidChangeCount = 1;
                    }
                    Interlocked.Exchange(ref _lastChangeTicks, now.Ticks);
                }
            };
        }
    }

    private void RaiseAlert(BehavioralAlert alert)
    {
        _alerts.Add(alert);
        if (_alerts.Count > 1000) _alerts.RemoveAt(0);
        AlertRaised?.Invoke(alert);
    }

    public void Stop()
    {
        _isRunning = false;
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
        _criticalFolderWatcher?.Dispose();
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
