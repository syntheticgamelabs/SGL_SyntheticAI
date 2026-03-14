#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Text;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security.Monitors;

public class AnomalyEvent
{
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public string AnomalyType { get; set; } = ""; // CpuSpike, MemorySpike, RapidFileOps, SuspiciousSpawn, NetworkSpike
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Real-time behavioral anomaly detection engine for the SGL SyntheticAI
/// Security Suite. Builds per-process CPU/memory baselines, monitors file
/// system activity for ransomware-like bulk operations, detects suspicious
/// parent-child process chains, and tracks rapid memory growth indicative of
/// injection or memory leaks.
/// </summary>
public sealed class BehavioralAnomalyService : IDisposable
{
    // -------------------------------------------------------------------
    //  Internal tracking structures
    // -------------------------------------------------------------------

    /// <summary>Tracks the rolling CPU/memory baseline for each process.</summary>
    private class ProcessBaseline
    {
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public double CpuSampleSum { get; set; }
        public long MemorySampleSum { get; set; }
        public int SampleCount { get; set; }
        public double LastCpuPercent { get; set; }
        public long LastMemoryBytes { get; set; }
        public long PreviousMemoryBytes { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;

        public double AverageCpu => SampleCount > 0 ? CpuSampleSum / SampleCount : 0;
        public long AverageMemory => SampleCount > 0 ? MemorySampleSum / SampleCount : 0;
    }

    /// <summary>Suspicious parent process to child process mappings (MITRE T1059/T1204).</summary>
    private static readonly Dictionary<string, HashSet<string>> SuspiciousSpawnPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["winword"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "certutil", "bitsadmin" },
        ["excel"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "certutil", "rundll32" },
        ["powerpnt"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta" },
        ["outlook"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "rundll32" },
        ["msedge"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "certutil", "bitsadmin" },
        ["chrome"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "certutil", "bitsadmin" },
        ["firefox"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "certutil" },
        ["explorer"] = new(StringComparer.OrdinalIgnoreCase) { "mshta", "regsvr32", "certutil", "bitsadmin" },
        ["svchost"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "mshta", "wscript", "cscript" },
        ["wmiprvse"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh" },
        ["notepad"] = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh" },
    };

    /// <summary>Directories monitored for rapid file operations (ransomware detection).</summary>
    private static readonly string[] MonitoredDirectories;

    static BehavioralAnomalyService()
    {
        var dirs = new List<string>();
        TryAddPath(dirs, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        TryAddPath(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        TryAddPath(dirs, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
        TryAddPath(dirs, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        MonitoredDirectories = dirs.ToArray();

        static void TryAddPath(List<string> list, string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                list.Add(path);
        }
    }

    // -------------------------------------------------------------------
    //  State fields
    // -------------------------------------------------------------------

    private readonly ConcurrentDictionary<int, ProcessBaseline> _baselines = new();
    private readonly ConcurrentDictionary<string, DateTime> _raisedAnomalies = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();

    // File-system activity tracking
    private int _fileOpsInWindow;
    private DateTime _fileOpsWindowStart = DateTime.Now;
    private const int FileOpsThreshold = 100; // ops per window
    private static readonly TimeSpan FileOpsWindow = TimeSpan.FromSeconds(10);

    // Network connection rate tracking
    private int _lastConnectionCount;
    private DateTime _lastNetworkCheck = DateTime.Now;

    // Timers
    private Timer? _baselineTimer;
    private Timer? _anomalyTimer;
    private Timer? _networkTimer;

    private volatile bool _monitoring;
    private bool _disposed;

    // System-level metrics (using WMI instead of PerformanceCounter to avoid extra NuGet dependency)
    private float _lastSystemCpu;

    // -------------------------------------------------------------------
    //  Public observable state
    // -------------------------------------------------------------------

    /// <summary>All anomaly events detected during this session.</summary>
    public ObservableCollection<AnomalyEvent> Anomalies { get; } = new();

    /// <summary>Total anomaly events detected.</summary>
    public int TotalAnomalies { get; private set; }

    /// <summary>Number of Critical/High severity anomalies.</summary>
    public int CriticalAnomalies { get; private set; }

    /// <summary>Whether monitoring is currently active.</summary>
    public bool IsMonitoring => _monitoring;

    /// <summary>Formatted timestamp of the last anomaly check.</summary>
    public string LastCheckTime { get; private set; } = "Never";

    /// <summary>Current system-wide CPU utilization percentage.</summary>
    public double SystemCpuPercent { get; private set; }

    /// <summary>Current system-wide memory utilization percentage.</summary>
    public double SystemMemoryPercent { get; private set; }

    // -------------------------------------------------------------------
    //  Constructor
    // -------------------------------------------------------------------

    public BehavioralAnomalyService()
    {
        // No-op constructor — CPU metrics are gathered via WMI in UpdateSystemMetrics()
    }

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>Starts all monitoring timers and file system watchers.</summary>
    public void StartMonitoring()
    {
        if (_monitoring) return;
        _monitoring = true;

        SglLogger.Information("[BehavioralAnomaly] Starting behavioral monitoring.");

        // Build initial baselines immediately, then refresh every 5 seconds.
        _baselineTimer = new Timer(BaselineCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        // Check for anomalies every 8 seconds.
        _anomalyTimer = new Timer(AnomalyCheckCallback, null, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8));

        // Network spike detection every 10 seconds.
        _networkTimer = new Timer(NetworkCheckCallback, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));

        // Start file-system watchers on monitored directories.
        StartFileSystemWatchers();
    }

    /// <summary>Stops all monitoring and disposes timers/watchers.</summary>
    public void StopMonitoring()
    {
        if (!_monitoring) return;
        _monitoring = false;

        SglLogger.Information("[BehavioralAnomaly] Stopping behavioral monitoring.");

        _baselineTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _anomalyTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _networkTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        _baselineTimer?.Dispose();
        _anomalyTimer?.Dispose();
        _networkTimer?.Dispose();
        _baselineTimer = null;
        _anomalyTimer = null;
        _networkTimer = null;

        StopFileSystemWatchers();
    }

    /// <summary>Runs a single anomaly check cycle immediately.</summary>
    public async Task CheckNowAsync()
    {
        SglLogger.Information("[BehavioralAnomaly] Running on-demand anomaly check.");

        await Task.Run(() =>
        {
            RefreshBaselines();
            CheckProcessAnomalies();
            CheckSuspiciousSpawns();
            UpdateSystemMetrics();
            LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------
    //  Timer callbacks
    // -------------------------------------------------------------------

    private void BaselineCallback(object? state)
    {
        if (!_monitoring) return;
        try
        {
            RefreshBaselines();
            UpdateSystemMetrics();
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[BehavioralAnomaly] Baseline callback error: {Message}", ex.Message);
        }
    }

    private void AnomalyCheckCallback(object? state)
    {
        if (!_monitoring) return;
        try
        {
            CheckProcessAnomalies();
            CheckSuspiciousSpawns();
            LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[BehavioralAnomaly] Anomaly check callback error: {Message}", ex.Message);
        }
    }

    private void NetworkCheckCallback(object? state)
    {
        if (!_monitoring) return;
        try
        {
            CheckNetworkSpike();
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[BehavioralAnomaly] Network check callback error: {Message}", ex.Message);
        }
    }

    // -------------------------------------------------------------------
    //  1. Process baseline tracking
    // -------------------------------------------------------------------

    private void RefreshBaselines()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return;
        }

        var livePids = new HashSet<int>();

        foreach (Process proc in processes)
        {
            try
            {
                int pid = proc.Id;
                livePids.Add(pid);

                long memBytes = 0;
                double cpuPercent = 0;

                try { memBytes = proc.WorkingSet64; } catch { }

                // Simple heuristic: compute CPU percentage from TotalProcessorTime
                // delta over the baseline interval. Real per-process CPU is expensive
                // without PDH; this is a fast approximation.
                try
                {
                    if (_baselines.TryGetValue(pid, out ProcessBaseline? existing))
                    {
                        cpuPercent = existing.LastCpuPercent; // carry forward
                    }
                    else
                    {
                        cpuPercent = 0;
                    }
                }
                catch { }

                // Attempt a quick CPU estimate via TotalProcessorTime
                try
                {
                    TimeSpan totalCpu = proc.TotalProcessorTime;
                    TimeSpan uptime = DateTime.Now - proc.StartTime;
                    if (uptime.TotalMilliseconds > 0)
                    {
                        cpuPercent = (totalCpu.TotalMilliseconds / uptime.TotalMilliseconds /
                                     Environment.ProcessorCount) * 100.0;
                        if (cpuPercent > 100) cpuPercent = 100;
                    }
                }
                catch { /* access denied for system processes */ }

                _baselines.AddOrUpdate(pid,
                    _ => new ProcessBaseline
                    {
                        ProcessName = proc.ProcessName,
                        ProcessId = pid,
                        CpuSampleSum = cpuPercent,
                        MemorySampleSum = memBytes,
                        SampleCount = 1,
                        LastCpuPercent = cpuPercent,
                        LastMemoryBytes = memBytes,
                        PreviousMemoryBytes = memBytes,
                        LastSeen = DateTime.Now,
                    },
                    (_, existing) =>
                    {
                        existing.PreviousMemoryBytes = existing.LastMemoryBytes;
                        existing.CpuSampleSum += cpuPercent;
                        existing.MemorySampleSum += memBytes;
                        existing.SampleCount++;
                        existing.LastCpuPercent = cpuPercent;
                        existing.LastMemoryBytes = memBytes;
                        existing.LastSeen = DateTime.Now;
                        return existing;
                    });
            }
            catch { /* access denied */ }
            finally
            {
                try { proc.Dispose(); } catch { }
            }
        }

        // Evict stale entries for processes that have exited.
        foreach (int pid in _baselines.Keys)
        {
            if (!livePids.Contains(pid))
                _baselines.TryRemove(pid, out _);
        }
    }

    // -------------------------------------------------------------------
    //  2. Process anomaly detection (CPU spike, memory spike / growth)
    // -------------------------------------------------------------------

    private void CheckProcessAnomalies()
    {
        foreach (var kvp in _baselines)
        {
            ProcessBaseline bl = kvp.Value;

            // Need at least a few samples before flagging deviations.
            if (bl.SampleCount < 4)
                continue;

            // --- CPU Spike: current > 3x the running average ---
            double avgCpu = bl.AverageCpu;
            if (avgCpu > 1 && bl.LastCpuPercent > avgCpu * 3 && bl.LastCpuPercent > 30)
            {
                RaiseAnomaly(new AnomalyEvent
                {
                    ProcessName = bl.ProcessName,
                    ProcessId = bl.ProcessId,
                    AnomalyType = "CpuSpike",
                    Description = $"Process '{bl.ProcessName}' (PID {bl.ProcessId}) CPU usage " +
                                  $"{bl.LastCpuPercent:F1}% is {bl.LastCpuPercent / avgCpu:F1}x above " +
                                  $"its baseline average of {avgCpu:F1}%.",
                    Severity = bl.LastCpuPercent > 80 ? "High" : "Medium",
                    DetectedAt = DateTime.Now,
                });
            }

            // --- Memory Spike: current > 3x the running average ---
            long avgMem = bl.AverageMemory;
            if (avgMem > 50 * 1024 * 1024 && bl.LastMemoryBytes > avgMem * 3)
            {
                double currentMb = bl.LastMemoryBytes / (1024.0 * 1024.0);
                double avgMb = avgMem / (1024.0 * 1024.0);

                RaiseAnomaly(new AnomalyEvent
                {
                    ProcessName = bl.ProcessName,
                    ProcessId = bl.ProcessId,
                    AnomalyType = "MemorySpike",
                    Description = $"Process '{bl.ProcessName}' (PID {bl.ProcessId}) memory usage " +
                                  $"{currentMb:F0} MB is {bl.LastMemoryBytes / (double)avgMem:F1}x above " +
                                  $"its baseline average of {avgMb:F0} MB.",
                    Severity = currentMb > 2000 ? "High" : "Medium",
                    DetectedAt = DateTime.Now,
                });
            }

            // --- Rapid Memory Growth: memory grew > 200 MB since previous sample ---
            long memGrowth = bl.LastMemoryBytes - bl.PreviousMemoryBytes;
            if (memGrowth > 200 * 1024 * 1024 && bl.PreviousMemoryBytes > 0)
            {
                double growthMb = memGrowth / (1024.0 * 1024.0);

                RaiseAnomaly(new AnomalyEvent
                {
                    ProcessName = bl.ProcessName,
                    ProcessId = bl.ProcessId,
                    AnomalyType = "MemorySpike",
                    Description = $"Process '{bl.ProcessName}' (PID {bl.ProcessId}) memory grew by " +
                                  $"{growthMb:F0} MB in a single sample interval. This may indicate " +
                                  "memory injection or a severe memory leak.",
                    Severity = "High",
                    DetectedAt = DateTime.Now,
                });
            }
        }
    }

    // -------------------------------------------------------------------
    //  3. File-system activity monitoring (ransomware detection)
    // -------------------------------------------------------------------

    private void StartFileSystemWatchers()
    {
        foreach (string dir in MonitoredDirectories)
        {
            try
            {
                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                   NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };

                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemRenamed;

                lock (_lock)
                {
                    _watchers.Add(watcher);
                }

                SglLogger.Debug("[BehavioralAnomaly] Watching directory: {Dir}", dir);
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[BehavioralAnomaly] Could not watch '{Dir}': {Message}", dir, ex.Message);
            }
        }
    }

    private void StopFileSystemWatchers()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        TrackFileOperation(e.FullPath);
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        TrackFileOperation(e.FullPath);
    }

    private void TrackFileOperation(string filePath)
    {
        DateTime now = DateTime.Now;

        // Reset the window if it has expired.
        if ((now - _fileOpsWindowStart) > FileOpsWindow)
        {
            Interlocked.Exchange(ref _fileOpsInWindow, 0);
            _fileOpsWindowStart = now;
        }

        int count = Interlocked.Increment(ref _fileOpsInWindow);

        if (count == FileOpsThreshold)
        {
            // Threshold just crossed; raise the anomaly once per window.
            RaiseAnomaly(new AnomalyEvent
            {
                ProcessName = "FileSystem",
                ProcessId = 0,
                AnomalyType = "RapidFileOps",
                Description = $"Rapid file operations detected: {count} file changes in " +
                              $"{FileOpsWindow.TotalSeconds} seconds across monitored directories. " +
                              "This pattern is consistent with ransomware encryption behavior. " +
                              $"Last file affected: {Truncate(filePath, 200)}",
                Severity = "Critical",
                DetectedAt = now,
            });
        }
        else if (count > FileOpsThreshold && count % 200 == 0)
        {
            // Continue alerting at higher counts.
            RaiseAnomaly(new AnomalyEvent
            {
                ProcessName = "FileSystem",
                ProcessId = 0,
                AnomalyType = "RapidFileOps",
                Description = $"Sustained rapid file operations: {count} file changes in " +
                              $"{(now - _fileOpsWindowStart).TotalSeconds:F0} seconds. " +
                              "Possible active ransomware attack.",
                Severity = "Critical",
                DetectedAt = now,
            });
        }
    }

    // -------------------------------------------------------------------
    //  4. Suspicious parent-child process chains
    // -------------------------------------------------------------------

    private void CheckSuspiciousSpawns()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return;
        }

        foreach (Process proc in processes)
        {
            try
            {
                string childName = proc.ProcessName;

                // Get parent PID via WMI
                int parentPid = GetParentProcessId(proc.Id);
                if (parentPid <= 0)
                    continue;

                string? parentName = null;
                try
                {
                    using var parentProc = Process.GetProcessById(parentPid);
                    parentName = parentProc.ProcessName;
                }
                catch
                {
                    continue; // parent exited
                }

                if (parentName == null)
                    continue;

                // Check the spawn pattern
                if (SuspiciousSpawnPatterns.TryGetValue(parentName, out HashSet<string>? badChildren))
                {
                    if (badChildren.Contains(childName))
                    {
                        RaiseAnomaly(new AnomalyEvent
                        {
                            ProcessName = childName,
                            ProcessId = proc.Id,
                            AnomalyType = "SuspiciousSpawn",
                            Description = $"Suspicious process chain detected: '{parentName}' (PID {parentPid}) " +
                                          $"spawned '{childName}' (PID {proc.Id}). This pattern is commonly " +
                                          "associated with malicious document exploitation or living-off-the-land attacks.",
                            Severity = "Critical",
                            DetectedAt = DateTime.Now,
                        });
                    }
                }
            }
            catch { /* access denied for system processes */ }
            finally
            {
                try { proc.Dispose(); } catch { }
            }
        }
    }

    private static int GetParentProcessId(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}");
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject obj in results)
            {
                object? val = obj["ParentProcessId"];
                if (val != null)
                    return Convert.ToInt32(val);
            }
        }
        catch { }

        return 0;
    }

    // -------------------------------------------------------------------
    //  5. Network connection spike detection
    // -------------------------------------------------------------------

    private void CheckNetworkSpike()
    {
        try
        {
            var ipProps = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            int currentCount = 0;

            try
            {
                currentCount += ipProps.GetActiveTcpConnections().Length;
            }
            catch { }

            try
            {
                currentCount += ipProps.GetActiveTcpListeners().Length;
            }
            catch { }

            double elapsed = (DateTime.Now - _lastNetworkCheck).TotalSeconds;
            if (elapsed < 1) elapsed = 1;

            if (_lastConnectionCount > 0)
            {
                int delta = currentCount - _lastConnectionCount;
                double ratePerSecond = delta / elapsed;

                // If connections grew by more than 50 in the interval, flag it.
                if (delta > 50 && ratePerSecond > 5)
                {
                    RaiseAnomaly(new AnomalyEvent
                    {
                        ProcessName = "Network",
                        ProcessId = 0,
                        AnomalyType = "NetworkSpike",
                        Description = $"Network connection spike detected: {delta} new connections in " +
                                      $"{elapsed:F0} seconds ({ratePerSecond:F1}/sec). Total active: {currentCount}. " +
                                      "This may indicate port scanning, C2 beaconing, or DDoS activity.",
                        Severity = delta > 200 ? "Critical" : "High",
                        DetectedAt = DateTime.Now,
                    });
                }
            }

            _lastConnectionCount = currentCount;
            _lastNetworkCheck = DateTime.Now;
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[BehavioralAnomaly] Network spike check error: {Message}", ex.Message);
        }
    }

    // -------------------------------------------------------------------
    //  System-level metrics
    // -------------------------------------------------------------------

    private void UpdateSystemMetrics()
    {
        // CPU via WMI
        try
        {
            using var cpuSearcher = new ManagementObjectSearcher(
                "SELECT LoadPercentage FROM Win32_Processor");
            using var cpuResults = cpuSearcher.Get();
            foreach (ManagementBaseObject obj in cpuResults)
            {
                _lastSystemCpu = Convert.ToSingle(obj["LoadPercentage"]);
                SystemCpuPercent = Math.Round(_lastSystemCpu, 1);
                break;
            }
        }
        catch { }

        // Memory via Performance Counter or GC info
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject obj in results)
            {
                double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"]);
                if (totalKb > 0)
                {
                    SystemMemoryPercent = Math.Round(((totalKb - freeKb) / totalKb) * 100.0, 1);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[BehavioralAnomaly] Memory metric error: {Message}", ex.Message);
        }
    }

    // -------------------------------------------------------------------
    //  Anomaly emission
    // -------------------------------------------------------------------

    private void RaiseAnomaly(AnomalyEvent anomaly)
    {
        // Deduplicate: suppress the same anomaly type + process within a 60 second window.
        string key = $"{anomaly.AnomalyType}|{anomaly.ProcessId}|{anomaly.ProcessName}";
        DateTime now = DateTime.Now;

        if (_raisedAnomalies.TryGetValue(key, out DateTime lastRaised))
        {
            if ((now - lastRaised).TotalSeconds < 60)
                return; // suppress duplicate
        }

        _raisedAnomalies[key] = now;

        lock (_lock)
        {
            Anomalies.Add(anomaly);
            TotalAnomalies = Anomalies.Count;
            CriticalAnomalies = Anomalies.Count(a =>
                a.Severity is "Critical" or "High");
        }

        SglLogger.Warning(
            "[BehavioralAnomaly] [{Severity}] {Type}: {Description}",
            anomaly.Severity, anomaly.AnomalyType, anomaly.Description);
    }

    // -------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    // -------------------------------------------------------------------
    //  IDisposable
    // -------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        StopMonitoring();

        // No PerformanceCounter to dispose — using WMI queries on demand

        _baselines.Clear();
        _raisedAnomalies.Clear();

        SglLogger.Information("[BehavioralAnomaly] Disposed.");
    }
}
