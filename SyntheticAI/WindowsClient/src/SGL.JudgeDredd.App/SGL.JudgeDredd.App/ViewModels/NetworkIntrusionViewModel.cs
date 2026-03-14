using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

// ──────────────────────────────────────────────────────────────
// Models
// ──────────────────────────────────────────────────────────────

public enum AlertSeverity { Info, Warning, Critical }

public enum AlertType
{
    PortScan,
    SuspiciousConnection,
    DnsAnomaly,
    DataExfiltration,
    BlockedConnection,
    UnusualPort,
    TorExitNode,
    HighPacketRate
}

public enum BlocklistEntryType { IP, Domain, Range }

public partial class NidsConnection : ObservableObject
{
    [ObservableProperty] private string _localAddress = string.Empty;
    [ObservableProperty] private int _localPort;
    [ObservableProperty] private string _remoteAddress = string.Empty;
    [ObservableProperty] private int _remotePort;
    [ObservableProperty] private string _protocol = "TCP";
    [ObservableProperty] private string _state = string.Empty;
    [ObservableProperty] private int _processId;
    [ObservableProperty] private string _processName = string.Empty;
    [ObservableProperty] private bool _isEstablished;
    [ObservableProperty] private bool _isSuspicious;
    [ObservableProperty] private string _suspiciousReason = string.Empty;
    [ObservableProperty] private long _bytesSent;
    [ObservableProperty] private long _bytesReceived;
    [ObservableProperty] private TimeSpan _duration;
}

public partial class NetworkAlert : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private AlertSeverity _severity = AlertSeverity.Info;
    [ObservableProperty] private AlertType _alertType;
    [ObservableProperty] private string _sourceIp = string.Empty;
    [ObservableProperty] private string _destIp = string.Empty;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _protocol = "TCP";
    [ObservableProperty] private string _processName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isAcknowledged;

    public string SeverityDisplay => Severity.ToString().ToUpperInvariant();
    public string AlertTypeDisplay => AlertType switch
    {
        AlertType.PortScan => "Port Scan",
        AlertType.SuspiciousConnection => "Suspicious Connection",
        AlertType.DnsAnomaly => "DNS Anomaly",
        AlertType.DataExfiltration => "Data Exfiltration",
        AlertType.BlockedConnection => "Blocked Connection",
        AlertType.UnusualPort => "Unusual Port",
        AlertType.TorExitNode => "Tor Exit Node",
        AlertType.HighPacketRate => "High Packet Rate",
        _ => AlertType.ToString()
    };
}

public partial class BlocklistEntry : ObservableObject
{
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private BlocklistEntryType _type = BlocklistEntryType.IP;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private DateTime _dateAdded = DateTime.Now;
    [ObservableProperty] private bool _isActive = true;
}

// ──────────────────────────────────────────────────────────────
// ViewModel
// ──────────────────────────────────────────────────────────────

public partial class NetworkIntrusionViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;
    private readonly Dictionary<string, List<DateTime>> _portScanTracker = new();
    private long _lastTotalBytesSent;
    private long _lastTotalBytesReceived;
    private DateTime _lastStatTime = DateTime.Now;

    // ── Observable properties ────────────────────────────────

    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _monitorStatus = "Idle - Click Start to begin network intrusion detection";

    // Stats
    [ObservableProperty] private int _totalConnections;
    [ObservableProperty] private int _suspiciousConnections;
    [ObservableProperty] private int _alertsCount;
    [ObservableProperty] private int _blockedCount;
    [ObservableProperty] private string _packetsPerSecond = "0";
    [ObservableProperty] private string _dataInbound = "0 B";
    [ObservableProperty] private string _dataOutbound = "0 B";

    // Blocklist add entry fields
    [ObservableProperty] private string _newBlocklistAddress = string.Empty;
    [ObservableProperty] private string _newBlocklistReason = string.Empty;

    // ── Collections ──────────────────────────────────────────

    public ObservableCollection<NidsConnection> ActiveConnections { get; } = [];
    public ObservableCollection<NetworkAlert> Alerts { get; } = [];
    public ObservableCollection<BlocklistEntry> Blocklist { get; } = [];

    // ── Threat intelligence ──────────────────────────────────

    private static readonly HashSet<string> KnownMaliciousIpRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        // Known C2 / botnet ranges (example indicators)
        "185.220.100.", "185.220.101.", "185.220.102.",
        "45.154.255.", "45.155.205.", "45.156.23.",
        "23.129.64.",  "23.154.177.",
        "171.25.193.", "198.96.155.", "199.249.230.",
        "209.141.32.", "209.141.33.", "209.141.34.",
        "104.244.72.", "104.244.73.", "104.244.74.", "104.244.75.", "104.244.76.",
        "51.15.0.",    "51.15.1.",    "51.15.2.",
        "46.166.139.", "46.166.142.",
        "91.219.236.", "91.219.237.",
        "62.102.148.", "62.210.105.",
        "77.247.181.", "77.81.247.",
        "178.20.55.",  "176.10.99.",  "176.10.104.",
        "185.100.87.", "185.107.47.", "185.129.62.",
        "193.218.118.", "193.189.100.",
        "89.234.157.", "89.163.131.",
        "192.42.116.", "192.36.27.",
        "141.98.10.",  "141.98.11.",
        "5.199.143.",  "5.2.69.",     "5.2.70.",
        "37.120.198.", "37.120.199.",
        "66.220.242.", "66.146.193.",
        "212.16.104.", "212.21.66.",
        "94.230.208.", "94.142.244.",
    };

    private static readonly HashSet<string> KnownC2Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "evil.com", "malware-c2.net", "darkcomet.cc",
        "poisonivy.biz", "rat-controller.xyz", "botnet-master.ru",
        "c2server.onion.ly", "exfil-data.cc", "apt-group.xyz",
        "malware-proxy.net", "cobaltstrike.evil.com", "beacon-c2.net",
        "meterpreter-c2.xyz", "empire-stager.cc", "covenant-c2.net",
        "sliver-implant.xyz", "brute-ratel.cc", "nighthawk-c2.net",
        "havoc-framework.xyz", "mythic-c2.cc", "poshc2-server.net",
    };

    private static readonly HashSet<int> SuspiciousPorts = new()
    {
        4444, 5555, 6666, 7777, 8888, 9999,  // Common RAT ports
        1234, 31337, 12345, 54321,            // Classic backdoor ports
        4443, 8443, 8080, 9090,               // Alt HTTPS / proxy
        6667, 6668, 6669,                     // IRC (C2 channel)
        3389,                                 // RDP (lateral movement)
        5900, 5901, 5902,                     // VNC
        2222, 2323,                           // Alt SSH/Telnet
        1080, 9050, 9150,                     // SOCKS proxy / Tor
        27017, 6379, 11211,                   // Unprotected databases
    };

    // ── Constructor ──────────────────────────────────────────

    public NetworkIntrusionViewModel()
    {
        Title = "Network Intrusion Detection";
    }

    // ── Commands ─────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
        {
            StopMonitoringInternal();
            await AvatarViewModel.Instance.ShowSpeechBubble("NIDS monitoring stopped.");
        }
        else
        {
            await StartMonitoringInternalAsync();
            await AvatarViewModel.Instance.ShowSpeechBubble("Network intrusion detection activated. I am the law.");
        }
    }

    [RelayCommand]
    private async Task RefreshConnectionsAsync()
    {
        await ScanConnectionsAsync();
    }

    [RelayCommand]
    private void AcknowledgeAlert(NetworkAlert? alert)
    {
        if (alert is null) return;
        alert.IsAcknowledged = true;

        var idx = Alerts.IndexOf(alert);
        if (idx >= 0)
        {
            Alerts.RemoveAt(idx);
            Alerts.Insert(idx, alert);
        }
    }

    [RelayCommand]
    private async Task BlockIpAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"JudgeDredd_Block_{ip}\" dir=in action=block remoteip={ip}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                await proc.WaitForExitAsync();
            }

            // Also block outbound
            var psi2 = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"JudgeDredd_Block_{ip}_Out\" dir=out action=block remoteip={ip}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc2 = Process.Start(psi2);
            if (proc2 is not null)
            {
                await proc2.WaitForExitAsync();
            }

            // Add to blocklist if not already there
            if (!Blocklist.Any(b => b.Address == ip))
            {
                Blocklist.Add(new BlocklistEntry
                {
                    Address = ip,
                    Type = BlocklistEntryType.IP,
                    Reason = "Blocked via NIDS",
                    DateAdded = DateTime.Now,
                    IsActive = true
                });
            }

            BlockedCount = Blocklist.Count(b => b.IsActive);

            RaiseAlert(AlertSeverity.Info, AlertType.BlockedConnection, "", ip, 0, "TCP",
                "System", $"IP {ip} has been blocked via Windows Firewall.");

            await AvatarViewModel.Instance.ShowSpeechBubble($"IP {ip} blocked. Justice served.");
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to block IP {ip}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UnblockIpAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"JudgeDredd_Block_{ip}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is not null) await proc.WaitForExitAsync();

            var psi2 = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"JudgeDredd_Block_{ip}_Out\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc2 = Process.Start(psi2);
            if (proc2 is not null) await proc2.WaitForExitAsync();

            var entry = Blocklist.FirstOrDefault(b => b.Address == ip);
            if (entry is not null) Blocklist.Remove(entry);

            BlockedCount = Blocklist.Count(b => b.IsActive);
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to unblock IP {ip}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddToBlocklistAsync()
    {
        var addr = NewBlocklistAddress?.Trim();
        if (string.IsNullOrWhiteSpace(addr)) return;

        if (Blocklist.Any(b => b.Address.Equals(addr, StringComparison.OrdinalIgnoreCase)))
        {
            MonitorStatus = $"{addr} is already in the blocklist.";
            return;
        }

        var entryType = BlocklistEntryType.IP;
        if (addr.Contains('/')) entryType = BlocklistEntryType.Range;
        else if (!IPAddress.TryParse(addr, out _)) entryType = BlocklistEntryType.Domain;

        var entry = new BlocklistEntry
        {
            Address = addr,
            Type = entryType,
            Reason = string.IsNullOrWhiteSpace(NewBlocklistReason) ? "Manually added" : NewBlocklistReason,
            DateAdded = DateTime.Now,
            IsActive = true
        };

        Blocklist.Add(entry);

        // If it is an IP, also create firewall rule
        if (entryType == BlocklistEntryType.IP || entryType == BlocklistEntryType.Range)
        {
            await BlockIpAsync(addr);
        }

        BlockedCount = Blocklist.Count(b => b.IsActive);
        NewBlocklistAddress = string.Empty;
        NewBlocklistReason = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveFromBlocklistAsync(BlocklistEntry? entry)
    {
        if (entry is null) return;

        if (entry.Type is BlocklistEntryType.IP or BlocklistEntryType.Range)
        {
            await UnblockIpAsync(entry.Address);
        }
        else
        {
            Blocklist.Remove(entry);
        }

        BlockedCount = Blocklist.Count(b => b.IsActive);
    }

    [RelayCommand]
    private async Task ExportAlertsAsync()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = Path.Combine(desktop, $"JudgeDredd_NIDS_Alerts_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Severity,AlertType,SourceIP,DestIP,Port,Protocol,Process,Description,Acknowledged");

            foreach (var a in Alerts)
            {
                sb.AppendLine($"\"{a.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{a.Severity}\",\"{a.AlertTypeDisplay}\"," +
                              $"\"{a.SourceIp}\",\"{a.DestIp}\",{a.Port},\"{a.Protocol}\"," +
                              $"\"{a.ProcessName}\",\"{a.Description.Replace("\"", "\"\"")}\",{a.IsAcknowledged}");
            }

            await File.WriteAllTextAsync(path, sb.ToString());
            MonitorStatus = $"Alerts exported to {path}";
            await AvatarViewModel.Instance.ShowSpeechBubble($"Exported {Alerts.Count} alerts to Desktop.");
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearAlerts()
    {
        Alerts.Clear();
        AlertsCount = 0;
    }

    // ── Monitoring lifecycle ─────────────────────────────────

    private async Task StartMonitoringInternalAsync()
    {
        IsMonitoring = true;
        MonitorStatus = "Starting NIDS monitoring...";

        // Initialize network stats baseline
        InitializeNetworkStats();

        // First scan
        await ScanConnectionsAsync();
        await ScanDnsCacheAsync();

        // Start timer for periodic scanning (every 5 seconds)
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _monitorTimer.Tick += async (_, _) =>
        {
            await ScanConnectionsAsync();
            UpdateNetworkStats();
        };
        _monitorTimer.Start();

        MonitorStatus = "NIDS active - monitoring all network connections";
        AvatarViewModel.Instance.SetExpression(Core.Enums.AvatarExpression.Running);
    }

    private void StopMonitoringInternal()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;
        IsMonitoring = false;
        MonitorStatus = $"Monitoring stopped. {Alerts.Count} alert(s) logged.";
        AvatarViewModel.Instance.SetExpression(Core.Enums.AvatarExpression.Idle);
    }

    // ── Core scanning logic ──────────────────────────────────

    private async Task ScanConnectionsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var connections = ParseNetstatOutput();

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ActiveConnections.Clear();

                    foreach (var conn in connections)
                    {
                        AnalyzeConnection(conn);
                        ActiveConnections.Add(conn);
                    }

                    TotalConnections = ActiveConnections.Count;
                    SuspiciousConnections = ActiveConnections.Count(c => c.IsSuspicious);
                    AlertsCount = Alerts.Count;

                    MonitorStatus = $"NIDS active - {TotalConnections} connections, {SuspiciousConnections} suspicious - {DateTime.Now:HH:mm:ss}";
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

    private List<NidsConnection> ParseNetstatOutput()
    {
        var connections = new List<NidsConnection>();
        var processNameCache = new Dictionary<int, string>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return connections;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("Proto") || line.StartsWith("Active") || string.IsNullOrEmpty(line))
                    continue;

                var parts = Regex.Split(line, @"\s+");
                if (parts.Length < 4) continue;

                var protocol = parts[0].ToUpperInvariant();
                if (protocol is not ("TCP" or "UDP")) continue;

                var localEndpoint = parts[1];
                var remoteEndpoint = parts[2];
                var state = protocol == "TCP" && parts.Length > 3 ? parts[3] : "STATELESS";
                var pidStr = parts[^1]; // last element

                if (!int.TryParse(pidStr, out var pid)) continue;

                ParseEndpoint(localEndpoint, out var localAddr, out var localPort);
                ParseEndpoint(remoteEndpoint, out var remoteAddr, out var remotePort);

                // Resolve process name
                if (!processNameCache.TryGetValue(pid, out var processName))
                {
                    processName = GetProcessName(pid);
                    processNameCache[pid] = processName;
                }

                connections.Add(new NidsConnection
                {
                    LocalAddress = localAddr,
                    LocalPort = localPort,
                    RemoteAddress = remoteAddr,
                    RemotePort = remotePort,
                    Protocol = protocol,
                    State = state,
                    ProcessId = pid,
                    ProcessName = processName,
                    IsEstablished = state.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase),
                });
            }
        }
        catch { /* netstat failed - return empty list */ }

        return connections;
    }

    private static void ParseEndpoint(string endpoint, out string address, out int port)
    {
        // Handle IPv6 [::]:port and IPv4 addr:port
        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon >= 0)
        {
            address = endpoint[..lastColon];
            int.TryParse(endpoint[(lastColon + 1)..], out port);
        }
        else
        {
            address = endpoint;
            port = 0;
        }
    }

    private static string GetProcessName(int pid)
    {
        try
        {
            if (pid == 0) return "System Idle";
            if (pid == 4) return "System";
            using var proc = Process.GetProcessById(pid);
            return proc.ProcessName;
        }
        catch
        {
            return $"PID:{pid}";
        }
    }

    // ── Connection analysis ──────────────────────────────────

    private void AnalyzeConnection(NidsConnection conn)
    {
        var reasons = new List<string>();

        // 1. Check against known malicious IP ranges
        if (IsKnownMaliciousIp(conn.RemoteAddress))
        {
            reasons.Add("Known malicious IP range");
            RaiseAlert(AlertSeverity.Critical, AlertType.SuspiciousConnection,
                conn.LocalAddress, conn.RemoteAddress, conn.RemotePort, conn.Protocol,
                conn.ProcessName, $"Connection to known malicious IP range: {conn.RemoteAddress}:{conn.RemotePort} by {conn.ProcessName}");
        }

        // 2. Check blocklist
        if (IsBlocklisted(conn.RemoteAddress))
        {
            reasons.Add("Blocklisted address");
            RaiseAlert(AlertSeverity.Critical, AlertType.BlockedConnection,
                conn.LocalAddress, conn.RemoteAddress, conn.RemotePort, conn.Protocol,
                conn.ProcessName, $"Connection to blocklisted address: {conn.RemoteAddress} by {conn.ProcessName}");
        }

        // 3. Check suspicious ports
        if (SuspiciousPorts.Contains(conn.RemotePort))
        {
            reasons.Add($"Suspicious port {conn.RemotePort}");
            RaiseAlert(AlertSeverity.Warning, AlertType.UnusualPort,
                conn.LocalAddress, conn.RemoteAddress, conn.RemotePort, conn.Protocol,
                conn.ProcessName, $"{conn.ProcessName} connected to suspicious port {conn.RemotePort} on {conn.RemoteAddress}");
        }

        // 4. Check for Tor exit node patterns (port 9001, 9030, 9050, 9150)
        if (conn.RemotePort is 9001 or 9030 or 9050 or 9150)
        {
            reasons.Add("Potential Tor traffic");
            RaiseAlert(AlertSeverity.Warning, AlertType.TorExitNode,
                conn.LocalAddress, conn.RemoteAddress, conn.RemotePort, conn.Protocol,
                conn.ProcessName, $"Potential Tor network traffic detected from {conn.ProcessName} to {conn.RemoteAddress}:{conn.RemotePort}");
        }

        // 5. Port scan detection - track connection attempts per remote IP
        DetectPortScan(conn);

        // 6. High-entropy remote address check (possible DGA domain resolution)
        if (IsHighEntropyAddress(conn.RemoteAddress))
        {
            reasons.Add("High-entropy remote address (possible DGA)");
        }

        if (reasons.Count > 0)
        {
            conn.IsSuspicious = true;
            conn.SuspiciousReason = string.Join("; ", reasons);
        }
    }

    private bool IsKnownMaliciousIp(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        return KnownMaliciousIpRanges.Any(prefix => ip.StartsWith(prefix, StringComparison.Ordinal));
    }

    private bool IsBlocklisted(string address)
    {
        if (string.IsNullOrEmpty(address)) return false;
        return Blocklist.Any(b => b.IsActive &&
            (b.Address.Equals(address, StringComparison.OrdinalIgnoreCase) ||
             (b.Type == BlocklistEntryType.Range && address.StartsWith(b.Address.Split('/')[0].TrimEnd('.') + ".", StringComparison.Ordinal))));
    }

    private void DetectPortScan(NidsConnection conn)
    {
        if (string.IsNullOrEmpty(conn.RemoteAddress) || conn.RemoteAddress == "0.0.0.0" ||
            conn.RemoteAddress == "*" || conn.RemoteAddress.StartsWith("127."))
            return;

        var key = conn.RemoteAddress;
        if (!_portScanTracker.ContainsKey(key))
            _portScanTracker[key] = [];

        _portScanTracker[key].Add(DateTime.Now);

        // Remove entries older than 10 seconds
        _portScanTracker[key] = _portScanTracker[key]
            .Where(t => (DateTime.Now - t).TotalSeconds < 10)
            .ToList();

        // If more than 15 connection events from same IP in 10 seconds => port scan
        if (_portScanTracker[key].Count > 15)
        {
            // Only alert once per IP per scan cycle
            if (!Alerts.Any(a => a.AlertType == AlertType.PortScan &&
                                 a.SourceIp == conn.RemoteAddress &&
                                 (DateTime.Now - a.Timestamp).TotalSeconds < 30))
            {
                conn.IsSuspicious = true;
                conn.SuspiciousReason = "Port scan detected";

                RaiseAlert(AlertSeverity.Critical, AlertType.PortScan,
                    conn.RemoteAddress, conn.LocalAddress, conn.LocalPort, conn.Protocol,
                    conn.ProcessName,
                    $"Port scan detected from {conn.RemoteAddress} - {_portScanTracker[key].Count} connection events in 10 seconds");
            }
        }
    }

    private static bool IsHighEntropyAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || IPAddress.TryParse(address, out _))
            return false; // Don't check raw IPs, only hostnames

        // Calculate Shannon entropy of the hostname
        var freq = new Dictionary<char, int>();
        foreach (var c in address.ToLowerInvariant())
        {
            if (c == '.') continue;
            freq[c] = freq.GetValueOrDefault(c) + 1;
        }

        var len = address.Replace(".", "").Length;
        if (len < 8) return false;

        double entropy = 0;
        foreach (var count in freq.Values)
        {
            var p = (double)count / len;
            entropy -= p * Math.Log2(p);
        }

        // Entropy > 3.5 on a hostname is suspicious (DGA-like)
        return entropy > 3.5;
    }

    // ── DNS cache analysis ───────────────────────────────────

    private async Task ScanDnsCacheAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/displaydns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                using var proc = Process.Start(psi);
                if (proc is null) return;

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(10_000);

                // Extract record names
                var matches = Regex.Matches(output, @"Record Name[\s.]*:\s*(.+)", RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var domain = match.Groups[1].Value.Trim();
                    AnalyzeDnsDomain(domain);
                }
            }
            catch { /* ipconfig /displaydns failed */ }
        });
    }

    private void AnalyzeDnsDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;

        // Check against known C2 domains
        if (KnownC2Domains.Any(c2 => domain.Contains(c2, StringComparison.OrdinalIgnoreCase)))
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                RaiseAlert(AlertSeverity.Critical, AlertType.DnsAnomaly,
                    "local", domain, 53, "DNS",
                    "DNS Cache", $"Known C2 domain found in DNS cache: {domain}");
            });
            return;
        }

        // DGA detection: check for high-entropy domain parts
        var parts = domain.Split('.');
        if (parts.Length >= 2)
        {
            var mainPart = parts[0];

            // DGA indicators: long random-looking subdomains
            if (mainPart.Length > 12 && IsStringHighEntropy(mainPart))
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    RaiseAlert(AlertSeverity.Warning, AlertType.DnsAnomaly,
                        "local", domain, 53, "DNS",
                        "DNS Cache", $"Possible DGA domain detected: {domain} (high-entropy subdomain)");
                });
            }

            // Excessive subdomain depth (>4 levels can indicate DNS tunneling)
            if (parts.Length > 5)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    RaiseAlert(AlertSeverity.Warning, AlertType.DnsAnomaly,
                        "local", domain, 53, "DNS",
                        "DNS Cache", $"Suspicious DNS depth detected: {domain} ({parts.Length} levels - possible DNS tunneling)");
                });
            }
        }
    }

    private static bool IsStringHighEntropy(string s)
    {
        if (s.Length < 6) return false;

        var freq = new Dictionary<char, int>();
        foreach (var c in s.ToLowerInvariant())
            freq[c] = freq.GetValueOrDefault(c) + 1;

        double entropy = 0;
        foreach (var count in freq.Values)
        {
            var p = (double)count / s.Length;
            entropy -= p * Math.Log2(p);
        }

        // Also check consonant-to-vowel ratio
        var vowels = s.Count(c => "aeiouAEIOU".Contains(c));
        var consonants = s.Count(c => char.IsLetter(c) && !"aeiouAEIOU".Contains(c));
        var ratio = consonants > 0 ? (double)vowels / consonants : 0;

        // Low vowel ratio + high entropy = likely DGA
        return entropy > 3.2 && ratio < 0.3;
    }

    // ── Network statistics ───────────────────────────────────

    private void InitializeNetworkStats()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            _lastTotalBytesSent = 0;
            _lastTotalBytesReceived = 0;

            foreach (var iface in interfaces)
            {
                var stats = iface.GetIPStatistics();
                _lastTotalBytesSent += stats.BytesSent;
                _lastTotalBytesReceived += stats.BytesReceived;
            }

            _lastStatTime = DateTime.Now;
        }
        catch { /* network stats unavailable */ }
    }

    private void UpdateNetworkStats()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            long totalSent = 0;
            long totalReceived = 0;

            foreach (var iface in interfaces)
            {
                var stats = iface.GetIPStatistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
            }

            var elapsed = (DateTime.Now - _lastStatTime).TotalSeconds;
            if (elapsed > 0)
            {
                var sentRate = (long)((totalSent - _lastTotalBytesSent) / elapsed);
                var recvRate = (long)((totalReceived - _lastTotalBytesReceived) / elapsed);

                DataOutbound = FormatRate(sentRate);
                DataInbound = FormatRate(recvRate);

                // Rough packet estimate (average ~500 bytes per packet)
                var totalRate = sentRate + recvRate;
                var pps = totalRate / 500;
                PacketsPerSecond = pps.ToString("N0");

                // Alert on unusually high packet rate (>50,000 pps)
                if (pps > 50_000)
                {
                    if (!Alerts.Any(a => a.AlertType == AlertType.HighPacketRate &&
                                         (DateTime.Now - a.Timestamp).TotalMinutes < 1))
                    {
                        RaiseAlert(AlertSeverity.Warning, AlertType.HighPacketRate,
                            "local", "multiple", 0, "TCP/UDP",
                            "System", $"Unusually high packet rate detected: ~{pps:N0} pps. Possible DDoS or data exfiltration.");
                    }
                }

                // Data exfiltration alert: >100 MB/s outbound
                if (sentRate > 100L * 1024 * 1024)
                {
                    if (!Alerts.Any(a => a.AlertType == AlertType.DataExfiltration &&
                                         (DateTime.Now - a.Timestamp).TotalMinutes < 1))
                    {
                        RaiseAlert(AlertSeverity.Critical, AlertType.DataExfiltration,
                            "local", "multiple", 0, "TCP/UDP",
                            "System", $"Potential data exfiltration: {FormatRate(sentRate)} outbound. Investigate immediately.");
                    }
                }
            }

            _lastTotalBytesSent = totalSent;
            _lastTotalBytesReceived = totalReceived;
            _lastStatTime = DateTime.Now;
        }
        catch { /* stats update failed */ }
    }

    // ── Alert helpers ────────────────────────────────────────

    private void RaiseAlert(AlertSeverity severity, AlertType type, string srcIp, string destIp,
            int port, string protocol, string processName, string description)
    {
        // Deduplicate: don't re-add identical alerts within 60 seconds
        if (Alerts.Any(a => a.AlertType == type &&
                            a.DestIp == destIp &&
                            a.SourceIp == srcIp &&
                            a.Port == port &&
                            (DateTime.Now - a.Timestamp).TotalSeconds < 60))
            return;

        var alert = new NetworkAlert
        {
            Timestamp = DateTime.Now,
            Severity = severity,
            AlertType = type,
            SourceIp = srcIp,
            DestIp = destIp,
            Port = port,
            Protocol = protocol,
            ProcessName = processName,
            Description = description,
        };

        Alerts.Insert(0, alert);
        AlertsCount = Alerts.Count;

        // Limit alerts to 500
        while (Alerts.Count > 500)
            Alerts.RemoveAt(Alerts.Count - 1);

        if (severity == AlertSeverity.Critical)
        {
            AvatarViewModel.Instance.SetExpression(Core.Enums.AvatarExpression.ProblemDetected);
        }
    }

    // ── Formatting helpers ───────────────────────────────────

    private static string FormatRate(long bytesPerSecond)
    {
        if (bytesPerSecond < 0) bytesPerSecond = 0;
        if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        if (bytesPerSecond < 1024L * 1024 * 1024) return $"{bytesPerSecond / (1024.0 * 1024.0):F1} MB/s";
        return $"{bytesPerSecond / (1024.0 * 1024.0 * 1024.0):F2} GB/s";
    }
}
