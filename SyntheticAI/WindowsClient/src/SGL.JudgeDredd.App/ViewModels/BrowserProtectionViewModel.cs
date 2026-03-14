using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class BrowserProtectionViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;

    [ObservableProperty]
    private bool _protectionEnabled = true;

    [ObservableProperty]
    private int _browsersMonitored;

    [ObservableProperty]
    private int _threatsBlocked;

    [ObservableProperty]
    private int _maliciousUrlsBlocked;

    [ObservableProperty]
    private int _trackersBlocked;

    [ObservableProperty]
    private string _monitorStatus = "Protection active - Waiting for first scan...";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _proxyTampered;

    [ObservableProperty]
    private string _proxyStatus = "No proxy tampering detected";

    [ObservableProperty]
    private bool _dnsMonitorEnabled = true;

    [ObservableProperty]
    private bool _extensionScanEnabled = true;

    [ObservableProperty]
    private bool _hijackDetectionEnabled = true;

    [ObservableProperty]
    private bool _proxyMonitorEnabled = true;

    [ObservableProperty]
    private string _scanDiagnostics = "No diagnostics run yet";

    [ObservableProperty]
    private bool _screenRecordingProtection = true;

    [ObservableProperty]
    private int _screenCaptureAttempts;

    [ObservableProperty]
    private string _screenRecordingStatus = "No screen capture attempts detected";

    public ObservableCollection<BrowserThreat> DetectedThreats { get; } = [];
    public ObservableCollection<ActiveBrowserInfo> ActiveBrowsers { get; } = [];

    // ── Known browser process names ──────────────────────────────────────
    private static readonly Dictionary<string, string> BrowserProcessMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "chrome", "Google Chrome" },
        { "firefox", "Mozilla Firefox" },
        { "msedge", "Microsoft Edge" },
        { "brave", "Brave Browser" },
        { "opera", "Opera" },
        { "vivaldi", "Vivaldi" },
        { "arc", "Arc Browser" },
    };

    // ── Known malicious / phishing domains (50+) ─────────────────────────
    private static readonly HashSet<string> MaliciousDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        // Phishing
        "secure-login-verify.com",
        "account-verify-support.com",
        "login-microsoftonline.xyz",
        "apple-id-verify.net",
        "paypal-confirm-identity.com",
        "amazon-security-alert.net",
        "netflix-billing-update.com",
        "chase-secure-login.net",
        "bankofamerica-alert.com",
        "wellsfargo-verify.net",
        "citi-secure-update.com",
        "usps-delivery-notice.com",
        "fedex-tracking-update.net",
        "dhl-parcel-notification.com",
        "irs-tax-refund.net",
        "google-docs-share.xyz",
        "dropbox-file-shared.net",
        "linkedin-connection.xyz",
        "instagram-verify.net",
        "facebook-security-check.com",
        // Malware distribution
        "free-software-crack.com",
        "keygen-download-free.net",
        "activator-windows-free.com",
        "cracked-games-download.net",
        "free-antivirus-scan.com",
        "your-pc-is-infected.net",
        "driver-update-free.com",
        "flash-player-update.net",
        "java-update-required.com",
        "browser-update-now.net",
        "codec-pack-download.com",
        "free-vpn-unlimited.net",
        "torrent-download-fast.com",
        "warez-full-download.net",
        "nulled-scripts-free.com",
        // Scam / tech support
        "microsoft-support-alert.com",
        "windows-defender-warning.net",
        "apple-support-call.com",
        "virus-alert-warning.net",
        "computer-locked-call.com",
        "tech-support-helpdesk.net",
        "error-code-fix.com",
        "system-warning-alert.net",
        "firewall-alert-critical.com",
        "security-warning-popup.net",
        // Cryptojacking / exploit kits
        "coinhive.com",
        "coin-hive.com",
        "crypto-loot.com",
        "cryptoloot.pro",
        "minero.cc",
        "webmine.pro",
        "ppoi.org",
        "cryptonight.wasm",
        "exploit-kit-landing.net",
        "angler-exploit.com",
    };

    // ── Suspicious browser extension identifiers ─────────────────────────
    private static readonly Dictionary<string, string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "hxxp-injector", "HTTP Injector - potential ad injection" },
        { "web-companion", "Web Companion - browser hijacker (Adaware)" },
        { "ask-toolbar", "Ask Toolbar - search hijacker" },
        { "babylon-toolbar", "Babylon Toolbar - search hijacker" },
        { "conduit-toolbar", "Conduit Toolbar - browser hijacker" },
        { "delta-toolbar", "Delta Toolbar - search modifier" },
        { "sweetpage", "Sweet Page - homepage hijacker" },
        { "mysearchdial", "MySearchDial - search hijacker" },
        { "superfish", "Superfish - SSL/TLS MITM adware" },
        { "wajam", "Wajam - social search injector" },
        { "shopper-pro", "Shopper Pro - ad injection" },
        { "browser-guardian", "Browser Guardian - adware" },
        { "price-meter", "PriceMeter - tracking adware" },
        { "savefrom-helper", "SaveFrom.net Helper - privacy risk" },
        { "hola-vpn", "Hola VPN - peer bandwidth sharing risk" },
        { "stylish", "Stylish - known analytics data harvester" },
        { "web-of-trust", "Web of Trust - sells browsing data" },
        { "hover-zoom", "Hover Zoom - known spyware" },
        { "smooth-gestures", "SmoothGestures - known spyware" },
        { "findmefreebies", "FindMeFreebies - ad injection" },
    };

    // ── Known legitimate default homepages / search engines ──────────────
    private static readonly HashSet<string> LegitimateHomepages = new(StringComparer.OrdinalIgnoreCase)
    {
        "about:blank", "about:newtab", "about:home",
        "chrome://newtab", "edge://newtab", "chrome://new-tab-page",
        "https://www.google.com", "https://www.google.com/",
        "https://www.bing.com", "https://www.bing.com/",
        "https://duckduckgo.com", "https://duckduckgo.com/",
        "https://search.brave.com", "https://search.brave.com/",
        "https://www.mozilla.org", "https://www.startpage.com",
    };

    private static readonly HashSet<string> LegitimateSearchEngines = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google", "Bing", "DuckDuckGo", "Yahoo", "Brave Search",
        "Startpage", "Ecosia", "Qwant",
    };

    public BrowserProtectionViewModel()
    {
        Title = "Browser Protection";
        StartAutoMonitoring();
        // Delay initial scan to ensure UI is ready
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            async () => await ScanBrowsersAsync());
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ScanBrowsersAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        MonitorStatus = "Scanning browsers for threats...";

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Scanning your browsers for threats...");

        try
        {
            await PerformFullScanAsync();

            if (DetectedThreats.Count > 0)
            {
                AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
                await AvatarViewModel.Instance.ShowSpeechBubble(
                    $"Found {DetectedThreats.Count} browser threat(s)! Review them below.");
            }
            else
            {
                AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
                await AvatarViewModel.Instance.ShowSpeechBubble("All browsers look clean. No threats detected.");
            }
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Scan error: {ex.Message}";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Browser scan encountered an error.");
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task BlockUrlAsync(BrowserThreat? threat)
    {
        if (threat is null) return;

        threat.IsBlocked = true;
        ThreatsBlocked++;

        if (threat.ThreatType is "Malicious URL" or "Phishing")
            MaliciousUrlsBlocked++;
        else if (threat.ThreatType == "Tracking")
            TrackersBlocked++;

        MonitorStatus = $"Blocked: {threat.Url}";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Blocked threat: {threat.Url}");
    }

    [RelayCommand]
    private void AllowUrl(BrowserThreat? threat)
    {
        if (threat is null) return;

        threat.IsBlocked = false;
        DetectedThreats.Remove(threat);
        MonitorStatus = $"Allowed: {threat.Url} (removed from threats)";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ScanBrowsersAsync();
    }

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        IsScanning = true;
        MonitorStatus = "Running browser diagnostics...";

        var diag = new System.Text.StringBuilder();
        diag.AppendLine($"Browser Diagnostics Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        diag.AppendLine(new string('=', 50));

        // 1. Check active browsers
        var processes = System.Diagnostics.Process.GetProcesses();
        var browserCount = 0;
        foreach (var proc in processes)
        {
            try
            {
                if (BrowserProcessMap.ContainsKey(proc.ProcessName))
                {
                    browserCount++;
                    string mem = FormatBytes(proc.WorkingSet64);
                    diag.AppendLine($"  [{proc.ProcessName}] PID: {proc.Id} | Memory: {mem} | Status: Running");
                }
            }
            catch { }
        }
        diag.AppendLine($"\nActive Browsers: {browserCount}");

        // 2. Check DNS settings
        diag.AppendLine("\n--- DNS Configuration ---");
        try
        {
            var dnsInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/all",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var dnsProc = System.Diagnostics.Process.Start(dnsInfo);
            if (dnsProc != null)
            {
                var output = dnsProc.StandardOutput.ReadToEnd();
                dnsProc.WaitForExit(3000);
                var dnsLines = output.Split('\n')
                    .Where(l => l.Contains("DNS Servers", StringComparison.OrdinalIgnoreCase))
                    .Take(3);
                foreach (var line in dnsLines)
                    diag.AppendLine($"  {line.Trim()}");
            }
        }
        catch (Exception ex) { diag.AppendLine($"  DNS check failed: {ex.Message}"); }

        // 3. Check proxy
        diag.AppendLine("\n--- Proxy Settings ---");
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings");
            var proxyEnable = key?.GetValue("ProxyEnable");
            var proxyServer = key?.GetValue("ProxyServer") as string;
            diag.AppendLine($"  Proxy Enabled: {(proxyEnable is int pe && pe == 1 ? "YES" : "NO")}");
            if (!string.IsNullOrEmpty(proxyServer))
                diag.AppendLine($"  Proxy Server: {proxyServer}");
            else
                diag.AppendLine("  Proxy Server: None configured");
        }
        catch (Exception ex) { diag.AppendLine($"  Proxy check failed: {ex.Message}"); }

        // 4. Extension count
        diag.AppendLine("\n--- Browser Extensions ---");
        var extDirs = new[]
        {
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Extensions"), "Chrome"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Extensions"), "Edge"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\User Data\Default\Extensions"), "Brave"),
        };
        foreach (var (dir, name) in extDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    var count = Directory.GetDirectories(dir).Length;
                    diag.AppendLine($"  {name}: {count} extension(s) installed");
                }
                else
                {
                    diag.AppendLine($"  {name}: Not installed");
                }
            }
            catch { diag.AppendLine($"  {name}: Access denied"); }
        }

        // 5. Summary
        diag.AppendLine($"\n--- Summary ---");
        diag.AppendLine($"  Threats Detected: {DetectedThreats.Count}");
        diag.AppendLine($"  Proxy Tampered: {ProxyTampered}");
        diag.AppendLine($"  Protection Status: {(ProtectionEnabled ? "ENABLED" : "DISABLED")}");
        diag.AppendLine($"  DNS Monitoring: {(DnsMonitorEnabled ? "ON" : "OFF")}");
        diag.AppendLine($"  Extension Scanning: {(ExtensionScanEnabled ? "ON" : "OFF")}");
        diag.AppendLine($"  Hijack Detection: {(HijackDetectionEnabled ? "ON" : "OFF")}");
        diag.AppendLine($"  Proxy Monitoring: {(ProxyMonitorEnabled ? "ON" : "OFF")}");

        foreach (var p in processes) { try { p.Dispose(); } catch { } }

        ScanDiagnostics = diag.ToString();
        IsScanning = false;
        MonitorStatus = $"Diagnostics complete - {DateTime.Now:HH:mm:ss}";

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
        await AvatarViewModel.Instance.ShowSpeechBubble("Browser diagnostics complete. Review the report below.");
    }

    [RelayCommand]
    private async Task ToggleProtectionAsync()
    {
        ProtectionEnabled = !ProtectionEnabled;

        if (ProtectionEnabled)
        {
            StartAutoMonitoring();
            MonitorStatus = "Protection re-enabled. Monitoring active.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
            await AvatarViewModel.Instance.ShowSpeechBubble("Browser protection is back online!");
        }
        else
        {
            StopAutoMonitoring();
            MonitorStatus = "Protection disabled. Browsers are NOT being monitored.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Warning: Browser protection has been disabled.");
        }
    }

    // ── Auto-monitoring ──────────────────────────────────────────────────

    private void StartAutoMonitoring()
    {
        if (_monitorTimer != null) return;

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _monitorTimer.Tick += async (_, _) =>
        {
            if (ProtectionEnabled && !IsScanning)
            {
                IsScanning = true;
                try { await PerformFullScanAsync(); }
                finally { IsScanning = false; }
            }
        };
        _monitorTimer.Start();
    }

    private void StopAutoMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;
    }

    // ── Core scanning logic ──────────────────────────────────────────────

    private async Task PerformFullScanAsync()
    {
        await Task.Run(() =>
        {
            ScanRunningBrowsers();
            if (HijackDetectionEnabled) ScanForBrowserHijacking();
            if (ExtensionScanEnabled) ScanForSuspiciousExtensions();
            if (DnsMonitorEnabled) ScanDnsCache();
            if (ProxyMonitorEnabled) CheckProxySettings();
            if (ScreenRecordingProtection) ScanForScreenCapture();
        });

        BrowsersMonitored = ActiveBrowsers.Count;
        MonitorStatus = ProtectionEnabled
            ? $"Protection active - {BrowsersMonitored} browser(s) monitored, "
              + $"{DetectedThreats.Count} threat(s) - Last scan: {DateTime.Now:HH:mm:ss}"
            : "Protection disabled.";
    }

    private void ScanRunningBrowsers()
    {
        var processes = Process.GetProcesses();
        var browserAggregates = new Dictionary<string, (string browserName, int mainPid, string exePath, long totalMemory, int processCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var proc in processes)
        {
            try
            {
                if (BrowserProcessMap.TryGetValue(proc.ProcessName, out var browserName))
                {
                    if (browserAggregates.TryGetValue(proc.ProcessName, out var existing))
                    {
                        browserAggregates[proc.ProcessName] = (existing.browserName, existing.mainPid, existing.exePath, existing.totalMemory + proc.WorkingSet64, existing.processCount + 1);
                    }
                    else
                    {
                        string exePath;
                        try { exePath = proc.MainModule?.FileName ?? "Unknown"; }
                        catch { exePath = "Access Denied"; }

                        browserAggregates[proc.ProcessName] = (browserName, proc.Id, exePath, proc.WorkingSet64, 1);
                    }
                }
            }
            catch { /* Access denied or exited */ }
        }

        foreach (var p in processes) { try { p.Dispose(); } catch { } }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ActiveBrowsers.Clear();
            foreach (var kvp in browserAggregates)
            {
                var (browserName, mainPid, exePath, totalMemory, processCount) = kvp.Value;
                ActiveBrowsers.Add(new ActiveBrowserInfo
                {
                    ProcessName = kvp.Key,
                    BrowserName = browserName,
                    Pid = mainPid,
                    ExePath = exePath,
                    MemoryUsage = $"{FormatBytes(totalMemory)} ({processCount} process{(processCount > 1 ? "es" : "")})",
                    Status = "Running",
                    DetectedAt = DateTime.Now,
                });
            }
        });
    }

    private void ScanForBrowserHijacking()
    {
        // Check Chrome homepage & search engine via registry
        CheckRegistryHomepage(
            @"SOFTWARE\Policies\Google\Chrome",
            "HomepageLocation",
            "Google Chrome");

        CheckRegistryHomepage(
            @"SOFTWARE\Policies\Google\Chrome",
            "DefaultSearchProviderName",
            "Google Chrome");

        // Check Edge homepage & search engine via registry
        CheckRegistryHomepage(
            @"SOFTWARE\Policies\Microsoft\Edge",
            "HomepageLocation",
            "Microsoft Edge");

        CheckRegistryHomepage(
            @"SOFTWARE\Policies\Microsoft\Edge",
            "DefaultSearchProviderName",
            "Microsoft Edge");

        // Check the default browser startup page in HKCU
        CheckRegistryHomepage(
            @"SOFTWARE\Microsoft\Internet Explorer\Main",
            "Start Page",
            "Internet Explorer / System Default");

        // Check for BHO (Browser Helper Objects) - classic hijack vector
        ScanBrowserHelperObjects();
    }

    private void CheckRegistryHomepage(string keyPath, string valueName, string browserName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName) as string;

            if (string.IsNullOrEmpty(value)) return;

            bool isLegitimate = valueName.Contains("Search", StringComparison.OrdinalIgnoreCase)
                ? LegitimateSearchEngines.Contains(value)
                : LegitimateHomepages.Contains(value);

            if (!isLegitimate)
            {
                var threatType = valueName.Contains("Search", StringComparison.OrdinalIgnoreCase)
                    ? "Search engine may have been hijacked"
                    : "Homepage may have been hijacked";

                AddThreatOnDispatcher(new BrowserThreat
                {
                    BrowserName = browserName,
                    ThreatType = "Hijack",
                    Url = value,
                    Description = $"{threatType} to: {value}",
                    RiskLevel = "High",
                    DetectedAt = DateTime.Now,
                    IsBlocked = false,
                });
            }
        }
        catch { /* Registry access denied or key doesn't exist */ }
    }

    private void ScanBrowserHelperObjects()
    {
        try
        {
            using var bhoKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects");

            if (bhoKey == null) return;

            var subKeys = bhoKey.GetSubKeyNames();
            if (subKeys.Length > 5) // Unusually high number of BHOs is suspicious
            {
                AddThreatOnDispatcher(new BrowserThreat
                {
                    BrowserName = "System",
                    ThreatType = "Hijack",
                    Url = "Registry: Browser Helper Objects",
                    Description = $"Unusually high number of BHOs installed ({subKeys.Length}). "
                                  + "Possible browser hijack via BHO injection.",
                    RiskLevel = "Medium",
                    DetectedAt = DateTime.Now,
                    IsBlocked = false,
                });
            }
        }
        catch { /* Access denied */ }
    }

    private void ScanForSuspiciousExtensions()
    {
        // Chrome extensions directory
        var chromeExtDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\Default\Extensions");

        ScanExtensionDirectory(chromeExtDir, "Google Chrome");

        // Edge extensions directory
        var edgeExtDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\Edge\User Data\Default\Extensions");

        ScanExtensionDirectory(edgeExtDir, "Microsoft Edge");

        // Brave extensions directory
        var braveExtDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"BraveSoftware\Brave-Browser\User Data\Default\Extensions");

        ScanExtensionDirectory(braveExtDir, "Brave Browser");
    }

    private void ScanExtensionDirectory(string extensionDir, string browserName)
    {
        try
        {
            if (!Directory.Exists(extensionDir)) return;

            var extFolders = Directory.GetDirectories(extensionDir);
            foreach (var folder in extFolders)
            {
                var folderName = Path.GetFileName(folder).ToLowerInvariant();

                // Check manifest files for suspicious names
                var manifestFiles = Directory.GetFiles(folder, "manifest.json", SearchOption.AllDirectories);
                foreach (var manifest in manifestFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(manifest).ToLowerInvariant();

                        foreach (var (suspiciousId, description) in SuspiciousExtensions)
                        {
                            if (content.Contains(suspiciousId, StringComparison.OrdinalIgnoreCase)
                                || folderName.Contains(suspiciousId, StringComparison.OrdinalIgnoreCase))
                            {
                                AddThreatOnDispatcher(new BrowserThreat
                                {
                                    BrowserName = browserName,
                                    ThreatType = "Suspicious Extension",
                                    Url = folder,
                                    Description = description,
                                    RiskLevel = "Medium",
                                    DetectedAt = DateTime.Now,
                                    IsBlocked = false,
                                });
                            }
                        }

                        // Flag extensions requesting dangerous permissions
                        if (content.Contains("\"webRequestBlocking\"") && content.Contains("\"<all_urls>\""))
                        {
                            AddThreatOnDispatcher(new BrowserThreat
                            {
                                BrowserName = browserName,
                                ThreatType = "Suspicious Extension",
                                Url = folder,
                                Description = "Extension intercepts ALL web requests (webRequestBlocking + <all_urls>). "
                                              + "Potential man-in-the-browser risk.",
                                RiskLevel = "High",
                                DetectedAt = DateTime.Now,
                                IsBlocked = false,
                            });
                        }
                    }
                    catch { /* File read error */ }
                }
            }
        }
        catch { /* Directory access error */ }
    }

    private void ScanDnsCache()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/displaydns",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            foreach (var domain in MaliciousDomains)
            {
                if (output.Contains(domain, StringComparison.OrdinalIgnoreCase))
                {
                    AddThreatOnDispatcher(new BrowserThreat
                    {
                        BrowserName = "DNS Cache",
                        ThreatType = "Malicious URL",
                        Url = domain,
                        Description = $"Malicious domain '{domain}' found in local DNS cache. "
                                      + "This domain may have been recently visited.",
                        RiskLevel = "Critical",
                        DetectedAt = DateTime.Now,
                        IsBlocked = false,
                    });
                }
            }

            // Also check for known phishing patterns in DNS cache
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Record Name", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = trimmed.Split(':');
                if (parts.Length < 2) continue;

                var resolvedHost = parts[1].Trim().ToLowerInvariant();

                // Detect typosquatting of popular domains
                if (IsTyposquatDomain(resolvedHost))
                {
                    AddThreatOnDispatcher(new BrowserThreat
                    {
                        BrowserName = "DNS Cache",
                        ThreatType = "Phishing",
                        Url = resolvedHost,
                        Description = $"Possible typosquat / phishing domain detected: {resolvedHost}",
                        RiskLevel = "High",
                        DetectedAt = DateTime.Now,
                        IsBlocked = false,
                    });
                }
            }
        }
        catch { /* ipconfig not available or access denied */ }
    }

    private void CheckProxySettings()
    {
        try
        {
            using var internetSettings = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings");

            if (internetSettings == null) return;

            var proxyEnable = internetSettings.GetValue("ProxyEnable");
            var proxyServer = internetSettings.GetValue("ProxyServer") as string;
            var autoConfigUrl = internetSettings.GetValue("AutoConfigURL") as string;

            bool tampered = false;
            var description = "";

            // Unexpected proxy enabled
            if (proxyEnable is int enabled && enabled == 1 && !string.IsNullOrEmpty(proxyServer))
            {
                tampered = true;
                description = $"System proxy is set to: {proxyServer}. "
                              + "This could redirect your traffic through a malicious server.";
            }

            // Suspicious auto-config URL
            if (!string.IsNullOrEmpty(autoConfigUrl)
                && !autoConfigUrl.Contains("wpad", StringComparison.OrdinalIgnoreCase))
            {
                tampered = true;
                description += (description.Length > 0 ? " " : "")
                               + $"Proxy auto-config URL detected: {autoConfigUrl}";
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ProxyTampered = tampered;
                ProxyStatus = tampered
                    ? "WARNING: Proxy settings may have been tampered with!"
                    : "No proxy tampering detected";
            });

            if (tampered)
            {
                AddThreatOnDispatcher(new BrowserThreat
                {
                    BrowserName = "System",
                    ThreatType = "Hijack",
                    Url = proxyServer ?? autoConfigUrl ?? "Unknown",
                    Description = description,
                    RiskLevel = "Critical",
                    DetectedAt = DateTime.Now,
                    IsBlocked = false,
                });
            }
        }
        catch { /* Registry access denied */ }
    }

    private void ScanForScreenCapture()
    {
        var suspiciousScreenApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "obs64", "OBS Studio - Screen Recording" },
            { "obs32", "OBS Studio - Screen Recording" },
            { "CamtasiaStudio", "Camtasia - Screen Recording" },
            { "CamRecorder", "Camtasia Recorder" },
            { "ScreenClip", "Screen Clipping Tool" },
            { "snippingtool", "Snipping Tool" },
            { "ScreenSketch", "Snip & Sketch" },
            { "ShareX", "ShareX - Screenshot/Recording" },
            { "Lightshot", "Lightshot Screenshot" },
            { "Greenshot", "Greenshot Screenshot" },
            { "Bandicam", "Bandicam Screen Recorder" },
            { "bdcam", "Bandicam Recorder" },
            { "XSplit", "XSplit Broadcaster" },
            { "Action", "Mirillis Action Screen Recorder" },
            { "oCam", "oCam Screen Recorder" },
            { "screenrec", "ScreenRec Recorder" },
            { "loom", "Loom Screen Recorder" },
            { "anydesk", "AnyDesk Remote - Screen Sharing" },
            { "TeamViewer", "TeamViewer - Remote Screen" },
            { "teamviewer_service", "TeamViewer Service" },
            { "UltraVnc", "UltraVNC - Remote Screen" },
            { "tvnserver", "TightVNC Server" },
            { "vncserver", "VNC Server" },
            { "rustdesk", "RustDesk - Remote Screen" },
        };

        var procs = Process.GetProcesses();
        var foundCapture = new List<string>();

        foreach (var proc in procs)
        {
            try
            {
                if (suspiciousScreenApps.TryGetValue(proc.ProcessName, out var appName))
                {
                    foundCapture.Add($"{appName} (PID: {proc.Id})");

                    AddThreatOnDispatcher(new BrowserThreat
                    {
                        BrowserName = "Screen Monitor",
                        ThreatType = "Screen Capture",
                        Url = proc.ProcessName,
                        Description = $"{appName} is currently running and can capture your screen content including browser passwords and sensitive data.",
                        RiskLevel = foundCapture.Count > 2 ? "High" : "Medium",
                        DetectedAt = DateTime.Now,
                        IsBlocked = false,
                    });
                }
            }
            catch { }
        }

        foreach (var p in procs) { try { p.Dispose(); } catch { } }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ScreenCaptureAttempts = foundCapture.Count;
            ScreenRecordingStatus = foundCapture.Count > 0
                ? $"{foundCapture.Count} screen capture app(s) detected: {string.Join(", ", foundCapture.Take(3))}"
                : "No screen capture applications detected";
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool IsTyposquatDomain(string host)
    {
        // Common targets for typosquatting
        string[] targets =
        [
            "google", "facebook", "amazon", "microsoft", "apple",
            "paypal", "netflix", "instagram", "twitter", "linkedin",
            "chase", "bankofamerica", "wellsfargo", "github",
        ];

        foreach (var target in targets)
        {
            if (host.Contains(target) && !host.EndsWith($".{target}.com")
                                      && !host.Equals($"{target}.com")
                                      && !host.Equals($"www.{target}.com")
                                      && !host.EndsWith($".{target}.net")
                                      && host.Length - target.Length is > 0 and < 6)
            {
                return true;
            }
        }

        return false;
    }

    private void AddThreatOnDispatcher(BrowserThreat threat)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // Avoid duplicates by URL + ThreatType combination
            var exists = DetectedThreats.Any(t =>
                t.Url == threat.Url && t.ThreatType == threat.ThreatType);

            if (!exists)
            {
                DetectedThreats.Add(threat);
                ThreatsBlocked++;
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
}

// ── BrowserThreat model ──────────────────────────────────────────────────
public partial class BrowserThreat : ObservableObject
{
    [ObservableProperty]
    private string _browserName = string.Empty;

    [ObservableProperty]
    private string _threatType = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _riskLevel = string.Empty;

    [ObservableProperty]
    private DateTime _detectedAt;

    [ObservableProperty]
    private bool _isBlocked;
}

// ── ActiveBrowserInfo model ──────────────────────────────────────────────
public class ActiveBrowserInfo
{
    public string ProcessName { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public int Pid { get; set; }
    public string ExePath { get; set; } = string.Empty;
    public string MemoryUsage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
