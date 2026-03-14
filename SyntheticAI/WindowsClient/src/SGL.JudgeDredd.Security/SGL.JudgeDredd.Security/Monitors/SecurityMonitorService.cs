using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.Security.Monitors;

public sealed class SecurityMonitorService : ISecurityMonitor, IProcessMonitor, IDisposable
{
    private Timer? _processTimer;
    private Timer? _usbTimer;
    private Timer? _networkTimer;
    private Timer? _registryTimer;

    private readonly ConcurrentBag<SecurityAlert> _activeAlerts = new();
    private readonly ConcurrentDictionary<int, ProcessInfo> _cachedProcesses = new();
    private readonly HashSet<string> _knownDriveLetters = new();
    private readonly Dictionary<string, string> _knownRegistryEntries = new();
    private readonly HashSet<string> _raisedAlertKeys = new();
    private readonly object _alertKeyLock = new();
    private readonly object _driveLock = new();
    private readonly object _registryLock = new();

    private volatile bool _running;
    private readonly string _ownProcessName;

    /// <summary>
    /// Toggle for remote access tool detection (TeamViewer, AnyDesk, etc.).
    /// When false, remote access processes are not flagged as suspicious.
    /// </summary>
    public bool EnableRemoteAccessDetection { get; set; } = true;

    /// <summary>
    /// Toggle for USB/BadUSB device detection.
    /// When false, new removable drive alerts are suppressed.
    /// </summary>
    public bool EnableBadUsbDetection { get; set; } = true;

    /// <summary>
    /// Toggle for unauthorized AI/LLM process detection.
    /// When false, AI processes like Ollama or LM Studio are not flagged.
    /// </summary>
    public bool EnableAiDetection { get; set; } = true;

    /// <summary>
    /// Toggle for startup registry key monitoring.
    /// When false, modifications to Run/RunOnce keys are not monitored.
    /// </summary>
    public bool EnableRegistryWatcher { get; set; } = true;

    public event EventHandler<SecurityAlertEvent>? AlertRaised;
    public event EventHandler<ProcessInfo>? SuspiciousProcessDetected;

    private static readonly HashSet<string> MaliciousProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mimikatz",
        "cobaltstrike",
        "meterpreter",
        "rubeus",
        "psexec",
        "lazagne",
        "sharphound",
        "bloodhound",
        "hashcat",
        "hydra",
        "john",
        "ncrack",
        "crackmapexec",
        "impacket",
        "responder",
        "empire",
        "powershell_empire",
        "covenant",
        "sliver",
        "brute",
        "bruteforce",
        "pwdump",
        "procdump",
        "nanodump",
        "safetykatz",
        "sharpkatz",
        "sharpdpapi",
        "seatbelt",
        "winpeas",
        "linpeas",
        "chisel",
        "ligolo",
        "plink",
        "netcat",
        "ncat",
        "nc",
        "wce",
        "gsecdump",
        "fgdump",
        "cachedump"
    };

    private static readonly HashSet<string> RemoteAccessProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "teamviewer",
        "teamviewer_service",
        "anydesk",
        "vncserver",
        "vncviewer",
        "tvnserver",
        "tvnviewer",
        "winvnc",
        "ammyy",
        "ammyy_admin",
        "rustdesk",
        "ultraviewer",
        "supremo",
        "logmein",
        "connectwise",
        "screenconnect",
        "splashtop",
        "bomgar",
        "dameware",
        "radmin",
        "remotepc",
        "gotomypc",
        "dwrcs",
        "dwrcst"
    };

    private static readonly HashSet<string> UnauthorizedAiProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ollama",
        "text-generation-webui",
        "koboldcpp",
        "llamacpp",
        "llama-server",
        "llama-cli",
        "oobabooga",
        "localai",
        "gpt4all",
        "lmstudio",
        "jan",
        "faraday",
        "privateGPT",
        "h2ogpt",
        "mlc-chat",
        "chatglm",
        "tabbyml",
        "tabby"
    };

    private static readonly HashSet<int> SuspiciousPortNumbers = new()
    {
        4444,   // Metasploit default
        4445,   // Metasploit alternate
        5555,   // Common backdoor / Android debug
        8888,   // Common C2
        1234,   // Common backdoor
        1337,   // Elite / backdoor
        31337,  // Back Orifice
        6666,   // Common backdoor
        6667,   // IRC (C2 channel)
        6697,   // IRC SSL
        9001,   // Tor default
        9050,   // Tor SOCKS
        9051,   // Tor control
        12345,  // NetBus
        54321,  // Back Orifice 2000
        65535,  // Common test backdoor
        2222,   // SSH alternate (often malicious)
        7777,   // Common C2
        8080,   // Alt HTTP (sometimes C2)
        13337,  // Backdoor
        4443,   // Alt HTTPS (C2)
        8443    // Alt HTTPS (C2)
    };

    private static readonly string[] StartupRegistryPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServices",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServicesOnce",
        @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce"
    };

    private static readonly string[] SuspiciousPathFragments =
    {
        @"\temp\",
        @"\tmp\",
        @"\appdata\local\temp\",
        @"\downloads\",
        @"\public\",
        @"\programdata\",
        @"$recycle.bin"
    };

    public SecurityMonitorService()
    {
        _ownProcessName = Process.GetCurrentProcess().ProcessName;
        InitializeKnownDrives();
        InitializeRegistryBaseline();
    }

    public void StartAllMonitors()
    {
        if (_running) return;
        _running = true;

        _processTimer = new Timer(ProcessMonitorCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        _usbTimer = new Timer(UsbMonitorCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
        _networkTimer = new Timer(NetworkMonitorCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        _registryTimer = new Timer(RegistryMonitorCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    public void StopAllMonitors()
    {
        _running = false;

        _processTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _usbTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _networkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _registryTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        _processTimer?.Dispose();
        _usbTimer?.Dispose();
        _networkTimer?.Dispose();
        _registryTimer?.Dispose();

        _processTimer = null;
        _usbTimer = null;
        _networkTimer = null;
        _registryTimer = null;
    }

    public IReadOnlyList<SecurityAlert> GetActiveAlerts()
    {
        return _activeAlerts.Where(a => !a.IsAcknowledged).ToList().AsReadOnly();
    }

    public IReadOnlyList<ProcessInfo> GetRunningProcesses()
    {
        return _cachedProcesses.Values.ToList().AsReadOnly();
    }

    public ProcessInfo? GetProcessByName(string name)
    {
        return _cachedProcesses.Values.FirstOrDefault(
            p => p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsProcessSuspicious(ProcessInfo process)
    {
        string lowerName = process.ProcessName.ToLowerInvariant();

        if (MaliciousProcessNames.Contains(lowerName))
            return true;

        if (RemoteAccessProcessNames.Contains(lowerName))
            return true;

        if (UnauthorizedAiProcessNames.Contains(lowerName))
            return true;

        if (IsRunningFromSuspiciousPath(process.ExecutablePath))
            return true;

        return false;
    }

    private void ProcessMonitorCallback(object? state)
    {
        if (!_running) return;

        try
        {
            Process[] processes = Process.GetProcesses();
            var currentPids = new HashSet<int>();

            foreach (Process proc in processes)
            {
                try
                {
                    currentPids.Add(proc.Id);

                    string? exePath = null;
                    string? commandLine = null;
                    DateTime? startTime = null;
                    long memoryMb = 0;

                    try { exePath = proc.MainModule?.FileName; } catch { }
                    try { startTime = proc.StartTime; } catch { }
                    try { memoryMb = proc.WorkingSet64 / (1024 * 1024); } catch { }

                    // Fetch command line via WMI (required for detecting encoded PowerShell, LOLBin abuse, etc.)
                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            using var searcher = new ManagementObjectSearcher(
                                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}");
                            using var results = searcher.Get();
                            foreach (ManagementBaseObject obj in results)
                            {
                                commandLine = obj["CommandLine"]?.ToString();
                                break;
                            }
                        }
                    }
                    catch { /* Access denied for elevated/system processes is expected */ }

                    var info = new ProcessInfo
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        ExecutablePath = exePath,
                        CommandLine = commandLine,
                        ParentProcessId = GetParentProcessId(proc),
                        StartTime = startTime,
                        CpuUsage = 0,
                        MemoryUsageMb = memoryMb,
                        IsSuspicious = false,
                        SuspicionReason = null
                    };

                    try
                    {
                        info.UserName = GetProcessOwner(proc.Id);
                    }
                    catch { }

                    EvaluateProcess(info);
                    _cachedProcesses.AddOrUpdate(proc.Id, info, (_, __) => info);
                }
                catch
                {
                    // Access denied for system processes is expected
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }

            // Remove stale entries for processes that no longer exist
            foreach (int pid in _cachedProcesses.Keys)
            {
                if (!currentPids.Contains(pid))
                {
                    _cachedProcesses.TryRemove(pid, out _);
                }
            }
        }
        catch
        {
            // Swallow top-level exceptions to keep the timer alive
        }
    }

    private void EvaluateProcess(ProcessInfo info)
    {
        string name = info.ProcessName;

        // Skip our own process
        if (name.Equals(_ownProcessName, StringComparison.OrdinalIgnoreCase))
            return;

        if (MaliciousProcessNames.Contains(name))
        {
            info.IsSuspicious = true;
            info.SuspicionReason = $"Known malicious tool detected: {name}";
            RaiseProcessAlert(info, AlertCategory.SuspiciousProcess, ThreatSeverity.Critical,
                $"Malicious Tool Detected: {name}",
                $"The process '{name}' (PID {info.ProcessId}) is a known offensive security tool. " +
                $"This process is commonly used in cyberattacks for credential theft, lateral movement, or exploitation.");
        }
        else if (EnableRemoteAccessDetection && RemoteAccessProcessNames.Contains(name))
        {
            info.IsSuspicious = true;
            info.SuspicionReason = $"Remote access tool detected: {name}";
            RaiseProcessAlert(info, AlertCategory.RemoteAccess, ThreatSeverity.High,
                $"Remote Access Tool Detected: {name}",
                $"The remote access application '{name}' (PID {info.ProcessId}) is running. " +
                $"Unauthorized remote access tools can be used to control this machine remotely.");
        }
        else if (EnableAiDetection && UnauthorizedAiProcessNames.Contains(name))
        {
            info.IsSuspicious = true;
            info.SuspicionReason = $"Unauthorized AI/LLM process detected: {name}";
            RaiseProcessAlert(info, AlertCategory.UnauthorizedAiLlm, ThreatSeverity.Medium,
                $"Unauthorized AI/LLM Process: {name}",
                $"An unauthorized AI or LLM application '{name}' (PID {info.ProcessId}) is running locally. " +
                $"Local AI models can be used for data exfiltration or to bypass security controls.");
        }
        else if (IsRunningFromSuspiciousPath(info.ExecutablePath))
        {
            info.IsSuspicious = true;
            info.SuspicionReason = $"Process running from suspicious location: {info.ExecutablePath}";
            RaiseProcessAlert(info, AlertCategory.SuspiciousProcess, ThreatSeverity.Medium,
                $"Process in Suspicious Location: {name}",
                $"The process '{name}' (PID {info.ProcessId}) is executing from a suspicious path: " +
                $"'{info.ExecutablePath}'. Malware commonly runs from temporary or user-writable directories.");
        }

        // Check command line for suspicious patterns (encoded commands, download cradles, LOLBin abuse)
        if (!info.IsSuspicious && !string.IsNullOrEmpty(info.CommandLine))
        {
            string? matchedPattern = DetectSuspiciousCommandLine(info.CommandLine);
            if (matchedPattern != null)
            {
                info.IsSuspicious = true;
                info.SuspicionReason = $"Suspicious command-line detected: {matchedPattern}";
                RaiseProcessAlert(info, AlertCategory.SuspiciousProcess, ThreatSeverity.High,
                    $"Suspicious Command Line: {name}",
                    $"The process '{name}' (PID {info.ProcessId}) was launched with suspicious arguments matching pattern '{matchedPattern}'. " +
                    $"Command line: '{Truncate(info.CommandLine, 300)}'. This may indicate a fileless attack or living-off-the-land technique.");
            }
        }

        if (info.IsSuspicious)
        {
            SuspiciousProcessDetected?.Invoke(this, info);
        }
    }

    private static readonly string[] SuspiciousCommandLinePatterns =
    {
        "-encodedcommand",
        "-enc ",
        "bypass",
        "-noprofile",
        "-windowstyle hidden",
        "-w hidden",
        "downloadstring",
        "downloadfile",
        "invoke-expression",
        "iex ",
        "iex(",
        "invoke-webrequest",
        "webclient",
        "net user ",
        "net localgroup",
        "reg add",
        "schtasks /create",
        "vssadmin delete",
        "wmic shadowcopy",
        "bcdedit /set",
        "bitsadmin /transfer",
        "certutil -urlcache",
        "certutil -decode",
        "mshta vbscript",
        "mshta javascript",
        "regsvr32 /s /n /u /i:",
        "rundll32 javascript",
        "wmic process call create",
        "powershell -ep bypass",
        "powershell -exec bypass",
        "frombase64string",
        "convertto-securestring",
        "system.reflection.assembly",
        "bitstransfer",
        "start-bitstransfer"
    };

    private static string? DetectSuspiciousCommandLine(string commandLine)
    {
        string lowerCmd = commandLine.ToLowerInvariant();
        foreach (string pattern in SuspiciousCommandLinePatterns)
        {
            if (lowerCmd.Contains(pattern))
                return pattern;
        }
        return null;
    }

    private void UsbMonitorCallback(object? state)
    {
        if (!_running) return;
        if (!EnableBadUsbDetection) return;

        try
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in allDrives)
            {
                if (drive.DriveType != DriveType.Removable)
                    continue;

                bool isNew;
                lock (_driveLock)
                {
                    isNew = _knownDriveLetters.Add(drive.Name);
                }

                if (isNew)
                {
                    string label = string.Empty;
                    long sizeMb = 0;
                    try
                    {
                        if (drive.IsReady)
                        {
                            label = drive.VolumeLabel;
                            sizeMb = drive.TotalSize / (1024 * 1024);
                        }
                    }
                    catch { }

                    string alertKey = $"USB-{drive.Name}";
                    if (TryMarkAlertRaised(alertKey))
                    {
                        var alert = new SecurityAlert
                        {
                            AlertId = Guid.NewGuid(),
                            Category = AlertCategory.BadUsb,
                            Severity = ThreatSeverity.High,
                            Title = $"New USB Drive Detected: {drive.Name}",
                            Description = $"A new removable USB drive has been connected at '{drive.Name}'" +
                                          (string.IsNullOrEmpty(label) ? "" : $" (Volume: {label})") +
                                          (sizeMb > 0 ? $", Size: {sizeMb} MB" : "") +
                                          ". USB devices can carry malware or be used for data exfiltration. " +
                                          "BadUSB attacks can emulate keyboards to execute malicious commands.",
                            FilePath = drive.Name,
                            DetectedAt = DateTime.UtcNow,
                            IsAcknowledged = false
                        };

                        PublishAlert(alert);
                    }
                }
            }

            // Detect removed drives to allow re-alerting if the same letter is reused
            lock (_driveLock)
            {
                var currentRemovable = new HashSet<string>(
                    allDrives.Where(d => d.DriveType == DriveType.Removable).Select(d => d.Name));

                var removed = _knownDriveLetters.Where(d => !currentRemovable.Contains(d)).ToList();
                foreach (string gone in removed)
                {
                    _knownDriveLetters.Remove(gone);
                    lock (_alertKeyLock)
                    {
                        _raisedAlertKeys.Remove($"USB-{gone}");
                    }
                }
            }
        }
        catch
        {
            // Swallow to keep timer alive
        }
    }

    private void NetworkMonitorCallback(object? state)
    {
        if (!_running) return;

        try
        {
            string netstatOutput = RunNetstat();
            if (string.IsNullOrWhiteSpace(netstatOutput))
                return;

            var connectionCounts = new Dictionary<int, int>();
            string[] lines = netstatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                // We only care about TCP ESTABLISHED or UDP lines with process IDs
                if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
                    continue;

                ParseNetstatLine(line, connectionCounts);
            }

            // Alert on processes with excessive outbound connections (more than 50)
            foreach (var kvp in connectionCounts)
            {
                if (kvp.Value > 50)
                {
                    string alertKey = $"NET-EXCESSIVE-{kvp.Key}";
                    if (TryMarkAlertRaised(alertKey))
                    {
                        string procName = GetProcessNameById(kvp.Key);
                        var alert = new SecurityAlert
                        {
                            AlertId = Guid.NewGuid(),
                            Category = AlertCategory.UnusualNetworkActivity,
                            Severity = ThreatSeverity.Medium,
                            Title = $"Excessive Connections: {procName}",
                            Description = $"Process '{procName}' (PID {kvp.Key}) has {kvp.Value} active network connections. " +
                                          "This may indicate data exfiltration, a DDoS bot, or command-and-control communication.",
                            ProcessName = procName,
                            ProcessId = kvp.Key,
                            DetectedAt = DateTime.UtcNow,
                            IsAcknowledged = false
                        };
                        PublishAlert(alert);
                    }
                }
            }
        }
        catch
        {
            // Swallow to keep timer alive
        }
    }

    private void ParseNetstatLine(string line, Dictionary<int, int> connectionCounts)
    {
        // Netstat -ano output format:
        // TCP    0.0.0.0:135       0.0.0.0:0       LISTENING       1234
        // TCP    192.168.1.5:49234 93.184.216.34:443 ESTABLISHED   5678
        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4)
            return;

        bool isTcp = parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase);
        // For TCP: proto local foreign state pid
        // For UDP: proto local *:*  pid
        int pidIndex = isTcp ? (parts.Length >= 5 ? 4 : -1) : (parts.Length >= 4 ? 3 : -1);
        if (pidIndex < 0 || pidIndex >= parts.Length)
            return;

        if (!int.TryParse(parts[pidIndex], out int pid) || pid == 0)
            return;

        // Track connection counts
        if (connectionCounts.ContainsKey(pid))
            connectionCounts[pid]++;
        else
            connectionCounts[pid] = 1;

        string foreignAddress = isTcp && parts.Length >= 3 ? parts[2] : string.Empty;

        if (!string.IsNullOrEmpty(foreignAddress) && foreignAddress.Contains(':'))
        {
            string[] addrParts = foreignAddress.Split(':');
            string ip = addrParts[0];

            if (int.TryParse(addrParts[^1], out int remotePort))
            {
                // Check for suspicious C2 ports
                if (SuspiciousPortNumbers.Contains(remotePort) && !IsLocalAddress(ip))
                {
                    string alertKey = $"NET-C2-{pid}-{remotePort}";
                    if (TryMarkAlertRaised(alertKey))
                    {
                        string procName = GetProcessNameById(pid);
                        var alert = new SecurityAlert
                        {
                            AlertId = Guid.NewGuid(),
                            Category = AlertCategory.UnusualNetworkActivity,
                            Severity = ThreatSeverity.High,
                            Title = $"Suspicious C2 Port Connection: {procName}",
                            Description = $"Process '{procName}' (PID {pid}) is connected to {foreignAddress}. " +
                                          $"Remote port {remotePort} is commonly associated with command-and-control (C2) frameworks.",
                            ProcessName = procName,
                            ProcessId = pid,
                            RemoteAddress = foreignAddress,
                            DetectedAt = DateTime.UtcNow,
                            IsAcknowledged = false
                        };
                        PublishAlert(alert);
                    }
                }

                // Local port parse for RDP detection
                string localAddress = parts.Length >= 2 ? parts[1] : string.Empty;
                if (!string.IsNullOrEmpty(localAddress) && localAddress.Contains(':'))
                {
                    string[] localParts = localAddress.Split(':');
                    if (int.TryParse(localParts[^1], out int localPort) && localPort == 3389)
                    {
                        // RDP connection from external IP
                        if (!IsLocalAddress(ip) && !string.Equals(ip, "0.0.0.0") && !string.Equals(ip, "*"))
                        {
                            string state = isTcp && parts.Length >= 4 ? parts[3] : "";
                            if (state.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase))
                            {
                                string alertKey = $"NET-RDP-{ip}";
                                if (TryMarkAlertRaised(alertKey))
                                {
                                    var alert = new SecurityAlert
                                    {
                                        AlertId = Guid.NewGuid(),
                                        Category = AlertCategory.RemoteAccess,
                                        Severity = ThreatSeverity.Critical,
                                        Title = $"External RDP Connection from {ip}",
                                        Description = $"An RDP session (port 3389) is established from external IP {ip}. " +
                                                      "Unauthorized RDP access is a common attack vector for ransomware and lateral movement.",
                                        ProcessId = pid,
                                        RemoteAddress = foreignAddress,
                                        DetectedAt = DateTime.UtcNow,
                                        IsAcknowledged = false
                                    };
                                    PublishAlert(alert);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void RegistryMonitorCallback(object? state)
    {
        if (!_running) return;
        if (!EnableRegistryWatcher) return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            foreach (string subKeyPath in StartupRegistryPaths)
            {
                ScanRegistryHive(Registry.CurrentUser, "HKCU", subKeyPath);
                ScanRegistryHive(Registry.LocalMachine, "HKLM", subKeyPath);
            }
        }
        catch
        {
            // Swallow to keep timer alive
        }
    }

    private void ScanRegistryHive(RegistryKey hive, string hiveName, string subKeyPath)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKeyPath, writable: false);
            if (key == null)
                return;

            string[] valueNames = key.GetValueNames();
            foreach (string valueName in valueNames)
            {
                if (string.IsNullOrEmpty(valueName))
                    continue;

                string fullKeyPath = $"{hiveName}\\{subKeyPath}";
                string compositeKey = $"{fullKeyPath}\\{valueName}";
                string? currentValue = key.GetValue(valueName)?.ToString() ?? string.Empty;

                lock (_registryLock)
                {
                    if (_knownRegistryEntries.TryGetValue(compositeKey, out string? previousValue))
                    {
                        if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
                        {
                            // Value changed
                            _knownRegistryEntries[compositeKey] = currentValue;

                            string alertKey = $"REG-CHG-{compositeKey}";
                            if (TryMarkAlertRaised(alertKey))
                            {
                                var alert = new SecurityAlert
                                {
                                    AlertId = Guid.NewGuid(),
                                    Category = AlertCategory.RegistryChange,
                                    Severity = ThreatSeverity.High,
                                    Title = $"Startup Registry Value Modified: {valueName}",
                                    Description = $"The startup registry entry '{valueName}' under '{fullKeyPath}' was modified. " +
                                                  $"Previous value: '{Truncate(previousValue, 200)}'. " +
                                                  $"New value: '{Truncate(currentValue, 200)}'. " +
                                                  "Malware commonly modifies startup keys to achieve persistence.",
                                    RegistryKey = compositeKey,
                                    FilePath = currentValue,
                                    DetectedAt = DateTime.UtcNow,
                                    IsAcknowledged = false
                                };
                                PublishAlert(alert);
                            }
                        }
                    }
                    else
                    {
                        // New entry not seen in baseline
                        _knownRegistryEntries[compositeKey] = currentValue;

                        string alertKey = $"REG-NEW-{compositeKey}";
                        if (TryMarkAlertRaised(alertKey))
                        {
                            var alert = new SecurityAlert
                            {
                                AlertId = Guid.NewGuid(),
                                Category = AlertCategory.RegistryChange,
                                Severity = ThreatSeverity.High,
                                Title = $"New Startup Registry Entry: {valueName}",
                                Description = $"A new startup entry '{valueName}' was added to '{fullKeyPath}' " +
                                              $"with value: '{Truncate(currentValue, 200)}'. " +
                                              "New auto-start entries can indicate malware persistence.",
                                RegistryKey = compositeKey,
                                FilePath = currentValue,
                                DetectedAt = DateTime.UtcNow,
                                IsAcknowledged = false
                            };
                            PublishAlert(alert);
                        }
                    }
                }
            }
        }
        catch
        {
            // Some keys may be inaccessible; continue scanning others
        }
    }

    private void InitializeKnownDrives()
    {
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    lock (_driveLock)
                    {
                        _knownDriveLetters.Add(drive.Name);
                    }
                }
            }
        }
        catch { }
    }

    private void InitializeRegistryBaseline()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            foreach (string subKeyPath in StartupRegistryPaths)
            {
                BaselineRegistryHive(Registry.CurrentUser, "HKCU", subKeyPath);
                BaselineRegistryHive(Registry.LocalMachine, "HKLM", subKeyPath);
            }
        }
        catch { }
    }

    private void BaselineRegistryHive(RegistryKey hive, string hiveName, string subKeyPath)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKeyPath, writable: false);
            if (key == null)
                return;

            foreach (string valueName in key.GetValueNames())
            {
                if (string.IsNullOrEmpty(valueName))
                    continue;

                string compositeKey = $"{hiveName}\\{subKeyPath}\\{valueName}";
                string currentValue = key.GetValue(valueName)?.ToString() ?? string.Empty;

                lock (_registryLock)
                {
                    _knownRegistryEntries[compositeKey] = currentValue;
                }
            }
        }
        catch { }
    }

    private void RaiseProcessAlert(ProcessInfo process, AlertCategory category, ThreatSeverity severity,
        string title, string description)
    {
        string alertKey = $"PROC-{category}-{process.ProcessName}-{process.ProcessId}";
        if (!TryMarkAlertRaised(alertKey))
            return;

        var alert = new SecurityAlert
        {
            AlertId = Guid.NewGuid(),
            Category = category,
            Severity = severity,
            Title = title,
            Description = description,
            ProcessName = process.ProcessName,
            ProcessId = process.ProcessId,
            FilePath = process.ExecutablePath,
            DetectedAt = DateTime.UtcNow,
            IsAcknowledged = false
        };

        PublishAlert(alert);
    }

    private void PublishAlert(SecurityAlert alert)
    {
        _activeAlerts.Add(alert);
        AlertRaised?.Invoke(this, new SecurityAlertEvent { Alert = alert });
    }

    private bool TryMarkAlertRaised(string key)
    {
        lock (_alertKeyLock)
        {
            return _raisedAlertKeys.Add(key);
        }
    }

    private static bool IsRunningFromSuspiciousPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        string lowerPath = executablePath.ToLowerInvariant();

        foreach (string fragment in SuspiciousPathFragments)
        {
            if (lowerPath.Contains(fragment))
                return true;
        }

        return false;
    }

    private static bool IsLocalAddress(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return true;

        return ip.StartsWith("127.") ||
               ip.StartsWith("10.") ||
               ip.StartsWith("192.168.") ||
               ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") ||
               ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") ||
               ip.StartsWith("172.20.") ||
               ip.StartsWith("172.21.") ||
               ip.StartsWith("172.22.") ||
               ip.StartsWith("172.23.") ||
               ip.StartsWith("172.24.") ||
               ip.StartsWith("172.25.") ||
               ip.StartsWith("172.26.") ||
               ip.StartsWith("172.27.") ||
               ip.StartsWith("172.28.") ||
               ip.StartsWith("172.29.") ||
               ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.") ||
               ip.Equals("0.0.0.0") ||
               ip.Equals("::") ||
               ip.Equals("::1") ||
               ip.Equals("*");
    }

    private static string RunNetstat()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit(TimeSpan.FromSeconds(8));

            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetParentProcessId(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}");
                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementBaseObject obj in results)
                {
                    object? val = obj["ParentProcessId"];
                    if (val != null)
                        return Convert.ToInt32(val);
                }
            }
        }
        catch { }

        return 0;
    }

    private static string? GetProcessOwner(int processId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementBaseObject obj in results)
                {
                    if (obj is ManagementObject mo)
                    {
                        var args = new object[] { string.Empty, string.Empty };
                        int returnVal = Convert.ToInt32(mo.InvokeMethod("GetOwner", args));
                        if (returnVal == 0)
                        {
                            string domain = args[1]?.ToString() ?? string.Empty;
                            string user = args[0]?.ToString() ?? string.Empty;
                            return string.IsNullOrEmpty(domain) ? user : $"{domain}\\{user}";
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static string GetProcessNameById(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.ProcessName;
        }
        catch
        {
            return $"PID-{pid}";
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    public void Dispose()
    {
        StopAllMonitors();
    }
}
