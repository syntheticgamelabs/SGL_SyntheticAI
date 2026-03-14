#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security.Monitors;

public class NetworkConnectionInfo
{
    public string LocalAddress { get; set; } = "";
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string State { get; set; } = "";
    public string Protocol { get; set; } = "TCP";
    public bool IsSuspicious { get; set; }
    public string Reason { get; set; } = "";
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public long Speed { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string IpAddress { get; set; } = "";
}

public class DnsCacheEntry
{
    public string HostName { get; set; } = "";
    public string RecordType { get; set; } = "";
    public string Data { get; set; } = "";
}

/// <summary>
/// Real network monitoring engine for the SGL SyntheticAI Security Suite.
/// Enumerates active TCP/UDP connections, listening ports, network interfaces,
/// and DNS cache entries using native Windows APIs. Detects suspicious
/// connections to known malicious ports, TOR infrastructure, and anomalous
/// outbound traffic patterns.
/// </summary>
public sealed class NetworkMonitorService : IDisposable
{
    // -------------------------------------------------------------------
    //  Suspicious-connection detection data
    // -------------------------------------------------------------------

    /// <summary>Ports commonly used by backdoors, C2 frameworks, and exploitation tools.</summary>
    private static readonly HashSet<int> MaliciousPorts = new()
    {
        4444,   // Metasploit default handler
        4445,   // Metasploit alternate
        5555,   // Common backdoor / Android debug bridge
        1234,   // Generic backdoor
        1337,   // Elite / backdoor
        31337,  // Back Orifice
        8888,   // Common C2 callback
        6666,   // Common backdoor
        6667,   // IRC (frequently used as C2 channel)
        6697,   // IRC over SSL
        12345,  // NetBus trojan
        54321,  // Back Orifice 2000
        65535,  // Common test backdoor
        2222,   // SSH alternate (often abused)
        7777,   // Common C2
        13337,  // Backdoor variant
        4443,   // Alt HTTPS C2
        3127,   // MyDoom backdoor
        3128,   // Common proxy (can be abused)
        27374,  // SubSeven trojan
        20034,  // NetBus Pro
        1170,   // Streaming Audio Trojan
        1243,   // SubSeven default
        5900,   // VNC (unauthorized remote access)
    };

    /// <summary>Ports associated with TOR infrastructure.</summary>
    private static readonly HashSet<int> TorPorts = new()
    {
        9001,   // Tor relay ORPort default
        9030,   // Tor directory authority
        9040,   // Tor TransPort
        9050,   // Tor SOCKS proxy
        9051,   // Tor control port
        9150,   // Tor Browser SOCKS proxy
        9151,   // Tor Browser control port
    };

    /// <summary>
    /// Well-known system process names that should NOT have outbound connections
    /// to non-Microsoft external IPs on unusual ports.
    /// </summary>
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "svchost",
        "lsass",
        "csrss",
        "smss",
        "wininit",
        "services",
        "winlogon",
        "dwm",
        "spoolsv",
    };

    /// <summary>
    /// Common legitimate high-traffic ports that are generally safe and should
    /// not generate false-positive suspicious flags on their own.
    /// </summary>
    private static readonly HashSet<int> SafeRemotePorts = new()
    {
        80,     // HTTP
        443,    // HTTPS
        53,     // DNS
        123,    // NTP
        993,    // IMAPS
        995,    // POP3S
        587,    // SMTP submission
        465,    // SMTPS
    };

    // -------------------------------------------------------------------
    //  Connection history tracking
    // -------------------------------------------------------------------

    /// <summary>
    /// Tracks every unique remote endpoint (IP:port) we have seen, along with
    /// the timestamp of first observation. Used to detect brand-new external
    /// connections that were not present in earlier scans.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _connectionHistory = new();

    // -------------------------------------------------------------------
    //  State
    // -------------------------------------------------------------------

    private readonly object _lock = new();
    private bool _disposed;

    // -------------------------------------------------------------------
    //  Public observable collections and summary properties
    // -------------------------------------------------------------------

    /// <summary>All active connections from the most recent scan.</summary>
    public ObservableCollection<NetworkConnectionInfo> ActiveConnections { get; } = new();

    /// <summary>All network interfaces from the most recent scan.</summary>
    public ObservableCollection<NetworkInterfaceInfo> Interfaces { get; } = new();

    /// <summary>DNS cache entries from the most recent scan.</summary>
    public ObservableCollection<DnsCacheEntry> DnsCache { get; } = new();

    /// <summary>Total number of connections (TCP established + TCP listeners + UDP listeners).</summary>
    public int TotalConnections { get; private set; }

    /// <summary>Number of TCP connections in the ESTABLISHED state.</summary>
    public int EstablishedConnections { get; private set; }

    /// <summary>Number of TCP + UDP listening endpoints.</summary>
    public int ListeningPorts { get; private set; }

    /// <summary>Aggregate bytes sent across all operational IPv4 network interfaces.</summary>
    public long BytesSent { get; private set; }

    /// <summary>Aggregate bytes received across all operational IPv4 network interfaces.</summary>
    public long BytesReceived { get; private set; }

    /// <summary>Number of connections flagged as suspicious in the most recent scan.</summary>
    public int SuspiciousConnections { get; private set; }

    /// <summary>Formatted timestamp of the last completed scan.</summary>
    public string LastRefreshTime { get; private set; } = "Never";

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>
    /// Performs a full network scan: active TCP connections, TCP/UDP listeners,
    /// network interface statistics, and DNS cache. Results are written into
    /// the observable collections and the summary properties are updated.
    /// </summary>
    public async Task RefreshAsync()
    {
        ThrowIfDisposed();

        SglLogger.Information("[NetworkMonitor] Starting full network refresh.");
        var stopwatch = Stopwatch.StartNew();

        // Run the four independent scans concurrently.
        var connectionsTask = Task.Run(ScanActiveConnections);
        var interfacesTask = Task.Run(ScanNetworkInterfaces);
        var dnsTask = Task.Run(ScanDnsCache);

        await Task.WhenAll(connectionsTask, interfacesTask, dnsTask)
                  .ConfigureAwait(false);

        List<NetworkConnectionInfo> connections = connectionsTask.Result;
        List<NetworkInterfaceInfo> interfaces = interfacesTask.Result;
        List<DnsCacheEntry> dnsEntries = dnsTask.Result;

        // Update summary counters.
        TotalConnections = connections.Count;
        EstablishedConnections = connections.Count(c =>
            c.State.Equals("Established", StringComparison.OrdinalIgnoreCase));
        ListeningPorts = connections.Count(c =>
            c.State.Equals("Listen", StringComparison.OrdinalIgnoreCase) ||
            c.State.Equals("Listening", StringComparison.OrdinalIgnoreCase));
        SuspiciousConnections = connections.Count(c => c.IsSuspicious);

        long totalSent = 0;
        long totalReceived = 0;
        foreach (var iface in interfaces)
        {
            totalSent += iface.BytesSent;
            totalReceived += iface.BytesReceived;
        }
        BytesSent = totalSent;
        BytesReceived = totalReceived;

        LastRefreshTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Populate observable collections under lock for thread safety.
        lock (_lock)
        {
            ActiveConnections.Clear();
            foreach (var conn in connections)
                ActiveConnections.Add(conn);

            Interfaces.Clear();
            foreach (var iface in interfaces)
                Interfaces.Add(iface);

            DnsCache.Clear();
            foreach (var entry in dnsEntries)
                DnsCache.Add(entry);
        }

        stopwatch.Stop();

        SglLogger.Information(
            "[NetworkMonitor] Refresh completed in {Elapsed}ms. " +
            "Total={Total}, Established={Established}, Listening={Listening}, " +
            "Suspicious={Suspicious}, Interfaces={Interfaces}, DNS={Dns}",
            stopwatch.ElapsedMilliseconds,
            TotalConnections, EstablishedConnections, ListeningPorts,
            SuspiciousConnections, interfaces.Count, dnsEntries.Count);
    }

    /// <summary>
    /// Returns all connections from the most recent scan that are flagged as
    /// suspicious.
    /// </summary>
    public List<NetworkConnectionInfo> GetSuspiciousConnections()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            return ActiveConnections.Where(c => c.IsSuspicious).ToList();
        }
    }

    // -------------------------------------------------------------------
    //  1. Active TCP Connections + TCP/UDP Listeners
    //     Uses System.Net.NetworkInformation.IPGlobalProperties
    // -------------------------------------------------------------------

    private List<NetworkConnectionInfo> ScanActiveConnections()
    {
        var results = new List<NetworkConnectionInfo>();

        try
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            // --- Active TCP connections (established, time-wait, close-wait, etc.) ---
            try
            {
                TcpConnectionInformation[] tcpConnections = ipProperties.GetActiveTcpConnections();

                foreach (TcpConnectionInformation tcp in tcpConnections)
                {
                    var conn = new NetworkConnectionInfo
                    {
                        LocalAddress = tcp.LocalEndPoint.Address.ToString(),
                        LocalPort = tcp.LocalEndPoint.Port,
                        RemoteAddress = tcp.RemoteEndPoint.Address.ToString(),
                        RemotePort = tcp.RemoteEndPoint.Port,
                        State = tcp.State.ToString(),
                        Protocol = "TCP",
                        DetectedAt = DateTime.Now,
                    };

                    EvaluateConnection(conn);
                    TrackConnection(conn);
                    results.Add(conn);

                    if (conn.IsSuspicious)
                    {
                        SglLogger.Warning(
                            "[NetworkMonitor] Suspicious TCP connection: {Local}:{LocalPort} -> " +
                            "{Remote}:{RemotePort} State={State} Reason={Reason}",
                            conn.LocalAddress, conn.LocalPort,
                            conn.RemoteAddress, conn.RemotePort,
                            conn.State, conn.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[NetworkMonitor] Error reading active TCP connections: {Message}", ex.Message);
            }

            // --- TCP listeners ---
            try
            {
                IPEndPoint[] tcpListeners = ipProperties.GetActiveTcpListeners();

                foreach (IPEndPoint listener in tcpListeners)
                {
                    var conn = new NetworkConnectionInfo
                    {
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        RemoteAddress = "*",
                        RemotePort = 0,
                        State = "Listen",
                        Protocol = "TCP",
                        DetectedAt = DateTime.Now,
                    };

                    EvaluateListeningPort(conn);
                    results.Add(conn);

                    if (conn.IsSuspicious)
                    {
                        SglLogger.Warning(
                            "[NetworkMonitor] Suspicious TCP listener: {Address}:{Port} Reason={Reason}",
                            conn.LocalAddress, conn.LocalPort, conn.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[NetworkMonitor] Error reading TCP listeners: {Message}", ex.Message);
            }

            // --- UDP listeners ---
            try
            {
                IPEndPoint[] udpListeners = ipProperties.GetActiveUdpListeners();

                foreach (IPEndPoint listener in udpListeners)
                {
                    var conn = new NetworkConnectionInfo
                    {
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        RemoteAddress = "*",
                        RemotePort = 0,
                        State = "Listening",
                        Protocol = "UDP",
                        DetectedAt = DateTime.Now,
                    };

                    EvaluateListeningPort(conn);
                    results.Add(conn);

                    if (conn.IsSuspicious)
                    {
                        SglLogger.Warning(
                            "[NetworkMonitor] Suspicious UDP listener: {Address}:{Port} Reason={Reason}",
                            conn.LocalAddress, conn.LocalPort, conn.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[NetworkMonitor] Error reading UDP listeners: {Message}", ex.Message);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[NetworkMonitor] Failed to enumerate active connections.", ex);
        }

        return results;
    }

    // -------------------------------------------------------------------
    //  2. Network Interfaces
    //     Uses System.Net.NetworkInformation.NetworkInterface
    // -------------------------------------------------------------------

    private List<NetworkInterfaceInfo> ScanNetworkInterfaces()
    {
        var results = new List<NetworkInterfaceInfo>();

        try
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nic in nics)
            {
                try
                {
                    var info = new NetworkInterfaceInfo
                    {
                        Name = nic.Name,
                        Type = nic.NetworkInterfaceType.ToString(),
                        Status = nic.OperationalStatus.ToString(),
                        Speed = nic.Speed,
                    };

                    // Attempt to read IPv4 statistics for byte counters.
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        try
                        {
                            IPv4InterfaceStatistics stats = nic.GetIPv4Statistics();
                            info.BytesSent = stats.BytesSent;
                            info.BytesReceived = stats.BytesReceived;
                        }
                        catch (Exception ex)
                        {
                            SglLogger.Debug(
                                "[NetworkMonitor] Could not read IPv4 stats for '{Nic}': {Message}",
                                nic.Name, ex.Message);
                        }
                    }

                    // Extract the first unicast IPv4 address for display.
                    try
                    {
                        IPInterfaceProperties ipProps = nic.GetIPProperties();
                        foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                info.IpAddress = addr.Address.ToString();
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SglLogger.Debug(
                            "[NetworkMonitor] Could not read IP address for '{Nic}': {Message}",
                            nic.Name, ex.Message);
                    }

                    results.Add(info);
                }
                catch (Exception ex)
                {
                    SglLogger.Debug(
                        "[NetworkMonitor] Error processing NIC '{Nic}': {Message}",
                        nic.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[NetworkMonitor] Failed to enumerate network interfaces.", ex);
        }

        return results;
    }

    // -------------------------------------------------------------------
    //  3. DNS Cache
    //     Shell out to ipconfig /displaydns and parse the output.
    // -------------------------------------------------------------------

    /// <summary>
    /// Regex that matches the record name header line in ipconfig /displaydns
    /// output, e.g.: "    Record Name . . . . . : example.com"
    /// </summary>
    private static readonly Regex DnsRecordNameRegex = new(
        @"Record Name[\s.]*:\s*(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex for the record type line, e.g.: "    Record Type . . . . . : 1"
    /// </summary>
    private static readonly Regex DnsRecordTypeRegex = new(
        @"Record Type[\s.]*:\s*(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex for the data/result line, e.g.:
    ///   "    A (Host) Record . . . : 93.184.216.34"
    ///   "    AAAA Record  . . . . : ::1"
    /// We capture everything after the last colon.
    /// </summary>
    private static readonly Regex DnsDataRegex = new(
        @"(?:A \(Host\) Record|AAAA Record|CNAME Record|Record\s*)\s*[\s.]*:\s*(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private List<DnsCacheEntry> ScanDnsCache()
    {
        var results = new List<DnsCacheEntry>();

        try
        {
            string output = RunIpconfigDisplayDns();

            if (string.IsNullOrWhiteSpace(output))
            {
                SglLogger.Debug("[NetworkMonitor] ipconfig /displaydns returned no output.");
                return results;
            }

            // The output is structured in blocks separated by dashes and blank
            // lines. Each block represents one cached record.
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string currentName = "";
            string currentType = "";
            string currentData = "";

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("---") || line.StartsWith("Windows IP"))
                    continue;

                Match nameMatch = DnsRecordNameRegex.Match(line);
                if (nameMatch.Success)
                {
                    // If we already have a pending entry, flush it.
                    if (!string.IsNullOrEmpty(currentName))
                    {
                        results.Add(new DnsCacheEntry
                        {
                            HostName = currentName.Trim(),
                            RecordType = MapDnsRecordType(currentType.Trim()),
                            Data = currentData.Trim(),
                        });
                    }

                    currentName = nameMatch.Groups[1].Value;
                    currentType = "";
                    currentData = "";
                    continue;
                }

                Match typeMatch = DnsRecordTypeRegex.Match(line);
                if (typeMatch.Success)
                {
                    currentType = typeMatch.Groups[1].Value;
                    continue;
                }

                Match dataMatch = DnsDataRegex.Match(line);
                if (dataMatch.Success)
                {
                    currentData = dataMatch.Groups[1].Value;
                }
            }

            // Flush the last pending entry.
            if (!string.IsNullOrEmpty(currentName))
            {
                results.Add(new DnsCacheEntry
                {
                    HostName = currentName.Trim(),
                    RecordType = MapDnsRecordType(currentType.Trim()),
                    Data = currentData.Trim(),
                });
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[NetworkMonitor] Failed to read DNS cache.", ex);
        }

        return results;
    }

    /// <summary>
    /// Executes <c>ipconfig /displaydns</c> and captures stdout.
    /// </summary>
    private static string RunIpconfigDisplayDns()
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/displaydns",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();

            proc.WaitForExit(15_000);

            return output;
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[NetworkMonitor] ipconfig /displaydns execution failed: {Message}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Converts the numeric DNS record type returned by ipconfig to a
    /// human-readable string.
    /// </summary>
    private static string MapDnsRecordType(string rawType)
    {
        return rawType switch
        {
            "1" => "A",
            "2" => "NS",
            "5" => "CNAME",
            "6" => "SOA",
            "12" => "PTR",
            "15" => "MX",
            "16" => "TXT",
            "28" => "AAAA",
            "33" => "SRV",
            _ => string.IsNullOrWhiteSpace(rawType) ? "Unknown" : rawType,
        };
    }

    // -------------------------------------------------------------------
    //  Connection evaluation (suspicious-connection detection)
    // -------------------------------------------------------------------

    /// <summary>
    /// Evaluates an active (non-listening) connection and sets
    /// <see cref="NetworkConnectionInfo.IsSuspicious"/> and
    /// <see cref="NetworkConnectionInfo.Reason"/> if warranted.
    /// </summary>
    private void EvaluateConnection(NetworkConnectionInfo conn)
    {
        // Skip evaluation for loopback / local-only traffic.
        if (IsLoopbackOrLocal(conn.RemoteAddress))
            return;

        var reasons = new List<string>();

        // --- 1. Known malicious port on the remote side ---
        if (MaliciousPorts.Contains(conn.RemotePort))
        {
            reasons.Add($"Remote port {conn.RemotePort} is associated with known malware/C2 frameworks");
        }

        // --- 2. Known malicious port on the local side (reverse shell listener) ---
        if (MaliciousPorts.Contains(conn.LocalPort) &&
            conn.State.Equals("Established", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"Local port {conn.LocalPort} is associated with backdoor/C2 listener");
        }

        // --- 3. TOR infrastructure ports ---
        if (TorPorts.Contains(conn.RemotePort))
        {
            reasons.Add($"Remote port {conn.RemotePort} is associated with TOR anonymization network");
        }
        if (TorPorts.Contains(conn.LocalPort))
        {
            reasons.Add($"Local port {conn.LocalPort} is associated with TOR anonymization network");
        }

        // --- 4. Connections on very high ephemeral ports to non-standard remote ports ---
        //     (Excludes safe ports like 80/443/53 which are legitimate)
        if (conn.RemotePort > 0 &&
            !SafeRemotePorts.Contains(conn.RemotePort) &&
            !MaliciousPorts.Contains(conn.RemotePort) &&
            !TorPorts.Contains(conn.RemotePort) &&
            conn.RemotePort >= 10000 &&
            conn.State.Equals("Established", StringComparison.OrdinalIgnoreCase))
        {
            // High remote ports on established connections to external IPs can
            // indicate C2 beacons that use randomized ports.
            reasons.Add($"Established connection to unusual high remote port {conn.RemotePort}");
        }

        // --- 5. Brand-new external connection not seen before ---
        string remoteKey = $"{conn.RemoteAddress}:{conn.RemotePort}";
        if (!_connectionHistory.ContainsKey(remoteKey) &&
            conn.State.Equals("Established", StringComparison.OrdinalIgnoreCase) &&
            !SafeRemotePorts.Contains(conn.RemotePort))
        {
            // Not flagged as suspicious on its own, but logged for analysts.
            // We only flag it if it also matches another heuristic above.
            SglLogger.Debug(
                "[NetworkMonitor] New external connection observed: {Remote}",
                remoteKey);
        }

        if (reasons.Count > 0)
        {
            conn.IsSuspicious = true;
            conn.Reason = string.Join("; ", reasons);
        }
    }

    /// <summary>
    /// Evaluates a listening port and flags it if it matches known malicious
    /// or TOR-related port numbers.
    /// </summary>
    private static void EvaluateListeningPort(NetworkConnectionInfo conn)
    {
        var reasons = new List<string>();

        if (MaliciousPorts.Contains(conn.LocalPort))
        {
            reasons.Add($"Listening on port {conn.LocalPort} which is associated with known malware/C2");
        }

        if (TorPorts.Contains(conn.LocalPort))
        {
            reasons.Add($"Listening on port {conn.LocalPort} which is associated with TOR network");
        }

        if (reasons.Count > 0)
        {
            conn.IsSuspicious = true;
            conn.Reason = string.Join("; ", reasons);
        }
    }

    /// <summary>
    /// Records a connection in the history dictionary so that future scans can
    /// detect brand-new external connections.
    /// </summary>
    private void TrackConnection(NetworkConnectionInfo conn)
    {
        if (IsLoopbackOrLocal(conn.RemoteAddress))
            return;

        string key = $"{conn.RemoteAddress}:{conn.RemotePort}";
        _connectionHistory.TryAdd(key, DateTime.Now);
    }

    // -------------------------------------------------------------------
    //  Address classification helpers
    // -------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if the given IP address is a loopback, link-local,
    /// or RFC-1918 private address that should not be considered suspicious.
    /// </summary>
    private static bool IsLoopbackOrLocal(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "*")
            return true;

        // IPv6 loopback
        if (ip == "::1" || ip == "::")
            return true;

        // IPv4 loopback
        if (ip.StartsWith("127."))
            return true;

        // IPv4 unspecified
        if (ip == "0.0.0.0")
            return true;

        // RFC-1918 private ranges
        if (ip.StartsWith("10."))
            return true;
        if (ip.StartsWith("192.168."))
            return true;

        // 172.16.0.0/12
        if (ip.StartsWith("172."))
        {
            string[] octets = ip.Split('.');
            if (octets.Length >= 2 && int.TryParse(octets[1], out int second))
            {
                if (second >= 16 && second <= 31)
                    return true;
            }
        }

        // Link-local
        if (ip.StartsWith("169.254."))
            return true;

        // IPv6 link-local
        if (ip.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase))
            return true;

        // IPv6 unique local
        if (ip.StartsWith("fc", StringComparison.OrdinalIgnoreCase) ||
            ip.StartsWith("fd", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // -------------------------------------------------------------------
    //  IDisposable
    // -------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            ActiveConnections.Clear();
            Interfaces.Clear();
            DnsCache.Clear();
        }

        _connectionHistory.Clear();

        SglLogger.Information("[NetworkMonitor] Disposed.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NetworkMonitorService));
    }
}
