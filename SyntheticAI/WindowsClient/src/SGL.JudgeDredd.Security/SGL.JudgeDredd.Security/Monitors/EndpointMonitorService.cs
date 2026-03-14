#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security.Monitors;

public class EndpointInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // Service, Port, Driver, Startup, ScheduledTask
    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
    public bool IsSuspicious { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Real endpoint monitoring engine for the SGL SyntheticAI Security Suite.
/// Enumerates Windows services, open ports, kernel drivers, startup programs,
/// and scheduled tasks using native Windows APIs. Flags suspicious items for
/// analyst review.
/// </summary>
public sealed class EndpointMonitorService : IDisposable
{
    // -----------------------------------------------------------------------
    //  Suspicious-item detection data
    // -----------------------------------------------------------------------

    /// <summary>Ports commonly abused by backdoors and C2 frameworks.</summary>
    private static readonly HashSet<int> SuspiciousPorts = new()
    {
        4444,   // Metasploit default
        4445,   // Metasploit alternate
        5555,   // Common backdoor / Android debug bridge
        1337,   // Elite / backdoor
        31337,  // Back Orifice
        8888,   // Common C2
        6666,   // Common backdoor
        6667,   // IRC (C2 channel)
        6697,   // IRC SSL
        9001,   // Tor default
        9050,   // Tor SOCKS proxy
        9051,   // Tor control port
        12345,  // NetBus
        54321,  // Back Orifice 2000
        65535,  // Common test backdoor
        2222,   // SSH alternate (often malicious)
        7777,   // Common C2
        13337,  // Backdoor variant
        4443,   // Alt HTTPS C2
        1234,   // Generic backdoor
    };

    /// <summary>Service name substrings that are suspicious when running from temp/appdata.</summary>
    private static readonly string[] SuspiciousServiceNameFragments =
    {
        "backdoor", "trojan", "rat_", "keylog", "miner",
        "cryptojack", "cobaltstrike", "meterpreter", "beacon",
        "implant", "payload", "shell_", "reverse", "bind_",
    };

    /// <summary>Path fragments that indicate a binary is running from non-standard locations.</summary>
    private static readonly string[] SuspiciousPathFragments =
    {
        @"\temp\",
        @"\tmp\",
        @"\appdata\local\temp\",
        @"\appdata\roaming\",
        @"\downloads\",
        @"\public\",
        @"$recycle.bin",
        @"\programdata\",
    };

    /// <summary>
    /// Well-known system paths for scheduled tasks. Tasks whose executable
    /// path does NOT start with one of these are considered suspicious.
    /// </summary>
    private static readonly string[] TrustedTaskPaths =
    {
        @"C:\Windows\",
        @"C:\Program Files\",
        @"C:\Program Files (x86)\",
        @"%SystemRoot%",
        @"%windir%",
        @"COM handler",       // COM-based tasks shown by schtasks
        @"Multiple Actions",  // aggregated tasks
    };

    // -----------------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------------

    private readonly object _lock = new();
    private bool _disposed;

    // -----------------------------------------------------------------------
    //  Public observable collection & summary properties
    // -----------------------------------------------------------------------

    /// <summary>All discovered endpoints from the most recent scan.</summary>
    public ObservableCollection<EndpointInfo> Endpoints { get; } = new();

    public int TotalServices { get; private set; }
    public int RunningServices { get; private set; }
    public int StoppedServices { get; private set; }
    public int OpenPorts { get; private set; }
    public int InstalledDrivers { get; private set; }
    public int StartupPrograms { get; private set; }
    public int ScheduledTasks { get; private set; }
    public string LastScanTime { get; private set; } = "Never";

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Performs a full endpoint scan: services, open ports, kernel drivers,
    /// startup programs, and scheduled tasks. Results are written into
    /// <see cref="Endpoints"/> and the summary properties are updated.
    /// </summary>
    public async Task ScanEndpointsAsync()
    {
        ThrowIfDisposed();

        SglLogger.Information("[EndpointMonitor] Starting full endpoint scan.");
        var stopwatch = Stopwatch.StartNew();

        var allEndpoints = new List<EndpointInfo>();

        // Run the five scans concurrently where possible.
        var serviceTask = Task.Run(ScanServices);
        var portTask = Task.Run(ScanOpenPorts);
        var driverTask = Task.Run(ScanDrivers);
        var startupTask = Task.Run(ScanStartupPrograms);
        var taskTask = Task.Run(ScanScheduledTasks);

        await Task.WhenAll(serviceTask, portTask, driverTask, startupTask, taskTask)
                  .ConfigureAwait(false);

        List<EndpointInfo> services = serviceTask.Result;
        List<EndpointInfo> ports = portTask.Result;
        List<EndpointInfo> drivers = driverTask.Result;
        List<EndpointInfo> startups = startupTask.Result;
        List<EndpointInfo> tasks = taskTask.Result;

        allEndpoints.AddRange(services);
        allEndpoints.AddRange(ports);
        allEndpoints.AddRange(drivers);
        allEndpoints.AddRange(startups);
        allEndpoints.AddRange(tasks);

        // Update summary counters.
        TotalServices = services.Count;
        RunningServices = services.Count(s => s.Status.Equals("Running", StringComparison.OrdinalIgnoreCase));
        StoppedServices = services.Count(s => s.Status.Equals("Stopped", StringComparison.OrdinalIgnoreCase));
        OpenPorts = ports.Count;
        InstalledDrivers = drivers.Count;
        StartupPrograms = startups.Count;
        ScheduledTasks = tasks.Count;
        LastScanTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Populate the observable collection on the calling context.
        lock (_lock)
        {
            Endpoints.Clear();
            foreach (EndpointInfo ep in allEndpoints)
            {
                Endpoints.Add(ep);
            }
        }

        stopwatch.Stop();

        int suspiciousCount = allEndpoints.Count(e => e.IsSuspicious);
        SglLogger.Information(
            "[EndpointMonitor] Scan completed in {Elapsed}ms. " +
            "Services={Services}, Ports={Ports}, Drivers={Drivers}, " +
            "Startup={Startup}, Tasks={Tasks}, Suspicious={Suspicious}",
            stopwatch.ElapsedMilliseconds,
            TotalServices, OpenPorts, InstalledDrivers,
            StartupPrograms, ScheduledTasks, suspiciousCount);
    }

    /// <summary>
    /// Returns all endpoints from the most recent scan that are flagged as
    /// suspicious.
    /// </summary>
    public List<EndpointInfo> GetSuspiciousEndpoints()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            return Endpoints.Where(e => e.IsSuspicious).ToList();
        }
    }

    // -----------------------------------------------------------------------
    //  1. Windows Services  (System.ServiceProcess.ServiceController)
    // -----------------------------------------------------------------------

    private List<EndpointInfo> ScanServices()
    {
        var results = new List<EndpointInfo>();

        try
        {
            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController svc in services)
            {
                try
                {
                    string status = svc.Status.ToString();

                    // Attempt to read the binary path from the registry so we
                    // can flag services executing from suspicious locations.
                    string imagePath = GetServiceImagePath(svc.ServiceName);

                    string details = $"DisplayName: {svc.DisplayName} | " +
                                     $"StartType: {GetServiceStartType(svc)} | " +
                                     $"ImagePath: {(string.IsNullOrEmpty(imagePath) ? "N/A" : imagePath)}";

                    bool suspicious = IsServiceSuspicious(svc.ServiceName, svc.DisplayName, imagePath, status);

                    results.Add(new EndpointInfo
                    {
                        Name = svc.ServiceName,
                        Type = "Service",
                        Status = status,
                        Details = details,
                        IsSuspicious = suspicious,
                        DetectedAt = DateTime.Now,
                    });

                    if (suspicious)
                    {
                        SglLogger.Warning(
                            "[EndpointMonitor] Suspicious service detected: {Name} ({Status}) Path={Path}",
                            svc.ServiceName, status, imagePath);
                    }
                }
                catch (Exception ex)
                {
                    SglLogger.Debug(
                        "[EndpointMonitor] Could not read service '{Name}': {Message}",
                        svc.ServiceName, ex.Message);
                }
                finally
                {
                    svc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[EndpointMonitor] Failed to enumerate services.", ex);
        }

        return results;
    }

    /// <summary>
    /// Reads the ImagePath value from the service's registry key so we can
    /// inspect where it runs from.
    /// </summary>
    private static string GetServiceImagePath(string serviceName)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);

            return key?.GetValue("ImagePath")?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Safely retrieves the start type of a service. Some services restrict
    /// access, so we swallow exceptions.
    /// </summary>
    private static string GetServiceStartType(ServiceController svc)
    {
        try
        {
            return svc.StartType.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static bool IsServiceSuspicious(string name, string displayName, string imagePath, string status)
    {
        string lowerName = name.ToLowerInvariant();
        string lowerDisplay = displayName.ToLowerInvariant();
        string lowerPath = imagePath.ToLowerInvariant();

        // Check for known-bad name fragments.
        foreach (string fragment in SuspiciousServiceNameFragments)
        {
            if (lowerName.Contains(fragment) || lowerDisplay.Contains(fragment))
                return true;
        }

        // A running service whose binary lives in temp/appdata/downloads is suspect.
        if (status.Equals("Running", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(imagePath))
        {
            foreach (string pathFrag in SuspiciousPathFragments)
            {
                if (lowerPath.Contains(pathFrag))
                    return true;
            }
        }

        return false;
    }

    // -----------------------------------------------------------------------
    //  2. Open Ports  (System.Net.NetworkInformation)
    // -----------------------------------------------------------------------

    private List<EndpointInfo> ScanOpenPorts()
    {
        var results = new List<EndpointInfo>();

        try
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            // --- Active TCP listeners ---
            try
            {
                var tcpListeners = ipProperties.GetActiveTcpListeners();
                foreach (var endpoint in tcpListeners)
                {
                    int port = endpoint.Port;
                    bool suspicious = SuspiciousPorts.Contains(port);

                    results.Add(new EndpointInfo
                    {
                        Name = $"TCP:{port}",
                        Type = "Port",
                        Status = "Listening",
                        Details = $"Protocol: TCP | Address: {endpoint.Address} | Port: {port}",
                        IsSuspicious = suspicious,
                        DetectedAt = DateTime.Now,
                    });

                    if (suspicious)
                    {
                        SglLogger.Warning(
                            "[EndpointMonitor] Suspicious TCP port open: {Port} on {Address}",
                            port, endpoint.Address);
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[EndpointMonitor] Error reading TCP listeners: {Message}", ex.Message);
            }

            // --- Active UDP listeners ---
            try
            {
                var udpListeners = ipProperties.GetActiveUdpListeners();
                foreach (var endpoint in udpListeners)
                {
                    int port = endpoint.Port;
                    bool suspicious = SuspiciousPorts.Contains(port);

                    results.Add(new EndpointInfo
                    {
                        Name = $"UDP:{port}",
                        Type = "Port",
                        Status = "Listening",
                        Details = $"Protocol: UDP | Address: {endpoint.Address} | Port: {port}",
                        IsSuspicious = suspicious,
                        DetectedAt = DateTime.Now,
                    });

                    if (suspicious)
                    {
                        SglLogger.Warning(
                            "[EndpointMonitor] Suspicious UDP port open: {Port} on {Address}",
                            port, endpoint.Address);
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[EndpointMonitor] Error reading UDP listeners: {Message}", ex.Message);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[EndpointMonitor] Failed to enumerate open ports.", ex);
        }

        return results;
    }

    // -----------------------------------------------------------------------
    //  3. Installed Kernel Drivers  (ServiceController.GetDevices)
    // -----------------------------------------------------------------------

    private List<EndpointInfo> ScanDrivers()
    {
        var results = new List<EndpointInfo>();

        try
        {
            ServiceController[] devices = ServiceController.GetDevices();

            foreach (ServiceController device in devices)
            {
                try
                {
                    string status = device.Status.ToString();
                    string imagePath = GetServiceImagePath(device.ServiceName);

                    string details = $"DisplayName: {device.DisplayName} | " +
                                     $"Type: {device.ServiceType} | " +
                                     $"ImagePath: {(string.IsNullOrEmpty(imagePath) ? "N/A" : imagePath)}";

                    bool suspicious = IsDriverSuspicious(device.ServiceName, imagePath);

                    results.Add(new EndpointInfo
                    {
                        Name = device.ServiceName,
                        Type = "Driver",
                        Status = status,
                        Details = details,
                        IsSuspicious = suspicious,
                        DetectedAt = DateTime.Now,
                    });

                    if (suspicious)
                    {
                        SglLogger.Warning(
                            "[EndpointMonitor] Suspicious driver detected: {Name} Path={Path}",
                            device.ServiceName, imagePath);
                    }
                }
                catch (Exception ex)
                {
                    SglLogger.Debug(
                        "[EndpointMonitor] Could not read device '{Name}': {Message}",
                        device.ServiceName, ex.Message);
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[EndpointMonitor] Failed to enumerate drivers.", ex);
        }

        return results;
    }

    private static bool IsDriverSuspicious(string name, string imagePath)
    {
        string lowerName = name.ToLowerInvariant();
        string lowerPath = imagePath.ToLowerInvariant();

        // Drivers should live under %SystemRoot%\System32\drivers.
        // Anything in temp, appdata, downloads, etc. is abnormal.
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            foreach (string pathFrag in SuspiciousPathFragments)
            {
                if (lowerPath.Contains(pathFrag))
                    return true;
            }
        }

        // Known-bad name substrings (rootkit indicators).
        foreach (string fragment in SuspiciousServiceNameFragments)
        {
            if (lowerName.Contains(fragment))
                return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    //  4. Startup Programs  (Registry: Run / RunOnce keys)
    // -----------------------------------------------------------------------

    private static readonly string[] StartupRegistrySubKeys =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
    };

    private List<EndpointInfo> ScanStartupPrograms()
    {
        var results = new List<EndpointInfo>();

        try
        {
            // HKLM keys
            foreach (string subKey in StartupRegistrySubKeys)
            {
                ReadStartupKey(Registry.LocalMachine, "HKLM", subKey, results);
            }

            // HKCU keys
            foreach (string subKey in StartupRegistrySubKeys)
            {
                ReadStartupKey(Registry.CurrentUser, "HKCU", subKey, results);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[EndpointMonitor] Failed to enumerate startup programs.", ex);
        }

        return results;
    }

    private void ReadStartupKey(
        RegistryKey hive,
        string hiveName,
        string subKeyPath,
        List<EndpointInfo> results)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKeyPath, writable: false);
            if (key is null)
                return;

            string[] valueNames = key.GetValueNames();

            foreach (string valueName in valueNames)
            {
                if (string.IsNullOrWhiteSpace(valueName))
                    continue;

                string value = key.GetValue(valueName)?.ToString() ?? string.Empty;
                string fullKeyPath = $@"{hiveName}\{subKeyPath}";

                bool suspicious = IsStartupEntrySuspicious(valueName, value);

                results.Add(new EndpointInfo
                {
                    Name = valueName,
                    Type = "Startup",
                    Status = "Enabled",
                    Details = $"Key: {fullKeyPath} | Command: {value}",
                    IsSuspicious = suspicious,
                    DetectedAt = DateTime.Now,
                });

                if (suspicious)
                {
                    SglLogger.Warning(
                        "[EndpointMonitor] Suspicious startup entry: {Name} => {Value} (Key: {Key})",
                        valueName, value, fullKeyPath);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Debug(
                "[EndpointMonitor] Could not read registry key {Hive}\\{SubKey}: {Message}",
                hiveName, subKeyPath, ex.Message);
        }
    }

    private static bool IsStartupEntrySuspicious(string name, string command)
    {
        string lowerName = name.ToLowerInvariant();
        string lowerCommand = command.ToLowerInvariant();

        // Flag entries whose binary path points to temp/appdata/downloads.
        foreach (string pathFrag in SuspiciousPathFragments)
        {
            if (lowerCommand.Contains(pathFrag))
                return true;
        }

        // Flag entries with known-bad substrings.
        foreach (string fragment in SuspiciousServiceNameFragments)
        {
            if (lowerName.Contains(fragment) || lowerCommand.Contains(fragment))
                return true;
        }

        // Flag entries using suspicious interpreters with encoded commands.
        if (lowerCommand.Contains("powershell") && lowerCommand.Contains("-encodedcommand"))
            return true;

        if (lowerCommand.Contains("powershell") && lowerCommand.Contains("-e "))
            return true;

        if (lowerCommand.Contains("cmd") && lowerCommand.Contains("/c") && lowerCommand.Contains("start"))
            return true;

        // Flag entries referencing scripts in user writable directories.
        if ((lowerCommand.Contains(".vbs") || lowerCommand.Contains(".js") || lowerCommand.Contains(".bat") || lowerCommand.Contains(".ps1"))
            && (lowerCommand.Contains(@"\users\") || lowerCommand.Contains(@"\temp\")))
            return true;

        return false;
    }

    // -----------------------------------------------------------------------
    //  5. Scheduled Tasks  (schtasks.exe /query /fo CSV /nh)
    // -----------------------------------------------------------------------

    private List<EndpointInfo> ScanScheduledTasks()
    {
        var results = new List<EndpointInfo>();

        try
        {
            string csv = RunSchtasksQuery();

            if (string.IsNullOrWhiteSpace(csv))
            {
                SglLogger.Debug("[EndpointMonitor] schtasks returned no output.");
                return results;
            }

            string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // CSV format: "TaskName","Next Run Time","Status"
                // Fields are quoted; split the easy way.
                string[] fields = ParseCsvLine(line);

                if (fields.Length < 3)
                    continue;

                string taskName = fields[0];
                string nextRun = fields[1];
                string status = fields[2];

                // Skip header-like rows that schtasks sometimes emits even with /nh.
                if (taskName.Equals("TaskName", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Try to get the command (field index 7 in full /V output). With
                // minimal output we only have 3 fields, so the detail is the
                // task name path itself.
                string details = $"NextRun: {nextRun} | Status: {status}";

                bool suspicious = IsScheduledTaskSuspicious(taskName);

                results.Add(new EndpointInfo
                {
                    Name = taskName,
                    Type = "ScheduledTask",
                    Status = status,
                    Details = details,
                    IsSuspicious = suspicious,
                    DetectedAt = DateTime.Now,
                });

                if (suspicious)
                {
                    SglLogger.Warning(
                        "[EndpointMonitor] Suspicious scheduled task detected: {TaskName} Status={Status}",
                        taskName, status);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[EndpointMonitor] Failed to enumerate scheduled tasks.", ex);
        }

        return results;
    }

    /// <summary>
    /// Executes <c>schtasks /query /fo CSV /nh</c> and captures stdout.
    /// </summary>
    private static string RunSchtasksQuery()
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = "/query /fo CSV /nh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();

            // Give it a generous timeout; on machines with many tasks this can
            // take a few seconds.
            proc.WaitForExit(15_000);

            return output;
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[EndpointMonitor] schtasks execution failed: {Message}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Minimal CSV line parser that handles double-quoted fields.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        // Add the last field.
        fields.Add(current.ToString().Trim());

        return fields.ToArray();
    }

    private static bool IsScheduledTaskSuspicious(string taskName)
    {
        string lowerTask = taskName.ToLowerInvariant();

        // Tasks under well-known Microsoft/Windows folders are generally safe.
        if (lowerTask.StartsWith(@"\microsoft\") || lowerTask.StartsWith(@"\windows\"))
            return false;

        // Flag tasks that reference temp/user paths in their name.
        foreach (string pathFrag in SuspiciousPathFragments)
        {
            if (lowerTask.Contains(pathFrag))
                return true;
        }

        // Flag tasks with names that match known-bad fragments.
        foreach (string fragment in SuspiciousServiceNameFragments)
        {
            if (lowerTask.Contains(fragment))
                return true;
        }

        // Tasks at the root level (e.g. "\SomeRandomName") without a folder
        // prefix are unusual on a stock Windows install.
        if (taskName.StartsWith(@"\") && taskName.IndexOf('\\', 1) < 0)
        {
            // Single-level root task. Many legitimate apps register here too,
            // so we only flag if the name looks random (contains a GUID-like
            // substring or is very short and non-alphabetic).
            if (LooksLikeRandomName(taskName.TrimStart('\\')))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Heuristic: names that are mostly hex characters or contain braces
    /// (GUIDs) are likely auto-generated by malware.
    /// </summary>
    private static bool LooksLikeRandomName(string name)
    {
        if (name.Length == 0)
            return false;

        // Contains braces => GUID-like.
        if (name.Contains('{') || name.Contains('}'))
            return true;

        // More than 60% hex characters in a name longer than 12 chars.
        if (name.Length > 12)
        {
            int hexCount = name.Count(c => "0123456789abcdefABCDEF-".Contains(c));
            double ratio = (double)hexCount / name.Length;
            if (ratio > 0.6)
                return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    //  IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            Endpoints.Clear();
        }

        SglLogger.Information("[EndpointMonitor] Disposed.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EndpointMonitorService));
    }
}
