#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security.Reports;

public class IncidentReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int ThreatCount { get; set; }
    public int FindingsCount { get; set; }
    public string Severity { get; set; } = "Low"; // Low, Medium, High, Critical
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Incident report generation service for the SGL SyntheticAI Security Suite.
/// Collects real system information (OS version, CPU, memory, uptime, network
/// interfaces), combines it with scan results and detected threats, and produces
/// structured plain-text reports saved to the <c>data/reports/</c> directory.
/// </summary>
public class IncidentReportService
{
    // -------------------------------------------------------------------
    //  State
    // -------------------------------------------------------------------

    private readonly string _reportsDirectory;

    /// <summary>All reports generated during this session (and previously saved ones if loaded).</summary>
    public ObservableCollection<IncidentReport> Reports { get; } = new();

    /// <summary>Total number of reports tracked.</summary>
    public int TotalReports { get; private set; }

    /// <summary>Formatted timestamp of the last report generation.</summary>
    public string LastReportTime { get; private set; } = "Never";

    // -------------------------------------------------------------------
    //  Constructor
    // -------------------------------------------------------------------

    public IncidentReportService()
    {
        _reportsDirectory = Path.Combine(AppContext.BaseDirectory, "data", "reports");

        try
        {
            Directory.CreateDirectory(_reportsDirectory);
        }
        catch (Exception ex)
        {
            SglLogger.Error("[IncidentReport] Could not create reports directory.", ex);
        }
    }

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>
    /// Generates an incident report from the supplied threat, network, and
    /// persistence findings. The report includes an executive summary, system
    /// information, and recommendations.
    /// </summary>
    public async Task<IncidentReport> GenerateReportAsync(
        string title,
        List<string> threats,
        List<string> networkIssues,
        List<string> persistenceFindings)
    {
        SglLogger.Information("[IncidentReport] Generating report: {Title}", title);

        var report = new IncidentReport
        {
            Title = title,
            ThreatCount = threats?.Count ?? 0,
            FindingsCount = (threats?.Count ?? 0) +
                            (networkIssues?.Count ?? 0) +
                            (persistenceFindings?.Count ?? 0),
            GeneratedAt = DateTime.Now,
        };

        // Determine severity based on findings.
        report.Severity = DetermineSeverity(
            threats?.Count ?? 0,
            networkIssues?.Count ?? 0,
            persistenceFindings?.Count ?? 0);

        // Collect system info asynchronously.
        SystemInfoSnapshot sysInfo = await Task.Run(CollectSystemInfo).ConfigureAwait(false);

        // Build the report content.
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(78, '='));
        sb.AppendLine("  SGL SYNTHETICAI SECURITY SUITE - INCIDENT REPORT");
        sb.AppendLine("=".PadRight(78, '='));
        sb.AppendLine();
        sb.AppendLine($"  Report ID      : {report.Id}");
        sb.AppendLine($"  Title          : {report.Title}");
        sb.AppendLine($"  Generated At   : {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Severity       : {report.Severity}");
        sb.AppendLine($"  Threats Found  : {report.ThreatCount}");
        sb.AppendLine($"  Total Findings : {report.FindingsCount}");
        sb.AppendLine();

        // Executive Summary
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 1: EXECUTIVE SUMMARY");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();
        sb.AppendLine(BuildExecutiveSummary(report, threats, networkIssues, persistenceFindings));
        sb.AppendLine();

        // System Information
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 2: SYSTEM INFORMATION");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();
        sb.AppendLine($"  Machine Name   : {sysInfo.MachineName}");
        sb.AppendLine($"  OS             : {sysInfo.OsVersion}");
        sb.AppendLine($"  OS Build       : {sysInfo.OsBuild}");
        sb.AppendLine($"  Processor      : {sysInfo.Processor}");
        sb.AppendLine($"  CPU Cores      : {sysInfo.CpuCores}");
        sb.AppendLine($"  Total Memory   : {sysInfo.TotalMemoryGb:F1} GB");
        sb.AppendLine($"  Free Memory    : {sysInfo.FreeMemoryGb:F1} GB");
        sb.AppendLine($"  Memory Usage   : {sysInfo.MemoryUsagePercent:F1}%");
        sb.AppendLine($"  System Uptime  : {sysInfo.Uptime}");
        sb.AppendLine($"  User           : {sysInfo.CurrentUser}");
        sb.AppendLine($"  Domain         : {sysInfo.DomainName}");
        sb.AppendLine();

        // Network Interfaces
        if (sysInfo.NetworkInterfaces.Count > 0)
        {
            sb.AppendLine("  Active Network Interfaces:");
            foreach (var nic in sysInfo.NetworkInterfaces)
            {
                sb.AppendLine($"    - {nic.Name}: {nic.IpAddress} ({nic.Type}, {nic.Status})");
            }
            sb.AppendLine();
        }

        // Disk info
        if (sysInfo.Drives.Count > 0)
        {
            sb.AppendLine("  Disk Drives:");
            foreach (var drive in sysInfo.Drives)
            {
                sb.AppendLine($"    - {drive.Name}: {drive.FreeGb:F1} GB free / {drive.TotalGb:F1} GB total " +
                              $"({drive.UsagePercent:F0}% used)");
            }
            sb.AppendLine();
        }

        // Threats Detected
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 3: THREATS DETECTED");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();

        if (threats != null && threats.Count > 0)
        {
            for (int i = 0; i < threats.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {threats[i]}");
            }
        }
        else
        {
            sb.AppendLine("  No threats were detected during this scan cycle.");
        }
        sb.AppendLine();

        // Network Analysis
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 4: NETWORK ANALYSIS");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();

        if (networkIssues != null && networkIssues.Count > 0)
        {
            for (int i = 0; i < networkIssues.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {networkIssues[i]}");
            }
        }
        else
        {
            sb.AppendLine("  No network anomalies were detected.");
        }
        sb.AppendLine();

        // Persistence Findings
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 5: PERSISTENCE FINDINGS");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();

        if (persistenceFindings != null && persistenceFindings.Count > 0)
        {
            for (int i = 0; i < persistenceFindings.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {persistenceFindings[i]}");
            }
        }
        else
        {
            sb.AppendLine("  No suspicious persistence mechanisms were detected.");
        }
        sb.AppendLine();

        // Recommendations
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine("  SECTION 6: RECOMMENDATIONS");
        sb.AppendLine("-".PadRight(78, '-'));
        sb.AppendLine();
        sb.AppendLine(BuildRecommendations(threats, networkIssues, persistenceFindings));
        sb.AppendLine();

        // Footer
        sb.AppendLine("=".PadRight(78, '='));
        sb.AppendLine("  END OF REPORT");
        sb.AppendLine($"  Generated by SGL SyntheticAI Security Suite");
        sb.AppendLine($"  Report Time: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine("=".PadRight(78, '='));

        report.Content = sb.ToString();

        // Save to disk.
        string fileName = $"incident_{report.Id}_{report.GeneratedAt:yyyyMMdd_HHmmss}.txt";
        string filePath = Path.Combine(_reportsDirectory, fileName);

        try
        {
            await File.WriteAllTextAsync(filePath, report.Content, Encoding.UTF8).ConfigureAwait(false);
            report.FilePath = filePath;
            SglLogger.Information("[IncidentReport] Report saved to {Path}", filePath);
        }
        catch (Exception ex)
        {
            SglLogger.Error("[IncidentReport] Failed to save report to disk.", ex);
            report.FilePath = "(save failed)";
        }

        // Track in observable collection.
        Reports.Add(report);
        TotalReports = Reports.Count;
        LastReportTime = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss");

        return report;
    }

    /// <summary>
    /// Generates a comprehensive system report using live data from the local
    /// machine. No external threat/network/persistence data is required; this
    /// method gathers everything autonomously.
    /// </summary>
    public async Task<IncidentReport> GenerateFullSystemReportAsync()
    {
        SglLogger.Information("[IncidentReport] Generating full system report.");

        // Collect live data.
        var threats = new List<string>();
        var networkIssues = new List<string>();
        var persistenceFindings = new List<string>();

        await Task.Run(() =>
        {
            // ------ Running processes check ------
            try
            {
                Process[] processes = Process.GetProcesses();
                var suspiciousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "mimikatz", "cobaltstrike", "meterpreter", "rubeus", "psexec",
                    "lazagne", "sharphound", "bloodhound", "hashcat", "hydra",
                    "ncrack", "responder", "sliver", "chisel", "netcat", "nc",
                };

                foreach (Process proc in processes)
                {
                    try
                    {
                        if (suspiciousNames.Contains(proc.ProcessName))
                        {
                            string? path = null;
                            try { path = proc.MainModule?.FileName; } catch { }
                            threats.Add($"Suspicious process running: {proc.ProcessName} " +
                                        $"(PID {proc.Id}) Path: {path ?? "(unknown)"}");
                        }
                    }
                    catch { }
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[IncidentReport] Process scan error: {Message}", ex.Message);
            }

            // ------ Network connections check ------
            try
            {
                var ipProps = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConns = ipProps.GetActiveTcpConnections();
                var suspiciousPorts = new HashSet<int>
                {
                    4444, 4445, 5555, 1234, 1337, 31337, 6666, 6667,
                    12345, 54321, 9001, 9050, 9051
                };

                foreach (var conn in tcpConns)
                {
                    if (suspiciousPorts.Contains(conn.RemoteEndPoint.Port) &&
                        conn.State == TcpState.Established)
                    {
                        networkIssues.Add($"Suspicious outbound connection to " +
                                          $"{conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port} " +
                                          $"(State: {conn.State})");
                    }
                }

                // Check for excessive connections
                if (tcpConns.Length > 500)
                {
                    networkIssues.Add($"High number of active TCP connections: {tcpConns.Length}");
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[IncidentReport] Network scan error: {Message}", ex.Message);
            }

            // ------ Registry persistence check (lightweight) ------
            try
            {
                string[] regPaths =
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                };

                foreach (string path in regPaths)
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path, false);
                        if (key == null) continue;

                        foreach (string name in key.GetValueNames())
                        {
                            string? val = key.GetValue(name)?.ToString();
                            if (val == null) continue;

                            string lower = val.ToLowerInvariant();
                            if (lower.Contains(@"\temp\") || lower.Contains(@"\tmp\") ||
                                lower.Contains(@"\downloads\") ||
                                (lower.Contains("powershell") &&
                                 (lower.Contains("-enc") || lower.Contains("bypass"))))
                            {
                                persistenceFindings.Add($"Suspicious registry run entry: " +
                                                       $"HKCU\\{path}\\{name} = {Truncate(val, 150)}");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[IncidentReport] Registry scan error: {Message}", ex.Message);
            }

            // ------ Startup folder check ------
            try
            {
                string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(startupDir))
                {
                    var suspiciousExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".vbs", ".bat", ".cmd", ".js", ".wsf", ".hta", ".exe", ".scr"
                    };

                    foreach (string file in Directory.EnumerateFiles(startupDir))
                    {
                        string ext = Path.GetExtension(file);
                        if (suspiciousExts.Contains(ext))
                        {
                            persistenceFindings.Add($"Executable/script in startup folder: {file}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[IncidentReport] Startup folder scan error: {Message}", ex.Message);
            }

        }).ConfigureAwait(false);

        // If nothing was found, note that in the report.
        if (threats.Count == 0 && networkIssues.Count == 0 && persistenceFindings.Count == 0)
        {
            threats.Add("No active threats detected during this automated scan.");
        }

        return await GenerateReportAsync(
            "Full System Security Assessment",
            threats,
            networkIssues,
            persistenceFindings).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads previously saved reports from the <c>data/reports/</c> directory
    /// and returns them. Also adds them to the <see cref="Reports"/> collection
    /// if they are not already present.
    /// </summary>
    public List<IncidentReport> LoadSavedReports()
    {
        var loaded = new List<IncidentReport>();

        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return loaded;

            // Track existing IDs to avoid duplicates.
            var existingIds = new HashSet<string>(Reports.Select(r => r.Id));

            foreach (string filePath in Directory.EnumerateFiles(_reportsDirectory, "incident_*.txt")
                         .OrderByDescending(f => File.GetCreationTime(f)))
            {
                try
                {
                    string content = File.ReadAllText(filePath, Encoding.UTF8);
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Parse the ID from the filename: incident_{id}_{timestamp}
                    string[] parts = fileName.Split('_', 3);
                    string reportId = parts.Length >= 2 ? parts[1] : Guid.NewGuid().ToString("N")[..8];

                    if (existingIds.Contains(reportId))
                        continue;

                    // Parse title from content
                    string title = "Loaded Report";
                    int titleIdx = content.IndexOf("Title", StringComparison.OrdinalIgnoreCase);
                    if (titleIdx >= 0)
                    {
                        int colonIdx = content.IndexOf(':', titleIdx);
                        int newlineIdx = content.IndexOf('\n', colonIdx > 0 ? colonIdx : titleIdx);
                        if (colonIdx > 0 && newlineIdx > colonIdx)
                        {
                            title = content[(colonIdx + 1)..newlineIdx].Trim();
                        }
                    }

                    // Parse severity from content
                    string severity = "Low";
                    int sevIdx = content.IndexOf("Severity", StringComparison.OrdinalIgnoreCase);
                    if (sevIdx >= 0)
                    {
                        int colonIdx = content.IndexOf(':', sevIdx);
                        int newlineIdx = content.IndexOf('\n', colonIdx > 0 ? colonIdx : sevIdx);
                        if (colonIdx > 0 && newlineIdx > colonIdx)
                        {
                            severity = content[(colonIdx + 1)..newlineIdx].Trim();
                        }
                    }

                    // Parse threat count
                    int threatCount = 0;
                    int threatIdx = content.IndexOf("Threats Found", StringComparison.OrdinalIgnoreCase);
                    if (threatIdx >= 0)
                    {
                        int colonIdx = content.IndexOf(':', threatIdx);
                        int newlineIdx = content.IndexOf('\n', colonIdx > 0 ? colonIdx : threatIdx);
                        if (colonIdx > 0 && newlineIdx > colonIdx)
                        {
                            int.TryParse(content[(colonIdx + 1)..newlineIdx].Trim(), out threatCount);
                        }
                    }

                    var report = new IncidentReport
                    {
                        Id = reportId,
                        Title = title,
                        Content = content,
                        FilePath = filePath,
                        ThreatCount = threatCount,
                        Severity = severity,
                        GeneratedAt = File.GetCreationTime(filePath),
                    };

                    loaded.Add(report);
                    Reports.Add(report);
                    existingIds.Add(reportId);
                }
                catch (Exception ex)
                {
                    SglLogger.Debug("[IncidentReport] Error loading report {File}: {Message}",
                        filePath, ex.Message);
                }
            }

            TotalReports = Reports.Count;

            SglLogger.Information("[IncidentReport] Loaded {Count} saved report(s) from disk.", loaded.Count);
        }
        catch (Exception ex)
        {
            SglLogger.Error("[IncidentReport] Error loading saved reports.", ex);
        }

        return loaded;
    }

    // -------------------------------------------------------------------
    //  System information collection
    // -------------------------------------------------------------------

    private class SystemInfoSnapshot
    {
        public string MachineName { get; set; } = "";
        public string OsVersion { get; set; } = "";
        public string OsBuild { get; set; } = "";
        public string Processor { get; set; } = "";
        public int CpuCores { get; set; }
        public double TotalMemoryGb { get; set; }
        public double FreeMemoryGb { get; set; }
        public double MemoryUsagePercent { get; set; }
        public string Uptime { get; set; } = "";
        public string CurrentUser { get; set; } = "";
        public string DomainName { get; set; } = "";
        public List<NicInfo> NetworkInterfaces { get; set; } = new();
        public List<DriveSnapshot> Drives { get; set; } = new();
    }

    private class NicInfo
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private class DriveSnapshot
    {
        public string Name { get; set; } = "";
        public double TotalGb { get; set; }
        public double FreeGb { get; set; }
        public double UsagePercent { get; set; }
    }

    private static SystemInfoSnapshot CollectSystemInfo()
    {
        var info = new SystemInfoSnapshot
        {
            MachineName = Environment.MachineName,
            CurrentUser = Environment.UserName,
            DomainName = Environment.UserDomainName,
            CpuCores = Environment.ProcessorCount,
        };

        // OS version
        try
        {
            info.OsVersion = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
            info.OsBuild = Environment.OSVersion.VersionString;
        }
        catch
        {
            info.OsVersion = "Unknown";
            info.OsBuild = "Unknown";
        }

        // Processor name from WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject obj in results)
            {
                info.Processor = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                break;
            }
        }
        catch
        {
            info.Processor = $"{Environment.ProcessorCount}-core processor";
        }

        // Memory from WMI
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject obj in results)
            {
                double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"]);
                info.TotalMemoryGb = totalKb / (1024.0 * 1024.0);
                info.FreeMemoryGb = freeKb / (1024.0 * 1024.0);
                if (totalKb > 0)
                    info.MemoryUsagePercent = ((totalKb - freeKb) / totalKb) * 100.0;
                break;
            }
        }
        catch
        {
            info.TotalMemoryGb = 0;
            info.FreeMemoryGb = 0;
        }

        // Uptime
        try
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            info.Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        catch
        {
            info.Uptime = "Unknown";
        }

        // Network interfaces
        try
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                string ipAddr = "";
                try
                {
                    IPInterfaceProperties ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipAddr = addr.Address.ToString();
                            break;
                        }
                    }
                }
                catch { }

                info.NetworkInterfaces.Add(new NicInfo
                {
                    Name = nic.Name,
                    IpAddress = ipAddr,
                    Type = nic.NetworkInterfaceType.ToString(),
                    Status = nic.OperationalStatus.ToString(),
                });
            }
        }
        catch { }

        // Disk drives
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType == DriveType.CDRom)
                    continue;

                double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double used = totalGb > 0 ? ((totalGb - freeGb) / totalGb) * 100.0 : 0;

                info.Drives.Add(new DriveSnapshot
                {
                    Name = $"{drive.Name} ({drive.VolumeLabel})",
                    TotalGb = totalGb,
                    FreeGb = freeGb,
                    UsagePercent = used,
                });
            }
        }
        catch { }

        return info;
    }

    // -------------------------------------------------------------------
    //  Report content builders
    // -------------------------------------------------------------------

    private static string BuildExecutiveSummary(
        IncidentReport report,
        List<string>? threats,
        List<string>? networkIssues,
        List<string>? persistenceFindings)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"  This incident report was generated on {report.GeneratedAt:MMMM dd, yyyy at HH:mm:ss}");
        sb.AppendLine($"  by the SGL SyntheticAI Security Suite.");
        sb.AppendLine();

        int totalIssues = (threats?.Count ?? 0) + (networkIssues?.Count ?? 0) +
                          (persistenceFindings?.Count ?? 0);

        if (totalIssues == 0)
        {
            sb.AppendLine("  ASSESSMENT: The system appears to be in a clean state. No threats,");
            sb.AppendLine("  suspicious network activity, or unauthorized persistence mechanisms");
            sb.AppendLine("  were detected during this scan.");
        }
        else
        {
            sb.AppendLine($"  ASSESSMENT: A total of {totalIssues} finding(s) were identified:");
            if (threats != null && threats.Count > 0)
                sb.AppendLine($"    - {threats.Count} threat(s) / malicious indicator(s)");
            if (networkIssues != null && networkIssues.Count > 0)
                sb.AppendLine($"    - {networkIssues.Count} network anomaly/anomalies");
            if (persistenceFindings != null && persistenceFindings.Count > 0)
                sb.AppendLine($"    - {persistenceFindings.Count} persistence mechanism(s)");
            sb.AppendLine();
            sb.AppendLine($"  Overall severity has been assessed as: {report.Severity}");
            sb.AppendLine("  Immediate review and remediation is recommended for all High and");
            sb.AppendLine("  Critical severity findings.");
        }

        return sb.ToString();
    }

    private static string BuildRecommendations(
        List<string>? threats,
        List<string>? networkIssues,
        List<string>? persistenceFindings)
    {
        var sb = new StringBuilder();
        int recNum = 1;

        bool hasThreats = threats != null && threats.Count > 0 &&
                          !threats.Any(t => t.Contains("No active threats"));
        bool hasNetwork = networkIssues != null && networkIssues.Count > 0;
        bool hasPersistence = persistenceFindings != null && persistenceFindings.Count > 0;

        if (hasThreats)
        {
            sb.AppendLine($"  {recNum}. THREAT REMEDIATION: Isolate the affected system from the network");
            sb.AppendLine("     and terminate any identified malicious processes. Run a full antivirus");
            sb.AppendLine("     scan and consider reimaging if compromise is confirmed.");
            recNum++;
            sb.AppendLine();
        }

        if (hasNetwork)
        {
            sb.AppendLine($"  {recNum}. NETWORK HARDENING: Review and block suspicious outbound connections");
            sb.AppendLine("     at the firewall level. Investigate connections to known C2 ports and");
            sb.AppendLine("     TOR infrastructure. Enable network segmentation where possible.");
            recNum++;
            sb.AppendLine();
        }

        if (hasPersistence)
        {
            sb.AppendLine($"  {recNum}. PERSISTENCE CLEANUP: Remove unauthorized startup entries from the");
            sb.AppendLine("     Windows Registry and startup folders. Review scheduled tasks and");
            sb.AppendLine("     Windows services for illegitimate entries. Check for WMI persistence.");
            recNum++;
            sb.AppendLine();
        }

        // Always-applicable recommendations
        sb.AppendLine($"  {recNum}. Ensure all operating system and application patches are up to date.");
        recNum++;
        sb.AppendLine($"  {recNum}. Verify that Windows Defender or the primary AV solution is active");
        sb.AppendLine("     and definitions are current.");
        recNum++;
        sb.AppendLine($"  {recNum}. Review user accounts and disable any unauthorized or dormant accounts.");
        recNum++;
        sb.AppendLine($"  {recNum}. Enable audit logging for process creation, logon events, and");
        sb.AppendLine("     PowerShell script block logging via Group Policy.");
        recNum++;
        sb.AppendLine($"  {recNum}. Schedule regular security scans using the SGL SyntheticAI");
        sb.AppendLine("     Security Suite to maintain continuous monitoring posture.");

        return sb.ToString();
    }

    private static string DetermineSeverity(int threatCount, int networkCount, int persistenceCount)
    {
        int total = threatCount + networkCount + persistenceCount;

        if (threatCount >= 3 || total >= 10)
            return "Critical";
        if (threatCount >= 1 || total >= 5)
            return "High";
        if (networkCount >= 2 || persistenceCount >= 2 || total >= 2)
            return "Medium";
        if (total >= 1)
            return "Low";

        return "Low";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
