using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Models;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Antivirus.Monitoring;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Server;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IScanEngine _scanEngine;
    private readonly IFirewallManager _firewallManager;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly BehavioralMonitor? _behavioralMonitor;
    private readonly AppSettings? _appSettings;
    private readonly CloudflareService? _cloudflareService;
    private readonly DateTime _sessionStartTime = DateTime.Now;
    private DispatcherTimer? _uptimeTimer;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private int _filesScannedToday;

    [ObservableProperty]
    private int _threatsBlocked;

    [ObservableProperty]
    private int _firewallRulesActive;

    [ObservableProperty]
    private string _uptime = "0h 0m 0s";

    [ObservableProperty]
    private string _threatLevel = "Low";

    [ObservableProperty]
    private string _threatLevelColor = "#4CAF50";

    // Scan progress properties
    [ObservableProperty]
    private bool _isScanRunning;

    [ObservableProperty]
    private string _currentScanFile = string.Empty;

    [ObservableProperty]
    private double _scanProgressPercent;

    [ObservableProperty]
    private int _scanTotalFiles;

    [ObservableProperty]
    private int _scanScannedFiles;

    [ObservableProperty]
    private int _scanThreatsFound;

    [ObservableProperty]
    private string _scanEta = "Calculating...";

    [ObservableProperty]
    private string _scanStatusText = string.Empty;

    [ObservableProperty]
    private string _scanType = string.Empty;

    // Scan log
    [ObservableProperty]
    private string _scanLogText = string.Empty;

    [ObservableProperty]
    private bool _hasScanLog;

    public ObservableCollection<SecurityAlert> RecentAlerts { get; } = [];
    public ObservableCollection<string> ScanLogEntries { get; } = [];
    public ObservableCollection<BehavioralAlert> BehavioralAlerts { get; } = [];

    [ObservableProperty]
    private int _behavioralAlertCount;

    [ObservableProperty]
    private bool _isBehavioralMonitorRunning;

    // Server status properties
    [ObservableProperty]
    private bool _isServerOnline;

    [ObservableProperty]
    private int _onlineClients;

    [ObservableProperty]
    private string _serverStatusText = "Checking...";

    // Tunnel status properties (server mode only)
    [ObservableProperty]
    private bool _isTunnelConnected;

    [ObservableProperty]
    private string _connectionDiagnostic = string.Empty;

    // DDoS warning properties
    [ObservableProperty]
    private bool _isDdosDetected;

    [ObservableProperty]
    private string _ddosWarningText = string.Empty;

    // Admin broadcast message properties
    [ObservableProperty]
    private string _broadcastMessage = string.Empty;

    [ObservableProperty]
    private string _broadcastPriority = "info";

    [ObservableProperty]
    private bool _hasBroadcast;

    [ObservableProperty]
    private string _adminBroadcastInput = string.Empty;

    [ObservableProperty]
    private string _adminBroadcastPriority = "info";

    // Broadcast notification with countdown timer properties
    [ObservableProperty]
    private bool _hasBroadcastNotification;

    [ObservableProperty]
    private string _broadcastNotificationMessage = string.Empty;

    [ObservableProperty]
    private string _broadcastCountdownText = string.Empty;

    [ObservableProperty]
    private string _broadcastPostedAt = string.Empty;

    [ObservableProperty]
    private string _broadcastAdminName = string.Empty;

    private DateTime _broadcastExpiresAt;
    private DispatcherTimer? _broadcastCountdownTimer;
    private DispatcherTimer? _broadcastNotificationPollTimer;

    public bool IsServerMode => _appSettings?.IsServerMode == true;

    private DispatcherTimer? _serverStatusTimer;

    public DashboardViewModel(
        IScanEngine scanEngine,
        IFirewallManager firewallManager,
        ISecurityMonitor securityMonitor,
        BehavioralMonitor? behavioralMonitor = null,
        AppSettings? appSettings = null,
        CloudflareService? cloudflareService = null)
    {
        _scanEngine = scanEngine;
        _firewallManager = firewallManager;
        _securityMonitor = securityMonitor;
        _behavioralMonitor = behavioralMonitor;
        _appSettings = appSettings;
        _cloudflareService = cloudflareService;
        Title = "Dashboard";

        RefreshStats();
        StartUptimeTimer();

        // Wire up behavioral monitor alerts
        if (_behavioralMonitor != null)
        {
            IsBehavioralMonitorRunning = _behavioralMonitor.IsRunning;

            // Load existing alerts
            foreach (var alert in _behavioralMonitor.Alerts)
                BehavioralAlerts.Add(alert);
            BehavioralAlertCount = BehavioralAlerts.Count;

            _behavioralMonitor.AlertRaised += OnBehavioralAlertRaised;
        }

        // Start server status polling
        StartServerStatusTimer();

        // Start broadcast notification polling (every 30 seconds)
        StartBroadcastNotificationPolling();
    }

    private void StartUptimeTimer()
    {
        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _sessionStartTime;
            if (elapsed.TotalHours >= 1)
                Uptime = $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
            else if (elapsed.TotalMinutes >= 1)
                Uptime = $"{elapsed.Minutes}m {elapsed.Seconds}s";
            else
                Uptime = $"{elapsed.Seconds}s";
        };
        _uptimeTimer.Start();
    }

    private void StartServerStatusTimer()
    {
        // Check immediately on startup
        _ = CheckServerStatusAsync();

        _serverStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _serverStatusTimer.Tick += async (_, _) => await CheckServerStatusAsync();
        _serverStatusTimer.Start();
    }

    private async Task CheckServerStatusAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            // Determine the correct URL based on deployment mode
            string statusUrl;
            if (_appSettings?.IsServerMode == true)
            {
                // SERVER mode: check the local Kestrel server first - this is what Cloudflare tunnel connects to
                statusUrl = $"http://localhost:{_appSettings.Server.Port}/api/v1/server/status";
            }
            else if (_appSettings?.IsClientMode == true && !string.IsNullOrEmpty(_appSettings.Client.ServerUrl))
            {
                // CLIENT mode: check the configured remote server URL
                statusUrl = _appSettings.Client.ServerUrl.TrimEnd('/') + "/api/v1/server/status";
            }
            else
            {
                // Fallback to external domain
                statusUrl = "https://syntheticgamelabs.dpdns.org/api/v1/server/status";
            }

            var response = await http.GetStringAsync(statusUrl);
            var status = System.Text.Json.JsonSerializer.Deserialize<ServerStatusResponse>(response,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            IsServerOnline = true;
            OnlineClients = status?.OnlineClients ?? 0;

            // In server mode, also verify Cloudflare tunnel health
            if (_appSettings?.IsServerMode == true && _cloudflareService != null)
            {
                var tunnelStatus = _cloudflareService.GetTunnelStatus();
                IsTunnelConnected = tunnelStatus.IsTunnelHealthy;

                if (tunnelStatus.IsTunnelHealthy && tunnelStatus.IsEndToEndVerified)
                {
                    ServerStatusText = $"Local Server Online - Tunnel Connected (Verified) - {OnlineClients} client(s) connected";
                    ConnectionDiagnostic = string.Empty;
                }
                else if (tunnelStatus.IsTunnelHealthy && !tunnelStatus.IsEndToEndVerified)
                {
                    ServerStatusText = $"Local Server Online - Tunnel Verifying... - {OnlineClients} client(s) connected";
                    ConnectionDiagnostic = "Tunnel appears connected locally. End-to-end verification in progress...";
                }
                else if (!tunnelStatus.IsServiceRunning)
                {
                    ServerStatusText = $"Server Running - Tunnel NOT Connected (Service Stopped)";
                    ConnectionDiagnostic = "Cloudflare tunnel service not running. Go to Admin > Server tab and click 'Start Tunnel'.";
                }
                else if (!tunnelStatus.IsLocalPortReachable)
                {
                    ServerStatusText = $"Server Running - Tunnel Port Unreachable";
                    ConnectionDiagnostic = $"Cloudflare tunnel is running but cannot reach localhost:{tunnelStatus.Port}. The API server may have stopped listening.";
                }
                else
                {
                    // Service running, port reachable, but end-to-end test FAILED (522 scenario)
                    ServerStatusText = $"Server Running - Tunnel NOT Connecting (522)";
                    ConnectionDiagnostic = !string.IsNullOrEmpty(tunnelStatus.LastError)
                        ? $"Tunnel error: {tunnelStatus.LastError}. " +
                          "The tunnel service is running and port is open, but Cloudflare cannot reach this server. " +
                          "Verify: 1) Tunnel token is correct, 2) Cloudflare dashboard tunnel routes to http://localhost:5000, " +
                          "3) No firewall/antivirus is blocking cloudflared outbound connections."
                        : "Tunnel service is running but end-to-end connectivity test failed (HTTP 522). " +
                          "Check the Cloudflare Zero Trust dashboard to verify your tunnel routes to http://localhost:5000.";
                }
            }
            else
            {
                // Client mode or no CloudflareService available
                IsTunnelConnected = false;
                ConnectionDiagnostic = string.Empty;
                ServerStatusText = _appSettings?.IsServerMode == true
                    ? $"Local Server Online - {OnlineClients} client(s) connected"
                    : $"Server Online - {OnlineClients} client(s) connected";
            }

            // Clear DDoS warning when server is healthy
            IsDdosDetected = false;
            DdosWarningText = string.Empty;

            // Poll for admin broadcast messages
            await CheckBroadcastAsync(http);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            IsServerOnline = false;
            IsTunnelConnected = false;
            OnlineClients = 0;
            ServerStatusText = "Server Overloaded (503)";
            ConnectionDiagnostic = "Server returned 503 Service Unavailable. Possible overload or DDoS condition.";
            IsDdosDetected = true;
            DdosWarningText = "Server returned 503 - possible DDoS or overload condition.";
        }
        catch (TaskCanceledException)
        {
            IsServerOnline = false;
            IsTunnelConnected = false;
            OnlineClients = 0;
            ServerStatusText = "Server Timeout";
            ConnectionDiagnostic = string.Empty;
            IsDdosDetected = false;
            DdosWarningText = string.Empty;
        }
        catch
        {
            IsServerOnline = false;
            IsTunnelConnected = false;
            OnlineClients = 0;
            ServerStatusText = "Server Offline";
            ConnectionDiagnostic = string.Empty;
            IsDdosDetected = false;
            DdosWarningText = string.Empty;
        }
    }

    private void StartBroadcastNotificationPolling()
    {
        // Poll immediately
        _ = PollBroadcastNotificationAsync();

        _broadcastNotificationPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _broadcastNotificationPollTimer.Tick += async (_, _) => await PollBroadcastNotificationAsync();
        _broadcastNotificationPollTimer.Start();
    }

    private async Task PollBroadcastNotificationAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            string notificationUrl;
            if (_appSettings?.IsServerMode == true)
                notificationUrl = $"http://localhost:{_appSettings.Server.Port}/api/v1/admin/broadcast-notification";
            else if (_appSettings?.IsClientMode == true && !string.IsNullOrEmpty(_appSettings.Client.ServerUrl))
                notificationUrl = _appSettings.Client.ServerUrl.TrimEnd('/') + "/api/v1/admin/broadcast-notification";
            else
                notificationUrl = "https://syntheticgamelabs.dpdns.org/api/v1/admin/broadcast-notification";

            var response = await http.GetStringAsync(notificationUrl);
            var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("notification", out var notifEl) &&
                notifEl.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                var message = notifEl.GetProperty("message").GetString() ?? "";
                var adminUsername = notifEl.GetProperty("adminUsername").GetString() ?? "";
                var postedAt = notifEl.GetProperty("postedAt").GetDateTime();
                var expiresAt = notifEl.GetProperty("expiresAt").GetDateTime();

                if (DateTime.UtcNow < expiresAt && !string.IsNullOrWhiteSpace(message))
                {
                    BroadcastNotificationMessage = message;
                    BroadcastAdminName = adminUsername;
                    BroadcastPostedAt = postedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    _broadcastExpiresAt = expiresAt;
                    HasBroadcastNotification = true;

                    // Start or restart the countdown timer
                    StartBroadcastCountdownTimer();
                    return;
                }
            }

            // No active notification or expired
            ClearBroadcastNotificationDisplay();
        }
        catch
        {
            // Silently fail - don't clear existing notification on network error
        }
    }

    private void StartBroadcastCountdownTimer()
    {
        // Stop existing timer if any
        _broadcastCountdownTimer?.Stop();

        _broadcastCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _broadcastCountdownTimer.Tick += (_, _) =>
        {
            var remaining = _broadcastExpiresAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                ClearBroadcastNotificationDisplay();
                _broadcastCountdownTimer?.Stop();
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if (remaining.Days > 0) parts.Add($"{remaining.Days}d");
            if (remaining.Hours > 0) parts.Add($"{remaining.Hours}h");
            if (remaining.Minutes > 0) parts.Add($"{remaining.Minutes}m");
            parts.Add($"{remaining.Seconds}s");

            BroadcastCountdownText = string.Join(" ", parts);
        };
        _broadcastCountdownTimer.Start();

        // Set initial value immediately
        var initialRemaining = _broadcastExpiresAt - DateTime.UtcNow;
        if (initialRemaining > TimeSpan.Zero)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (initialRemaining.Days > 0) parts.Add($"{initialRemaining.Days}d");
            if (initialRemaining.Hours > 0) parts.Add($"{initialRemaining.Hours}h");
            if (initialRemaining.Minutes > 0) parts.Add($"{initialRemaining.Minutes}m");
            parts.Add($"{initialRemaining.Seconds}s");
            BroadcastCountdownText = string.Join(" ", parts);
        }
    }

    private void ClearBroadcastNotificationDisplay()
    {
        HasBroadcastNotification = false;
        BroadcastNotificationMessage = string.Empty;
        BroadcastCountdownText = string.Empty;
        BroadcastPostedAt = string.Empty;
        BroadcastAdminName = string.Empty;
        _broadcastCountdownTimer?.Stop();
    }

    private void RefreshStats()
    {
        try
        {
            var rules = _firewallManager.GetAllRules();
            FirewallRulesActive = rules.Count;
        }
        catch { }

        try
        {
            var alerts = _securityMonitor.GetActiveAlerts();
            RecentAlerts.Clear();
            foreach (var alert in alerts)
                RecentAlerts.Add(alert);
        }
        catch { }

        UpdateThreatLevel();
    }

    private void UpdateThreatLevel()
    {
        if (ThreatsBlocked > 10)
        {
            ThreatLevel = "Critical";
            ThreatLevelColor = "#F44336";
        }
        else if (ThreatsBlocked > 5)
        {
            ThreatLevel = "High";
            ThreatLevelColor = "#FF9800";
        }
        else if (ThreatsBlocked > 0)
        {
            ThreatLevel = "Medium";
            ThreatLevelColor = "#FFC107";
        }
        else
        {
            ThreatLevel = "Low";
            ThreatLevelColor = "#4CAF50";
        }
    }

    private void OnBehavioralAlertRaised(BehavioralAlert alert)
    {
        // Marshal to UI thread since behavioral monitor fires from background threads
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            BehavioralAlerts.Insert(0, alert);
            BehavioralAlertCount = BehavioralAlerts.Count;

            // Cap displayed alerts at 100 to avoid memory growth
            while (BehavioralAlerts.Count > 100)
                BehavioralAlerts.RemoveAt(BehavioralAlerts.Count - 1);

            // Escalate threat level for critical behavioral alerts
            if (alert.Severity == "Critical")
            {
                ThreatsBlocked++;
                UpdateThreatLevel();
            }
        });
    }

    [RelayCommand]
    private async Task QuickScanAsync()
    {
        if (IsScanRunning) return;

        _scanCts = new CancellationTokenSource();
        IsScanRunning = true;
        ScanType = "Quick Scan";
        ScanStatusText = "Starting quick scan...";
        ScanProgressPercent = 0;
        ScanScannedFiles = 0;
        ScanTotalFiles = 0;
        ScanThreatsFound = 0;
        ScanEta = "Calculating...";
        CurrentScanFile = "Discovering files...";
        ScanLogEntries.Clear();

        var scanStartTime = DateTime.Now;
        AddScanLogEntry("Quick Scan started");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Starting quick scan...");

        var progress = new Progress<Core.Events.ScanProgressEvent>(e =>
        {
            ScanTotalFiles = e.TotalFiles;
            ScanScannedFiles = e.ScannedFiles;
            ScanThreatsFound = e.ThreatsFound;
            ScanProgressPercent = e.ProgressPercent;
            CurrentScanFile = e.CurrentFile;
            FilesScannedToday = e.ScannedFiles;

            if (e.EstimatedTimeRemaining.HasValue)
            {
                var eta = e.EstimatedTimeRemaining.Value;
                ScanEta = eta.TotalMinutes >= 1
                    ? $"{(int)eta.TotalMinutes}m {eta.Seconds}s remaining"
                    : $"{eta.Seconds}s remaining";
            }

            ScanStatusText = $"Scanning: {e.ScannedFiles:N0} / {e.TotalFiles:N0} files ({e.ProgressPercent:F1}%)";
        });

        try
        {
            var session = await _scanEngine.QuickScanAsync(progress, _scanCts.Token);
            ThreatsBlocked += session.ThreatsFound;
            UpdateThreatLevel();

            var duration = DateTime.Now - scanStartTime;
            AddScanLogEntry($"Scan completed in {FormatDuration(duration)}");
            AddScanLogEntry($"Files scanned: {session.ScannedFiles:N0}");
            AddScanLogEntry($"Threats found: {session.ThreatsFound}");
            AddScanLogEntry($"Errors: {session.ErrorCount}");

            foreach (var threat in session.Threats)
                AddScanLogEntry($"  THREAT: {threat.ThreatName} - {threat.FilePath}");

            ScanStatusText = $"Quick scan complete - {session.ScannedFiles:N0} files, {session.ThreatsFound} threats";
            ScanEta = "Complete";
            CurrentScanFile = string.Empty;

            GenerateAndSaveScanLog("Quick", session, duration);

            AvatarViewModel.Instance.SetExpression(
                session.ThreatsFound > 0 ? AvatarExpression.FoundMalware : AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                session.ThreatsFound > 0
                    ? $"Quick scan done. Found {session.ThreatsFound} threat(s)!"
                    : "Quick scan complete. No threats found.");
        }
        catch (OperationCanceledException)
        {
            ScanStatusText = "Scan cancelled";
            ScanEta = "Cancelled";
            AddScanLogEntry("Scan was cancelled by user");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Scan was cancelled.");
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan error: {ex.Message}";
            AddScanLogEntry($"ERROR: {ex.Message}");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Scan encountered an error.");
        }
        finally
        {
            IsScanRunning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    private async Task FullScanAsync()
    {
        if (IsScanRunning) return;

        _scanCts = new CancellationTokenSource();
        IsScanRunning = true;
        ScanType = "Extended Scan";
        ScanStatusText = "Starting extended scan...";
        ScanProgressPercent = 0;
        ScanScannedFiles = 0;
        ScanTotalFiles = 0;
        ScanThreatsFound = 0;
        ScanEta = "Discovering files...";
        CurrentScanFile = "Enumerating directories...";
        ScanLogEntries.Clear();

        var scanStartTime = DateTime.Now;
        AddScanLogEntry("Extended Scan started");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Starting extended scan...");

        var progress = new Progress<Core.Events.ScanProgressEvent>(e =>
        {
            ScanTotalFiles = e.TotalFiles;
            ScanScannedFiles = e.ScannedFiles;
            ScanThreatsFound = e.ThreatsFound;
            ScanProgressPercent = e.ProgressPercent;
            CurrentScanFile = e.CurrentFile;
            FilesScannedToday = e.ScannedFiles;

            if (e.EstimatedTimeRemaining.HasValue)
            {
                var eta = e.EstimatedTimeRemaining.Value;
                if (eta.TotalHours >= 1)
                    ScanEta = $"{(int)eta.TotalHours}h {eta.Minutes}m remaining";
                else if (eta.TotalMinutes >= 1)
                    ScanEta = $"{(int)eta.TotalMinutes}m {eta.Seconds}s remaining";
                else
                    ScanEta = $"{eta.Seconds}s remaining";
            }
            else if (e.ScannedFiles == 0)
            {
                ScanEta = "Discovering files...";
            }

            ScanStatusText = $"Scanning: {e.ScannedFiles:N0} / {e.TotalFiles:N0} files ({e.ProgressPercent:F1}%)";
        });

        try
        {
            var session = await _scanEngine.ExtendedScanAsync(null, progress, _scanCts.Token);
            ThreatsBlocked += session.ThreatsFound;
            UpdateThreatLevel();

            var duration = DateTime.Now - scanStartTime;
            AddScanLogEntry($"Scan completed in {FormatDuration(duration)}");
            AddScanLogEntry($"Files scanned: {session.ScannedFiles:N0}");
            AddScanLogEntry($"Threats found: {session.ThreatsFound}");
            AddScanLogEntry($"Errors: {session.ErrorCount}");

            foreach (var threat in session.Threats)
                AddScanLogEntry($"  THREAT: {threat.ThreatName} - {threat.FilePath}");

            ScanStatusText = $"Extended scan complete - {session.ScannedFiles:N0} files, {session.ThreatsFound} threats in {FormatDuration(duration)}";
            ScanEta = "Complete";
            CurrentScanFile = string.Empty;

            GenerateAndSaveScanLog("Extended", session, duration);

            AvatarViewModel.Instance.SetExpression(
                session.ThreatsFound > 0 ? AvatarExpression.FoundMalware : AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                session.ThreatsFound > 0
                    ? $"Extended scan done. Found {session.ThreatsFound} threat(s)!"
                    : "Extended scan complete. No threats found.");
        }
        catch (OperationCanceledException)
        {
            ScanStatusText = "Scan cancelled";
            ScanEta = "Cancelled";
            AddScanLogEntry("Scan was cancelled by user");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Scan was cancelled.");
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan error: {ex.Message}";
            AddScanLogEntry($"ERROR: {ex.Message}");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Extended scan encountered an error.");
        }
        finally
        {
            IsScanRunning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
    }

    private void AddScanLogEntry(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ScanLogEntries.Add(timestamped);
    }

    private void GenerateAndSaveScanLog(string scanType, ScanSession session, TimeSpan duration)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine($"  SGL SYNTHETICAI - {scanType.ToUpper()} SCAN REPORT");
        sb.AppendLine($"  Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  Scan Type:     {scanType}");
        sb.AppendLine($"  Status:        {session.Status}");
        sb.AppendLine($"  Duration:      {FormatDuration(duration)}");
        sb.AppendLine($"  Files Scanned: {session.ScannedFiles:N0}");
        sb.AppendLine($"  Threats Found: {session.ThreatsFound}");
        sb.AppendLine($"  Errors:        {session.ErrorCount}");
        sb.AppendLine();

        if (session.Threats.Count > 0)
        {
            sb.AppendLine("── THREATS DETECTED ────────────────────────────");
            foreach (var threat in session.Threats)
            {
                sb.AppendLine($"  [{threat.Severity}] {threat.ThreatName}");
                sb.AppendLine($"    File:      {threat.FilePath}");
                sb.AppendLine($"    Detection: {threat.DetectionMethod}");
                sb.AppendLine($"    Score:     {threat.HeuristicScore:F1}");
                sb.AppendLine($"    Hash:      {threat.Sha256Hash ?? "N/A"}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No threats detected. System appears clean.");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine("  END OF SCAN REPORT");
        sb.AppendLine("═══════════════════════════════════════════════");

        ScanLogText = sb.ToString();
        HasScanLog = true;

        // Save to file
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "data", "scan_logs");
            Directory.CreateDirectory(logDir);
            var fileName = $"scan_{scanType.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            var logPath = Path.Combine(logDir, fileName);
            File.WriteAllText(logPath, sb.ToString());
        }
        catch { /* Non-critical - log save failure doesn't block UI */ }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private async Task CheckBroadcastAsync(HttpClient http)
    {
        try
        {
            string broadcastUrl;
            if (_appSettings?.IsServerMode == true)
                broadcastUrl = $"http://localhost:{_appSettings.Server.Port}/api/v1/broadcast";
            else if (_appSettings?.IsClientMode == true && !string.IsNullOrEmpty(_appSettings.Client.ServerUrl))
                broadcastUrl = _appSettings.Client.ServerUrl.TrimEnd('/') + "/api/v1/broadcast";
            else
                broadcastUrl = "https://syntheticgamelabs.dpdns.org/api/v1/broadcast";

            var response = await http.GetAsync(broadcastUrl);
            if (response.IsSuccessStatusCode)
            {
                var broadcast = await response.Content.ReadFromJsonAsync<BroadcastMessage>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (broadcast != null && !string.IsNullOrWhiteSpace(broadcast.Message))
                {
                    BroadcastMessage = broadcast.Message;
                    BroadcastPriority = broadcast.Priority;
                    HasBroadcast = true;
                    return;
                }
            }
        }
        catch { }

        BroadcastMessage = string.Empty;
        HasBroadcast = false;
    }

    [RelayCommand]
    private async Task SendBroadcastAsync()
    {
        if (string.IsNullOrWhiteSpace(AdminBroadcastInput)) return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://localhost:{_appSettings?.Server.Port ?? 5000}/api/v1/broadcast";
            var payload = new { message = AdminBroadcastInput.Trim(), priority = AdminBroadcastPriority };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                BroadcastMessage = AdminBroadcastInput.Trim();
                BroadcastPriority = AdminBroadcastPriority;
                HasBroadcast = true;
                AdminBroadcastInput = string.Empty;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task ClearBroadcastAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://localhost:{_appSettings?.Server.Port ?? 5000}/api/v1/broadcast";
            await http.DeleteAsync(url);
            BroadcastMessage = string.Empty;
            HasBroadcast = false;
        }
        catch { }
    }
}
