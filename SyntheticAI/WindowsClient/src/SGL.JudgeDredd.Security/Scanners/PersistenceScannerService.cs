#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security.Scanners;

public class PersistenceEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // Registry, StartupFolder, ScheduledTask, Service, WMI, BrowserExtension
    public string Location { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsSuspicious { get; set; }
    public string Reason { get; set; } = "";
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Scans Windows persistence mechanisms to detect malware that has established
/// auto-start footholds. Inspects registry Run keys, startup folders, scheduled
/// tasks, Windows services, WMI event subscriptions, and browser extension
/// directories. Each discovered entry is evaluated against heuristics and
/// flagged as suspicious when appropriate.
/// </summary>
public sealed class PersistenceScannerService
{
    // -------------------------------------------------------------------
    //  Heuristic data
    // -------------------------------------------------------------------

    /// <summary>Path fragments that are unusual for legitimate auto-start entries.</summary>
    private static readonly string[] SuspiciousPathFragments =
    {
        @"\temp\",
        @"\tmp\",
        @"\appdata\local\temp\",
        @"\downloads\",
        @"\public\",
        @"$recycle.bin",
        @"\users\public\",
        @"\programdata\",
    };

    /// <summary>Executable names commonly associated with malware persistence.</summary>
    private static readonly HashSet<string> SuspiciousExecutableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mimikatz.exe", "cobaltstrike.exe", "meterpreter.exe", "rubeus.exe",
        "psexec.exe", "lazagne.exe", "sharphound.exe", "bloodhound.exe",
        "hashcat.exe", "hydra.exe", "ncrack.exe", "responder.exe",
        "sliver.exe", "chisel.exe", "ligolo.exe", "netcat.exe", "nc.exe",
        "certutil.exe", "bitsadmin.exe", "mshta.exe", "regsvr32.exe",
        "wscript.exe", "cscript.exe",
    };

    /// <summary>Registry sub-key paths under HKLM and HKCU to scan for auto-start entries.</summary>
    private static readonly string[] RegistryRunKeyPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServices",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServicesOnce",
        @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
    };

    /// <summary>Extensions that constitute executable content in the startup folder.</summary>
    private static readonly HashSet<string> StartupExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse", ".wsf",
        ".wsh", ".exe", ".com", ".scr", ".pif",
    };

    /// <summary>Task paths in Task Scheduler that are typically legitimate Microsoft tasks.</summary>
    private static readonly HashSet<string> SafeTaskPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        @"\Microsoft\",
        @"\Apple\",
        @"\GoogleSystem\",
        @"\Google\",
    };

    /// <summary>Known legitimate Windows service executable paths (lowercase).</summary>
    private static readonly HashSet<string> TrustedServicePathRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        @"c:\windows\system32\",
        @"c:\windows\syswow64\",
        @"c:\program files\",
        @"c:\program files (x86)\",
    };

    // -------------------------------------------------------------------
    //  Observable state
    // -------------------------------------------------------------------

    /// <summary>All persistence entries discovered during the most recent scan.</summary>
    public ObservableCollection<PersistenceEntry> Entries { get; } = new();

    /// <summary>Total entries found in the last scan.</summary>
    public int TotalEntries { get; private set; }

    /// <summary>Number of entries flagged as suspicious in the last scan.</summary>
    public int SuspiciousEntries { get; private set; }

    /// <summary>Formatted timestamp of the last completed scan.</summary>
    public string LastScanTime { get; private set; } = "Never";

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>
    /// Performs a comprehensive asynchronous scan of all persistence mechanisms
    /// and populates <see cref="Entries"/>.
    /// </summary>
    public async Task ScanAsync()
    {
        SglLogger.Information("[PersistenceScanner] Starting comprehensive persistence scan.");
        var stopwatch = Stopwatch.StartNew();

        var allEntries = new List<PersistenceEntry>();

        // Run independent scanners concurrently.
        var registryTask = Task.Run(ScanRegistryRunKeys);
        var startupTask = Task.Run(ScanStartupFolders);
        var scheduledTask = Task.Run(ScanScheduledTasks);
        var serviceTask = Task.Run(ScanServices);
        var wmiTask = Task.Run(ScanWmiSubscriptions);
        var browserTask = Task.Run(ScanBrowserExtensions);

        await Task.WhenAll(registryTask, startupTask, scheduledTask, serviceTask, wmiTask, browserTask)
                  .ConfigureAwait(false);

        allEntries.AddRange(registryTask.Result);
        allEntries.AddRange(startupTask.Result);
        allEntries.AddRange(scheduledTask.Result);
        allEntries.AddRange(serviceTask.Result);
        allEntries.AddRange(wmiTask.Result);
        allEntries.AddRange(browserTask.Result);

        // Update observable state.
        TotalEntries = allEntries.Count;
        SuspiciousEntries = allEntries.Count(e => e.IsSuspicious);
        LastScanTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        Entries.Clear();
        foreach (var entry in allEntries)
            Entries.Add(entry);

        stopwatch.Stop();
        SglLogger.Information(
            "[PersistenceScanner] Scan completed in {Elapsed}ms. Total={Total}, Suspicious={Suspicious}",
            stopwatch.ElapsedMilliseconds, TotalEntries, SuspiciousEntries);
    }

    /// <summary>Returns only entries flagged as suspicious.</summary>
    public List<PersistenceEntry> GetSuspiciousEntries()
    {
        return Entries.Where(e => e.IsSuspicious).ToList();
    }

    // -------------------------------------------------------------------
    //  1. Registry Run Keys
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanRegistryRunKeys()
    {
        var entries = new List<PersistenceEntry>();

        foreach (string subKeyPath in RegistryRunKeyPaths)
        {
            ScanRegistryHive(Registry.LocalMachine, "HKLM", subKeyPath, entries);
            ScanRegistryHive(Registry.CurrentUser, "HKCU", subKeyPath, entries);
        }

        SglLogger.Information("[PersistenceScanner] Registry scan found {Count} entries.", entries.Count);
        return entries;
    }

    private void ScanRegistryHive(RegistryKey hive, string hiveName, string subKeyPath,
        List<PersistenceEntry> entries)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKeyPath, writable: false);
            if (key == null)
                return;

            string fullPath = $"{hiveName}\\{subKeyPath}";
            string[] valueNames = key.GetValueNames();

            foreach (string valueName in valueNames)
            {
                if (string.IsNullOrEmpty(valueName))
                    continue;

                string? rawValue = key.GetValue(valueName)?.ToString() ?? "";

                var entry = new PersistenceEntry
                {
                    Name = valueName,
                    Type = "Registry",
                    Location = fullPath,
                    Value = rawValue,
                    DetectedAt = DateTime.Now,
                };

                EvaluateRegistryEntry(entry, rawValue);
                entries.Add(entry);
            }
        }
        catch (System.Security.SecurityException)
        {
            SglLogger.Debug("[PersistenceScanner] Access denied to {Hive}\\{Path}", hiveName, subKeyPath);
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[PersistenceScanner] Error reading {Hive}\\{Path}: {Message}",
                hiveName, subKeyPath, ex.Message);
        }
    }

    private static void EvaluateRegistryEntry(PersistenceEntry entry, string rawValue)
    {
        string lower = rawValue.ToLowerInvariant();
        var reasons = new List<string>();

        // Check path fragments
        foreach (string fragment in SuspiciousPathFragments)
        {
            if (lower.Contains(fragment.ToLowerInvariant()))
            {
                reasons.Add($"Executable path contains suspicious fragment '{fragment}'");
                break;
            }
        }

        // Check for suspicious executables
        foreach (string exeName in SuspiciousExecutableNames)
        {
            if (lower.Contains(exeName.ToLowerInvariant()))
            {
                reasons.Add($"References known suspicious executable '{exeName}'");
                break;
            }
        }

        // Encoded / obfuscated command lines
        if (lower.Contains("powershell") && (lower.Contains("-enc") || lower.Contains("-e ") ||
            lower.Contains("encodedcommand") || lower.Contains("bypass") ||
            lower.Contains("hidden") || lower.Contains("-nop")))
        {
            reasons.Add("PowerShell invocation with encoding/bypass flags detected");
        }

        if (lower.Contains("cmd") && lower.Contains("/c") && lower.Contains("start"))
        {
            reasons.Add("cmd.exe chain-launch pattern detected");
        }

        if (lower.Contains("mshta") || lower.Contains("regsvr32") || lower.Contains("rundll32"))
        {
            if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains("javascript:"))
            {
                reasons.Add("LOLBin with remote payload reference detected");
            }
        }

        if (reasons.Count > 0)
        {
            entry.IsSuspicious = true;
            entry.Reason = string.Join("; ", reasons);
        }
    }

    // -------------------------------------------------------------------
    //  2. Startup Folders
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanStartupFolders()
    {
        var entries = new List<PersistenceEntry>();

        // Current user startup
        string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!string.IsNullOrEmpty(userStartup) && Directory.Exists(userStartup))
        {
            ScanStartupDirectory(userStartup, "StartupFolder (User)", entries);
        }

        // All-users startup (CommonStartup)
        string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        if (!string.IsNullOrEmpty(commonStartup) && Directory.Exists(commonStartup))
        {
            ScanStartupDirectory(commonStartup, "StartupFolder (AllUsers)", entries);
        }

        // Fallback: ProgramData startup path
        string programDataStartup = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        if (Directory.Exists(programDataStartup) &&
            !string.Equals(programDataStartup, commonStartup, StringComparison.OrdinalIgnoreCase))
        {
            ScanStartupDirectory(programDataStartup, "StartupFolder (ProgramData)", entries);
        }

        SglLogger.Information("[PersistenceScanner] Startup folder scan found {Count} entries.", entries.Count);
        return entries;
    }

    private void ScanStartupDirectory(string directoryPath, string locationLabel,
        List<PersistenceEntry> entries)
    {
        try
        {
            foreach (string filePath in Directory.EnumerateFiles(directoryPath))
            {
                string ext = Path.GetExtension(filePath);
                if (!StartupExtensions.Contains(ext))
                    continue;

                string fileName = Path.GetFileName(filePath);

                var entry = new PersistenceEntry
                {
                    Name = fileName,
                    Type = "StartupFolder",
                    Location = locationLabel,
                    Value = filePath,
                    DetectedAt = DateTime.Now,
                };

                EvaluateStartupFile(entry, filePath, ext);
                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[PersistenceScanner] Error scanning startup directory '{Dir}': {Message}",
                directoryPath, ex.Message);
        }
    }

    private static void EvaluateStartupFile(PersistenceEntry entry, string filePath, string ext)
    {
        var reasons = new List<string>();

        // Script files in startup are inherently risky
        string lowerExt = ext.ToLowerInvariant();
        if (lowerExt is ".vbs" or ".vbe" or ".js" or ".jse" or ".wsf" or ".wsh" or ".bat" or ".cmd")
        {
            reasons.Add($"Script file ({ext}) in startup folder can execute arbitrary code");
        }

        // .exe or .scr directly in startup without .lnk wrapper
        if (lowerExt is ".exe" or ".scr" or ".com" or ".pif")
        {
            reasons.Add($"Direct executable ({ext}) placed in startup folder");
        }

        // Check file creation time - very recently created files are more suspicious
        try
        {
            var fi = new FileInfo(filePath);
            if (fi.Exists && (DateTime.Now - fi.CreationTime).TotalHours < 24)
            {
                reasons.Add("File was created within the last 24 hours");
            }
        }
        catch { /* access denied is possible */ }

        // Check if the path fragment is suspicious
        string lower = filePath.ToLowerInvariant();
        foreach (string fragment in SuspiciousPathFragments)
        {
            if (lower.Contains(fragment.ToLowerInvariant()))
            {
                reasons.Add($"File path contains suspicious fragment '{fragment}'");
                break;
            }
        }

        if (reasons.Count > 0)
        {
            entry.IsSuspicious = true;
            entry.Reason = string.Join("; ", reasons);
        }
    }

    // -------------------------------------------------------------------
    //  3. Scheduled Tasks (via schtasks.exe)
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanScheduledTasks()
    {
        var entries = new List<PersistenceEntry>();

        try
        {
            string output = RunProcess("schtasks", "/query /fo CSV /v");
            if (string.IsNullOrWhiteSpace(output))
            {
                SglLogger.Debug("[PersistenceScanner] schtasks returned no output.");
                return entries;
            }

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return entries;

            // First line is the CSV header. Parse column indices.
            string[] headers = ParseCsvLine(lines[0]);
            int taskNameIdx = Array.FindIndex(headers, h =>
                h.Contains("TaskName", StringComparison.OrdinalIgnoreCase));
            int taskToRunIdx = Array.FindIndex(headers, h =>
                h.Contains("Task To Run", StringComparison.OrdinalIgnoreCase));
            int statusIdx = Array.FindIndex(headers, h =>
                h.Contains("Status", StringComparison.OrdinalIgnoreCase));
            int authorIdx = Array.FindIndex(headers, h =>
                h.Contains("Author", StringComparison.OrdinalIgnoreCase));
            int scheduleTypeIdx = Array.FindIndex(headers, h =>
                h.Contains("Schedule Type", StringComparison.OrdinalIgnoreCase));

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    string[] cols = ParseCsvLine(lines[i]);
                    if (cols.Length < 3)
                        continue;

                    string taskName = taskNameIdx >= 0 && taskNameIdx < cols.Length
                        ? cols[taskNameIdx].Trim() : "";
                    string taskToRun = taskToRunIdx >= 0 && taskToRunIdx < cols.Length
                        ? cols[taskToRunIdx].Trim() : "";
                    string status = statusIdx >= 0 && statusIdx < cols.Length
                        ? cols[statusIdx].Trim() : "";
                    string author = authorIdx >= 0 && authorIdx < cols.Length
                        ? cols[authorIdx].Trim() : "";
                    string scheduleType = scheduleTypeIdx >= 0 && scheduleTypeIdx < cols.Length
                        ? cols[scheduleTypeIdx].Trim() : "";

                    if (string.IsNullOrEmpty(taskName) || taskName.Equals("TaskName", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entry = new PersistenceEntry
                    {
                        Name = taskName,
                        Type = "ScheduledTask",
                        Location = $"Author: {author} | Schedule: {scheduleType} | Status: {status}",
                        Value = taskToRun,
                        DetectedAt = DateTime.Now,
                    };

                    EvaluateScheduledTask(entry, taskName, taskToRun);
                    entries.Add(entry);
                }
                catch
                {
                    // Malformed CSV line; skip.
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[PersistenceScanner] Error scanning scheduled tasks.", ex);
        }

        SglLogger.Information("[PersistenceScanner] Scheduled task scan found {Count} entries.", entries.Count);
        return entries;
    }

    private static void EvaluateScheduledTask(PersistenceEntry entry, string taskName, string taskToRun)
    {
        // Skip well-known Microsoft / vendor tasks
        foreach (string safePrefix in SafeTaskPathPrefixes)
        {
            if (taskName.StartsWith(safePrefix, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var reasons = new List<string>();
        string lower = taskToRun.ToLowerInvariant();

        // Suspicious command patterns
        if (lower.Contains("powershell") && (lower.Contains("-enc") || lower.Contains("-e ") ||
            lower.Contains("bypass") || lower.Contains("hidden") || lower.Contains("downloadstring") ||
            lower.Contains("invoke-webrequest") || lower.Contains("iex") || lower.Contains("invoke-expression")))
        {
            reasons.Add("Scheduled task runs PowerShell with suspicious flags or download commands");
        }

        if (lower.Contains("cmd") && lower.Contains("/c") &&
            (lower.Contains("curl") || lower.Contains("certutil") || lower.Contains("bitsadmin")))
        {
            reasons.Add("Scheduled task uses cmd.exe to invoke download utilities");
        }

        if (lower.Contains("mshta") || lower.Contains("regsvr32") || lower.Contains("rundll32"))
        {
            if (lower.Contains("http") || lower.Contains("javascript:") || lower.Contains("vbscript:"))
            {
                reasons.Add("Scheduled task uses LOLBin with remote/script payload");
            }
        }

        // Tasks pointing to temp directories
        foreach (string fragment in SuspiciousPathFragments)
        {
            if (lower.Contains(fragment.ToLowerInvariant()))
            {
                reasons.Add($"Task executable path contains suspicious fragment '{fragment}'");
                break;
            }
        }

        // Task name that looks randomly generated (lots of hex chars)
        if (taskName.Length > 5 && !taskName.StartsWith("\\Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            string leafName = taskName.Contains('\\')
                ? taskName[(taskName.LastIndexOf('\\') + 1)..] : taskName;
            if (leafName.Length >= 16 && leafName.All(c => char.IsLetterOrDigit(c)) &&
                leafName.Count(c => char.IsDigit(c)) > leafName.Length / 3)
            {
                reasons.Add("Task name appears randomly generated (possible malware persistence)");
            }
        }

        if (reasons.Count > 0)
        {
            entry.IsSuspicious = true;
            entry.Reason = string.Join("; ", reasons);
        }
    }

    // -------------------------------------------------------------------
    //  4. Windows Services
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanServices()
    {
        var entries = new List<PersistenceEntry>();

        try
        {
            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController svc in services)
            {
                try
                {
                    string? imagePath = GetServiceImagePath(svc.ServiceName);

                    var entry = new PersistenceEntry
                    {
                        Name = svc.ServiceName,
                        Type = "Service",
                        Location = $"DisplayName: {svc.DisplayName} | Status: {svc.Status}",
                        Value = imagePath ?? "(unknown)",
                        DetectedAt = DateTime.Now,
                    };

                    EvaluateService(entry, imagePath);
                    entries.Add(entry);
                }
                catch
                {
                    // Access issues on certain services are expected.
                }
                finally
                {
                    svc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[PersistenceScanner] Error enumerating services.", ex);
        }

        SglLogger.Information("[PersistenceScanner] Service scan found {Count} entries.", entries.Count);
        return entries;
    }

    private static string? GetServiceImagePath(string serviceName)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);
            return key?.GetValue("ImagePath")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void EvaluateService(PersistenceEntry entry, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        var reasons = new List<string>();
        string lower = imagePath.ToLowerInvariant().Trim('"');

        // Check if the service binary is from a trusted location
        bool isTrusted = false;
        foreach (string root in TrustedServicePathRoots)
        {
            if (lower.StartsWith(root.ToLowerInvariant()))
            {
                isTrusted = true;
                break;
            }
        }

        if (!isTrusted)
        {
            // Service binaries outside standard locations deserve attention (not
            // necessarily malicious, but worth flagging).
            foreach (string fragment in SuspiciousPathFragments)
            {
                if (lower.Contains(fragment.ToLowerInvariant()))
                {
                    reasons.Add($"Service binary path contains suspicious fragment '{fragment}'");
                    break;
                }
            }
        }

        // Unquoted service path with spaces (privilege escalation risk)
        if (!imagePath.TrimStart().StartsWith("\"") && imagePath.Contains(' ') &&
            !lower.StartsWith("\\systemroot") && !lower.StartsWith(@"c:\windows\system32\svchost"))
        {
            // Check if the path portion (before arguments) actually has spaces
            string pathPortion = imagePath.Split(new[] { " -", " /", " /" }, StringSplitOptions.None)[0].Trim();
            if (pathPortion.Contains(' ') && !pathPortion.StartsWith("\""))
            {
                reasons.Add("Unquoted service path with spaces (potential privilege escalation vector)");
            }
        }

        // Service pointing to cmd/powershell directly
        if (lower.Contains("powershell") || lower.Contains("cmd.exe /c"))
        {
            reasons.Add("Service configured to execute via cmd.exe or powershell.exe");
        }

        if (reasons.Count > 0)
        {
            entry.IsSuspicious = true;
            entry.Reason = string.Join("; ", reasons);
        }
    }

    // -------------------------------------------------------------------
    //  5. WMI Event Subscriptions
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanWmiSubscriptions()
    {
        var entries = new List<PersistenceEntry>();

        try
        {
            // Scan WMI EventFilter objects
            string filterOutput = RunProcess("wmic",
                @"/namespace:\\root\subscription path __EventFilter list /format:csv");
            ParseWmiCsvOutput(filterOutput, "WMI EventFilter", entries);

            // Scan WMI EventConsumer objects (CommandLineEventConsumer and ActiveScriptEventConsumer)
            string consumerOutput = RunProcess("wmic",
                @"/namespace:\\root\subscription path CommandLineEventConsumer list /format:csv");
            ParseWmiCsvOutput(consumerOutput, "WMI CommandLineConsumer", entries);

            string scriptConsumerOutput = RunProcess("wmic",
                @"/namespace:\\root\subscription path ActiveScriptEventConsumer list /format:csv");
            ParseWmiCsvOutput(scriptConsumerOutput, "WMI ActiveScriptConsumer", entries);

            // Scan FilterToConsumerBinding
            string bindingOutput = RunProcess("wmic",
                @"/namespace:\\root\subscription path __FilterToConsumerBinding list /format:csv");
            ParseWmiCsvOutput(bindingOutput, "WMI FilterToConsumerBinding", entries);
        }
        catch (Exception ex)
        {
            SglLogger.Error("[PersistenceScanner] Error scanning WMI subscriptions.", ex);
        }

        SglLogger.Information("[PersistenceScanner] WMI subscription scan found {Count} entries.", entries.Count);
        return entries;
    }

    private void ParseWmiCsvOutput(string output, string wmiType, List<PersistenceEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return;

        string[] headers = ParseCsvLine(lines[0]);
        int nameIdx = Array.FindIndex(headers, h =>
            h.Contains("Name", StringComparison.OrdinalIgnoreCase));

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] cols = ParseCsvLine(line);
            if (cols.Length < 2)
                continue;

            string name = nameIdx >= 0 && nameIdx < cols.Length ? cols[nameIdx].Trim() : "";
            string fullLine = line;

            if (string.IsNullOrEmpty(name))
            {
                // Use first non-empty column as name fallback
                name = cols.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "(unnamed)";
            }

            var entry = new PersistenceEntry
            {
                Name = name,
                Type = "WMI",
                Location = wmiType,
                Value = Truncate(fullLine, 500),
                DetectedAt = DateTime.Now,
            };

            // All WMI event subscriptions are suspicious by nature; they are
            // a well-known persistence technique (T1546.003).
            entry.IsSuspicious = true;
            entry.Reason = $"WMI event subscription detected ({wmiType}). " +
                           "WMI persistence is a common technique used by advanced malware (MITRE T1546.003).";

            entries.Add(entry);
        }
    }

    // -------------------------------------------------------------------
    //  6. Browser Extensions
    // -------------------------------------------------------------------

    private List<PersistenceEntry> ScanBrowserExtensions()
    {
        var entries = new List<PersistenceEntry>();
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Chrome extensions
        string chromeExtDir = Path.Combine(userProfile,
            @"AppData\Local\Google\Chrome\User Data\Default\Extensions");
        ScanBrowserExtensionDirectory(chromeExtDir, "Chrome", entries);

        // Edge extensions
        string edgeExtDir = Path.Combine(userProfile,
            @"AppData\Local\Microsoft\Edge\User Data\Default\Extensions");
        ScanBrowserExtensionDirectory(edgeExtDir, "Edge", entries);

        // Firefox extensions
        string firefoxProfilesDir = Path.Combine(userProfile,
            @"AppData\Roaming\Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfilesDir))
        {
            try
            {
                foreach (string profileDir in Directory.EnumerateDirectories(firefoxProfilesDir))
                {
                    string extensionsDir = Path.Combine(profileDir, "extensions");
                    ScanBrowserExtensionDirectory(extensionsDir, "Firefox", entries);
                }
            }
            catch (Exception ex)
            {
                SglLogger.Debug("[PersistenceScanner] Error scanning Firefox profiles: {Message}", ex.Message);
            }
        }

        // Brave extensions
        string braveExtDir = Path.Combine(userProfile,
            @"AppData\Local\BraveSoftware\Brave-Browser\User Data\Default\Extensions");
        ScanBrowserExtensionDirectory(braveExtDir, "Brave", entries);

        SglLogger.Information("[PersistenceScanner] Browser extension scan found {Count} entries.", entries.Count);
        return entries;
    }

    private void ScanBrowserExtensionDirectory(string extensionsDir, string browserName,
        List<PersistenceEntry> entries)
    {
        if (!Directory.Exists(extensionsDir))
            return;

        try
        {
            foreach (string extDir in Directory.EnumerateDirectories(extensionsDir))
            {
                string extId = Path.GetFileName(extDir);

                // Try to read the extension manifest to get the real name
                string extName = extId;
                string extDescription = "";

                try
                {
                    // Walk into the version subdirectory to find manifest.json
                    foreach (string versionDir in Directory.EnumerateDirectories(extDir))
                    {
                        string manifestPath = Path.Combine(versionDir, "manifest.json");
                        if (File.Exists(manifestPath))
                        {
                            string manifestContent = File.ReadAllText(manifestPath);
                            // Simple JSON parsing without bringing in a whole JSON library
                            string? parsedName = ExtractJsonStringValue(manifestContent, "name");
                            string? parsedDesc = ExtractJsonStringValue(manifestContent, "description");
                            if (!string.IsNullOrEmpty(parsedName) && !parsedName.StartsWith("__MSG_"))
                            {
                                extName = parsedName;
                            }
                            if (!string.IsNullOrEmpty(parsedDesc) && !parsedDesc.StartsWith("__MSG_"))
                            {
                                extDescription = parsedDesc;
                            }
                            break;
                        }
                    }
                }
                catch { /* manifest read failed; proceed with ID */ }

                var entry = new PersistenceEntry
                {
                    Name = extName,
                    Type = "BrowserExtension",
                    Location = $"{browserName} | ID: {extId}",
                    Value = extDir,
                    DetectedAt = DateTime.Now,
                };

                EvaluateBrowserExtension(entry, extId, extName, extDescription, extDir);
                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[PersistenceScanner] Error scanning {Browser} extensions: {Message}",
                browserName, ex.Message);
        }
    }

    private static void EvaluateBrowserExtension(PersistenceEntry entry, string extId,
        string extName, string extDescription, string extDir)
    {
        var reasons = new List<string>();

        // Extensions with very short IDs or non-standard IDs (not 32 lowercase chars)
        // Chromium extension IDs are 32 lowercase a-p characters; anything else is unusual.
        if (extId.Length != 32 || !extId.All(c => c >= 'a' && c <= 'p'))
        {
            // Firefox uses different format, so only flag for Chromium-based
            if (!extDir.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("Extension ID does not match standard Chromium format (may be sideloaded)");
            }
        }

        // Check if the extension was installed very recently
        try
        {
            var dirInfo = new DirectoryInfo(extDir);
            if (dirInfo.Exists && (DateTime.Now - dirInfo.CreationTime).TotalHours < 24)
            {
                reasons.Add("Extension directory was created within the last 24 hours");
            }
        }
        catch { /* access issues */ }

        // Suspicious keywords in description
        string lowerDesc = extDescription.ToLowerInvariant();
        if (lowerDesc.Contains("crypto") && lowerDesc.Contains("mine"))
        {
            reasons.Add("Extension description contains crypto-mining related keywords");
        }
        if (lowerDesc.Contains("keylog") || lowerDesc.Contains("password") && lowerDesc.Contains("steal"))
        {
            reasons.Add("Extension description contains credential-theft related keywords");
        }

        if (reasons.Count > 0)
        {
            entry.IsSuspicious = true;
            entry.Reason = string.Join("; ", reasons);
        }
    }

    // -------------------------------------------------------------------
    //  Utility methods
    // -------------------------------------------------------------------

    /// <summary>Runs an external process and returns its stdout.</summary>
    private static string RunProcess(string fileName, string arguments)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);
            return output;
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[PersistenceScanner] Failed to run {File} {Args}: {Message}",
                fileName, arguments, ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Minimal CSV line parser that handles quoted fields.
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
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// Extracts a string value from JSON content without a JSON library.
    /// Finds the first occurrence of "key": "value" and returns value.
    /// </summary>
    private static string? ExtractJsonStringValue(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
            return null;

        int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
        if (colonIndex < 0)
            return null;

        int quoteStart = json.IndexOf('"', colonIndex + 1);
        if (quoteStart < 0)
            return null;

        int quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteEnd < 0)
            return null;

        return json[(quoteStart + 1)..quoteEnd];
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
