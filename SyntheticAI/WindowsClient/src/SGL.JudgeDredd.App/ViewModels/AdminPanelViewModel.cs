using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services.Auth;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Server;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class AdminPanelViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ILlmService _llmService;
    private readonly AppSettings? _appSettings;

    [ObservableProperty]
    private string _currentAdminName = string.Empty;

    [ObservableProperty]
    private string _currentIpAddress = "Detecting...";

    [ObservableProperty]
    private string _localIpAddress = "Detecting...";

    [ObservableProperty]
    private string _publicIpAddress = "Detecting...";

    // Users tab
    public ObservableCollection<UserAccount> Users { get; } = [];

    [ObservableProperty]
    private UserAccount? _selectedUser;

    // Reports tab
    [ObservableProperty]
    private string _reportText = "Click 'Generate Report' to create a system report.";

    // Diagnostics tab
    [ObservableProperty]
    private string _diagOsVersion = string.Empty;

    [ObservableProperty]
    private string _diagMachineName = string.Empty;

    [ObservableProperty]
    private int _diagProcessorCount;

    [ObservableProperty]
    private long _diagMemoryMb;

    [ObservableProperty]
    private string _diagUptime = string.Empty;

    [ObservableProperty]
    private string _diagModelLoaded = "Unknown";

    [ObservableProperty]
    private string _diagDbSize = "Unknown";

    [ObservableProperty]
    private int _diagThreatCount;

    [ObservableProperty]
    private int _diagUserCount;

    [ObservableProperty]
    private string _diagLog = string.Empty;

    // Remote Assist tab
    [ObservableProperty]
    private string _remoteClientIp = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isNotConnected = true;

    [ObservableProperty]
    private ImageSource? _remoteScreenshot;

    [ObservableProperty]
    private string _remoteStatusText = string.Empty;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    // Password Change
    [ObservableProperty]
    private string _changePasswordNewPassword = string.Empty;

    [ObservableProperty]
    private string _changePasswordConfirm = string.Empty;

    [ObservableProperty]
    private string _changePasswordStatus = string.Empty;

    [ObservableProperty]
    private bool _changePasswordHasError;

    [ObservableProperty]
    private bool _changePasswordSuccess;

    // Server Dashboard integration
    [ObservableProperty]
    private ServerDashboardViewModel? _serverDashboardVm;

    [ObservableProperty]
    private bool _hasServerTab;

    [ObservableProperty]
    private bool _isServerDashboardUnavailable;

    [ObservableProperty]
    private string _serverDashboardUnavailableMessage = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex;

    // ── Cloudflare Tunnel Properties ─────────────────────────────────
    [ObservableProperty]
    private bool _isCloudflaredRunning;

    [ObservableProperty]
    private string _cloudflaredStatus = "Checking...";

    [ObservableProperty]
    private string _tunnelName = "SGL";

    [ObservableProperty]
    private string _tunnelDomain = "syntheticgamelabs.dpdns.org";

    [ObservableProperty]
    private string _tunnelServiceUrl = "http://localhost:5000";

    [ObservableProperty]
    private int _tunnelConnections;

    [ObservableProperty]
    private string _serverListenAddress = "0.0.0.0:5000";

    [ObservableProperty]
    private string _dnsNameServer1 = "fred.ns.cloudflare.com";

    [ObservableProperty]
    private string _dnsNameServer2 = "magali.ns.cloudflare.com";

    [ObservableProperty]
    private string _publicIpv4 = "Detecting...";

    [ObservableProperty]
    private bool _isServerListening;

    [ObservableProperty]
    private string _serverBindStatus = "Checking...";

    // ── DDoS Detection Properties ────────────────────────────────────
    [ObservableProperty]
    private bool _isDdosDetected;

    [ObservableProperty]
    private string _ddosStatusText = "No attacks detected";

    [ObservableProperty]
    private double _requestsPerSecond;

    [ObservableProperty]
    private double _errorRatePercent;

    // ── Server Speed ─────────────────────────────────────────────────
    [ObservableProperty]
    private string _serverResponseTime = "N/A";

    // ── Tunnel Token Install ──────────────────────────────────────────
    [ObservableProperty]
    private string _tunnelTokenInput = string.Empty;

    [ObservableProperty]
    private string _tunnelInstallStatus = string.Empty;

    [ObservableProperty]
    private string _zoneId = "1ce7082c964871b5da3793c8587a3571";

    [ObservableProperty]
    private string _accountId = "876f3867525f05b18f33cebf6d592dd8";

    private DispatcherTimer? _cloudflareTimer;

    // ── Broadcast Notification with Countdown Properties ─────────────
    [ObservableProperty]
    private string _broadcastMessageText = string.Empty;

    [ObservableProperty]
    private int _broadcastDays;

    [ObservableProperty]
    private int _broadcastHours;

    [ObservableProperty]
    private int _broadcastMinutes;

    [ObservableProperty]
    private int _broadcastSeconds;

    [ObservableProperty]
    private string _broadcastNotificationStatus = string.Empty;

    [ObservableProperty]
    private bool _broadcastNotificationSent;

    [ObservableProperty]
    private bool _broadcastNotificationHasError;

    // ── Cloudflare API Login Properties ─────────────────────────────
    private CloudflareApiClient? _cfApiClient;

    [ObservableProperty]
    private string _cfEmail = string.Empty;

    [ObservableProperty]
    private string _cfApiKey = string.Empty;

    [ObservableProperty]
    private string _cfApiToken = string.Empty;

    [ObservableProperty]
    private bool _cfUseToken = true;

    [ObservableProperty]
    private bool _cfIsAuthenticated;

    [ObservableProperty]
    private string _cfAccountName = string.Empty;

    [ObservableProperty]
    private string _cfAccountEmail = string.Empty;

    [ObservableProperty]
    private string _cfLoginStatus = string.Empty;

    [ObservableProperty]
    private bool _cfLoginHasError;

    [ObservableProperty]
    private bool _cfIsLoggingIn;

    [ObservableProperty]
    private string _cfZoneInfo = string.Empty;

    [ObservableProperty]
    private string _cfTunnelInfo = string.Empty;

    [ObservableProperty]
    private string _cfOriginCaKey = string.Empty;

    [ObservableProperty]
    private string _cfTunnelConfigStatus = string.Empty;

    [ObservableProperty]
    private bool _cfTunnelConfigured;

    // ── Hardware Monitor Properties ──────────────────────────────────
    [ObservableProperty]
    private double _hwCpuUsage;

    [ObservableProperty]
    private double _hwRamUsage;

    [ObservableProperty]
    private double _hwGpuUsage;

    [ObservableProperty]
    private double _hwTemperature;

    [ObservableProperty]
    private int _hwFanSpeed;

    public ObservableCollection<string> HwDiskUsage { get; } = [];

    [ObservableProperty]
    private string _hwStatus = string.Empty;

    // ── Multi-LLM Control Properties ─────────────────────────────────
    public ObservableCollection<LlmSlotDisplayInfo> LlmSlots { get; } = [];

    public ObservableCollection<string> LlmCatalog { get; } = [];

    public ObservableCollection<string> LlmCatalogModels { get; } = [];

    /// <summary>Maps display name to model ID for mount requests.</summary>
    private readonly Dictionary<string, string> _catalogDisplayNameToId = new();

    [ObservableProperty]
    private string _llmStatus = string.Empty;

    // ── Website Editor Properties ────────────────────────────────────
    [ObservableProperty]
    private string _weHeroTitle = string.Empty;

    [ObservableProperty]
    private string _weHeroSubtitle = string.Empty;

    [ObservableProperty]
    private string _weAnnouncementBanner = string.Empty;

    [ObservableProperty]
    private bool _weAnnouncementVisible;

    [ObservableProperty]
    private string _weSupportEmail = string.Empty;

    [ObservableProperty]
    private string _weStatus = string.Empty;

    // ── Website Metrics Properties ───────────────────────────────────
    [ObservableProperty]
    private long _wmTotalVisitors;

    [ObservableProperty]
    private int _wmActiveVisitors;

    [ObservableProperty]
    private string _wmAvgDuration = "0:00";

    [ObservableProperty]
    private long _wmTotalDownloads;

    public ObservableCollection<KeyValuePair<string, long>> WmDownloadsByPlatform { get; } = [];

    public ObservableCollection<KeyValuePair<string, long>> WmTopCountries { get; } = [];

    [ObservableProperty]
    private string _wmStatus = string.Empty;

    partial void OnSelectedTabIndexChanged(int value)
    {
        // Server tab is index 11 (12th tab: Server&CF, CF Login, Users, Reports, Diagnostics, Remote Assist, Broadcast, Hardware, Multi-LLM, Web Metrics, Website Editor, Server)
        if (ServerDashboardVm != null)
        {
            ServerDashboardVm.IsActive = (value == 11);
        }
        else if (HasServerTab && value == 11)
        {
            // Server tab selected but dashboard VM is null — show unavailable message
            IsServerDashboardUnavailable = true;
            ServerDashboardUnavailableMessage = "Server Dashboard unavailable - ConnectedClientTracker not initialized. The server module may not have started correctly. Check diagnostic logs for details.";
        }
    }

    public void SetServerDashboard(ServerDashboardViewModel serverDashVm)
    {
        ServerDashboardVm = serverDashVm;
        HasServerTab = true;
        IsServerDashboardUnavailable = false;
        ServerDashboardUnavailableMessage = string.Empty;
    }

    /// <summary>
    /// Marks the Server tab as visible but shows an unavailable message instead of the dashboard.
    /// Called when server mode is active but the dashboard VM could not be created.
    /// </summary>
    public void SetServerDashboardUnavailable(string reason)
    {
        HasServerTab = true;
        ServerDashboardVm = null;
        IsServerDashboardUnavailable = true;
        ServerDashboardUnavailableMessage = reason;
    }

    public AdminPanelViewModel(AuthService authService, IKnowledgeBase knowledgeBase, ILlmService llmService, AppSettings? appSettings = null)
    {
        _authService = authService;
        _knowledgeBase = knowledgeBase;
        _llmService = llmService;
        _appSettings = appSettings;
        Title = "Admin Panel";

        if (_authService.CurrentUser != null)
            CurrentAdminName = _authService.CurrentUser.Username;

        // Load Cloudflare config from AppSettings if available
        if (_appSettings != null && _appSettings.IsServerMode)
        {
            ZoneId = _appSettings.Server.CloudflareZoneId;
            AccountId = _appSettings.Server.CloudflareAccountId;
            TunnelDomain = _appSettings.Server.PublicDomain;
            TunnelServiceUrl = $"http://localhost:{_appSettings.Server.Port}";
            ServerListenAddress = $"{_appSettings.Server.ListenAddress}:{_appSettings.Server.Port}";
            if (!string.IsNullOrEmpty(_appSettings.Server.TunnelToken))
                TunnelTokenInput = _appSettings.Server.TunnelToken;
        }

        _ = LoadUsersAsync();
        _ = LoadServerUsersAsync();
        _ = LoadLlmCatalogAsync();
        _ = LoadWebsiteContentAsync();
        RunDiagnosticsInternal();
        _ = DetectIpAddressesAsync();

        // Start periodic Cloudflare & server status checks every 15 seconds
        _cloudflareTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _cloudflareTimer.Tick += async (_, _) =>
        {
            await CheckCloudflaredStatusAsync();
            await CheckServerListeningAsync();
        };
        _cloudflareTimer.Start();

        // Initial check
        _ = CheckCloudflaredStatusAsync();
        _ = CheckServerListeningAsync();
        _ = DetectPublicIpv4Async();
    }

    [RelayCommand]
    private async Task RefreshUsersAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _authService.GetAllUsersAsync();

            // Resolve the local machine IP to display alongside each user
            string localIp = "Unknown";
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIp = ip.ToString();
                        break;
                    }
                }
            }
            catch { /* DNS resolution failed */ }

            Users.Clear();
            foreach (var user in users)
            {
                user.IpAddress = localIp;
                Users.Add(user);
            }
            DiagUserCount = Users.Count;
        }
        catch (Exception ex)
        {
            DiagLog += $"[ERROR] Failed to load users: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task DeactivateUserAsync()
    {
        if (SelectedUser == null) return;
        if (SelectedUser.IsAdmin)
        {
            DiagLog += $"[WARN] Cannot deactivate admin account: {SelectedUser.Username}\n";
            return;
        }

        var success = await _authService.DeactivateUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Deactivated user: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task ActivateUserAsync()
    {
        if (SelectedUser == null) return;

        var success = await _authService.ActivateUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Activated user: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null) return;
        if (SelectedUser.IsAdmin)
        {
            DiagLog += $"[WARN] Cannot delete admin account: {SelectedUser.Username}\n";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to permanently delete the user '{SelectedUser.Username}'?\n\nThis action cannot be undone.",
            "Delete User - Confirm",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var success = await _authService.DeleteUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Deleted user: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
        else
        {
            DiagLog += $"[ERROR] Failed to delete user: {SelectedUser.Username}\n";
        }
    }

    [RelayCommand]
    private async Task BanUserAsync()
    {
        if (SelectedUser == null) return;
        if (SelectedUser.IsAdmin)
        {
            DiagLog += $"[WARN] Cannot ban admin account: {SelectedUser.Username}\n";
            return;
        }

        var success = await _authService.BanUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Banned user: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
        else
        {
            DiagLog += $"[ERROR] Failed to ban user: {SelectedUser.Username}\n";
        }
    }

    [RelayCommand]
    private async Task UnbanUserAsync()
    {
        if (SelectedUser == null) return;

        var success = await _authService.UnbanUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Unbanned user: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task PromoteUserAsync()
    {
        if (SelectedUser == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Promote '{SelectedUser.Username}' to admin?\n\nThis will grant full admin privileges.",
            "Promote User - Confirm",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var success = await _authService.PromoteUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Promoted user to admin: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task DemoteUserAsync()
    {
        if (SelectedUser == null) return;

        var success = await _authService.DemoteUserAsync(SelectedUser.Id);
        if (success)
        {
            DiagLog += $"[INFO] Demoted user from admin: {SelectedUser.Username}\n";
            await LoadUsersAsync();
        }
    }

    // Direct Message to user
    [ObservableProperty]
    private string _dmMessageText = string.Empty;

    [ObservableProperty]
    private string _dmStatus = string.Empty;

    [RelayCommand]
    private async Task SendDmToUserAsync()
    {
        if (SelectedUser == null)
        {
            DmStatus = "Select a user first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DmMessageText))
        {
            DmStatus = "Message cannot be empty.";
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var payload = new
            {
                targetUsername = SelectedUser.Username,
                message = DmMessageText.Trim(),
                fromAdmin = CurrentAdminName
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync("http://localhost:5000/api/v1/admin/dm", content);

            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] DM sent to {SelectedUser.Username}: {DmMessageText.Trim()}\n";
                DmStatus = $"Message sent to {SelectedUser.Username}.";
                DmMessageText = string.Empty;
            }
            else
            {
                DmStatus = $"Failed to send DM (HTTP {(int)resp.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            DmStatus = $"DM error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        ChangePasswordHasError = false;
        ChangePasswordSuccess = false;
        ChangePasswordStatus = string.Empty;

        if (SelectedUser == null)
        {
            ChangePasswordHasError = true;
            ChangePasswordStatus = "Please select a user from the Users tab first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ChangePasswordNewPassword))
        {
            ChangePasswordHasError = true;
            ChangePasswordStatus = "New password cannot be empty.";
            return;
        }

        if (ChangePasswordNewPassword != ChangePasswordConfirm)
        {
            ChangePasswordHasError = true;
            ChangePasswordStatus = "Passwords do not match.";
            return;
        }

        var (success, message) = await _authService.ChangePasswordAsync(SelectedUser.Id, ChangePasswordNewPassword);

        if (success)
        {
            ChangePasswordSuccess = true;
            ChangePasswordStatus = message;
            ChangePasswordNewPassword = string.Empty;
            ChangePasswordConfirm = string.Empty;
            DiagLog += $"[INFO] Password changed for user: {SelectedUser.Username}\n";
        }
        else
        {
            ChangePasswordHasError = true;
            ChangePasswordStatus = message;
            DiagLog += $"[ERROR] Password change failed for {SelectedUser.Username}: {message}\n";
        }
    }

    // ── Server Users Management (remote users via HTTP API) ──────────
    public ObservableCollection<ServerUserInfo> ServerUsers { get; } = [];

    [ObservableProperty]
    private ServerUserInfo? _selectedServerUser;

    [ObservableProperty]
    private string _serverUserStatus = string.Empty;

    [ObservableProperty]
    private string _serverDmText = string.Empty;

    [RelayCommand]
    private async Task LoadServerUsersAsync()
    {
        try
        {
            ServerUserStatus = "Loading server users...";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var response = await http.GetAsync($"http://localhost:{port}/api/v1/admin/users");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<ServerUserInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                ServerUsers.Clear();
                if (users != null)
                {
                    foreach (var u in users)
                        ServerUsers.Add(u);
                }

                ServerUserStatus = $"Loaded {ServerUsers.Count} server users";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Loaded {ServerUsers.Count} server users.\n";
            }
            else
            {
                ServerUserStatus = $"Failed to load: HTTP {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            ServerUserStatus = $"Error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Server users load error: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task BanServerUserAsync()
    {
        if (SelectedServerUser == null) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var resp = await http.PostAsync($"http://localhost:{port}/api/v1/admin/users/{SelectedServerUser.Username}/ban", null);
            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] Banned server user: {SelectedServerUser.Username}\n";
                ServerUserStatus = $"Banned {SelectedServerUser.Username}";
                await LoadServerUsersAsync();
            }
            else
            {
                ServerUserStatus = $"Ban failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"Ban error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task UnbanServerUserAsync()
    {
        if (SelectedServerUser == null) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var resp = await http.PostAsync($"http://localhost:{port}/api/v1/admin/users/{SelectedServerUser.Username}/unban", null);
            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] Unbanned server user: {SelectedServerUser.Username}\n";
                ServerUserStatus = $"Unbanned {SelectedServerUser.Username}";
                await LoadServerUsersAsync();
            }
            else
            {
                ServerUserStatus = $"Unban failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"Unban error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task PromoteServerUserAsync()
    {
        if (SelectedServerUser == null) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var resp = await http.PostAsync($"http://localhost:{port}/api/v1/admin/users/{SelectedServerUser.Username}/promote", null);
            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] Promoted server user: {SelectedServerUser.Username}\n";
                ServerUserStatus = $"Promoted {SelectedServerUser.Username}";
                await LoadServerUsersAsync();
            }
            else
            {
                ServerUserStatus = $"Promote failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"Promote error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task DemoteServerUserAsync()
    {
        if (SelectedServerUser == null) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var resp = await http.PostAsync($"http://localhost:{port}/api/v1/admin/users/{SelectedServerUser.Username}/demote", null);
            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] Demoted server user: {SelectedServerUser.Username}\n";
                ServerUserStatus = $"Demoted {SelectedServerUser.Username}";
                await LoadServerUsersAsync();
            }
            else
            {
                ServerUserStatus = $"Demote failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"Demote error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task DeleteServerUserAsync()
    {
        if (SelectedServerUser == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Permanently delete server user '{SelectedServerUser.Username}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var resp = await http.DeleteAsync($"http://localhost:{port}/api/v1/admin/users/{SelectedServerUser.Username}");
            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] Deleted server user: {SelectedServerUser.Username}\n";
                ServerUserStatus = $"Deleted {SelectedServerUser.Username}";
                await LoadServerUsersAsync();
            }
            else
            {
                ServerUserStatus = $"Delete failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"Delete error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SendDmToServerUserAsync()
    {
        if (SelectedServerUser == null)
        {
            ServerUserStatus = "Select a server user first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ServerDmText))
        {
            ServerUserStatus = "Message cannot be empty.";
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var payload = new
            {
                targetUsername = SelectedServerUser.Username,
                message = ServerDmText.Trim(),
                fromAdmin = CurrentAdminName
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync($"http://localhost:{port}/api/v1/admin/dm", content);

            if (resp.IsSuccessStatusCode)
            {
                DiagLog += $"[INFO] DM sent to server user {SelectedServerUser.Username}\n";
                ServerUserStatus = $"DM sent to {SelectedServerUser.Username}";
                ServerDmText = string.Empty;
            }
            else
            {
                ServerUserStatus = $"DM failed: HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex) { ServerUserStatus = $"DM error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine("  SGL SYNTHETICAI - SYSTEM REPORT");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Admin: {CurrentAdminName}");
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine();

        // System Information
        sb.AppendLine("── SYSTEM INFORMATION ──────────────────────────");
        sb.AppendLine($"  OS:           {Environment.OSVersion}");
        sb.AppendLine($"  Machine:      {Environment.MachineName}");
        sb.AppendLine($"  Processors:   {Environment.ProcessorCount}");
        sb.AppendLine($"  64-bit OS:    {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"  CLR Version:  {Environment.Version}");
        sb.AppendLine($"  System Dir:   {Environment.SystemDirectory}");
        sb.AppendLine();

        // Memory
        var process = Process.GetCurrentProcess();
        sb.AppendLine("── MEMORY USAGE ───────────────────────────────");
        sb.AppendLine($"  Working Set:   {process.WorkingSet64 / 1024 / 1024} MB");
        sb.AppendLine($"  Private Mem:   {process.PrivateMemorySize64 / 1024 / 1024} MB");
        sb.AppendLine($"  GC Memory:     {GC.GetTotalMemory(false) / 1024 / 1024} MB");
        sb.AppendLine();

        // Application Status
        sb.AppendLine("── APPLICATION STATUS ─────────────────────────");
        sb.AppendLine($"  LLM Loaded:    {_llmService.IsModelLoaded}");
        sb.AppendLine($"  App Directory: {AppContext.BaseDirectory}");
        sb.AppendLine();

        // User Accounts
        sb.AppendLine("── USER ACCOUNTS ──────────────────────────────");
        var users = await _authService.GetAllUsersAsync();
        foreach (var user in users)
        {
            sb.AppendLine($"  [{(user.IsActive ? "ACTIVE" : "INACTIVE")}] {user.Username,-15} {user.Email,-30} Admin:{user.IsAdmin} Last:{user.LastLoginAt:yyyy-MM-dd HH:mm}");
        }
        sb.AppendLine($"  Total Users: {users.Count}");
        sb.AppendLine();

        // Running Processes (top 20 by memory)
        sb.AppendLine("── TOP PROCESSES (by Memory) ───────────────────");
        try
        {
            var processes = Process.GetProcesses()
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } })
                .Take(20);
            foreach (var p in processes)
            {
                try
                {
                    sb.AppendLine($"  PID:{p.Id,-6} {p.ProcessName,-30} Mem:{p.WorkingSet64 / 1024 / 1024,6} MB");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error reading processes: {ex.Message}");
        }
        sb.AppendLine();

        // Network Connections
        sb.AppendLine("── ACTIVE NETWORK LISTENERS ────────────────────");
        try
        {
            var psi = new ProcessStartInfo("netstat", "-an")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var netstat = Process.Start(psi);
            if (netstat != null)
            {
                var output = await netstat.StandardOutput.ReadToEndAsync();
                var lines = output.Split('\n')
                    .Where(l => l.Contains("LISTENING") || l.Contains("ESTABLISHED"))
                    .Take(20);
                foreach (var line in lines)
                {
                    sb.AppendLine($"  {line.Trim()}");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error: {ex.Message}");
        }
        sb.AppendLine();

        // Drives
        sb.AppendLine("── DRIVE INFORMATION ──────────────────────────");
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady)
                {
                    sb.AppendLine($"  {drive.Name} [{drive.DriveType}] {drive.DriveFormat} " +
                                $"Free:{drive.AvailableFreeSpace / 1024 / 1024 / 1024}GB / Total:{drive.TotalSize / 1024 / 1024 / 1024}GB");
                }
            }
            catch { }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine("  END OF REPORT");
        sb.AppendLine("═══════════════════════════════════════════════");

        ReportText = sb.ToString();
    }

    [RelayCommand]
    private void RunDiagnostics()
    {
        RunDiagnosticsInternal();
    }

    private void RunDiagnosticsInternal()
    {
        try
        {
            DiagOsVersion = Environment.OSVersion.ToString();
            DiagMachineName = Environment.MachineName;
            DiagProcessorCount = Environment.ProcessorCount;

            var process = Process.GetCurrentProcess();
            DiagMemoryMb = process.WorkingSet64 / 1024 / 1024;

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            DiagUptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";

            DiagModelLoaded = _llmService.IsModelLoaded ? "Yes" : "No";

            var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "sgl-sai-knowledge.db");
            if (File.Exists(dbPath))
            {
                var fi = new FileInfo(dbPath);
                DiagDbSize = $"{fi.Length / 1024} KB";
            }
            else
            {
                DiagDbSize = "Not found";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Diagnostics started");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] OS: {DiagOsVersion}");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Machine: {DiagMachineName}");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] CPU Cores: {DiagProcessorCount}");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] App Memory: {DiagMemoryMb} MB");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Uptime: {DiagUptime}");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] LLM Model: {DiagModelLoaded}");
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Database: {DiagDbSize}");

            // Check for known remote access tools
            var remoteTools = new[] { "TeamViewer", "AnyDesk", "tvnserver", "winvnc" };
            var runningProcesses = Process.GetProcesses().Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in remoteTools)
            {
                if (runningProcesses.Contains(tool))
                    sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] WARNING: Remote access tool detected: {tool}");
            }

            // Check suspicious processes
            var suspicious = new[] { "mimikatz", "cobaltstrike", "meterpreter", "ncat", "nc" };
            foreach (var sus in suspicious)
            {
                if (runningProcesses.Contains(sus))
                    sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] CRITICAL: Suspicious process detected: {sus}");
            }

            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] Diagnostics completed OK");
            DiagLog = sb.ToString();
        }
        catch (Exception ex)
        {
            DiagLog += $"[ERROR] Diagnostics failed: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task ConnectRemoteAsync()
    {
        if (string.IsNullOrWhiteSpace(RemoteClientIp))
        {
            ConnectionStatus = "Please enter a client IP address";
            return;
        }

        ConnectionStatus = $"Connecting to {RemoteClientIp}...";

        try
        {
            // Attempt to connect via TCP to the remote assist service
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(RemoteClientIp, 9847);
            var completed = await Task.WhenAny(connectTask, Task.Delay(5000));

            if (completed == connectTask && client.Connected)
            {
                IsConnected = true;
                IsNotConnected = false;
                ConnectionStatus = $"Connected to {RemoteClientIp}";
                RemoteStatusText = $"Live - {RemoteClientIp} - {DateTime.Now:HH:mm:ss}";

                // Request initial screenshot
                await RequestScreenshotAsync(client);
            }
            else
            {
                ConnectionStatus = $"Connection timeout - client at {RemoteClientIp} not responding. " +
                                 "Ensure the client has remote assist enabled.";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Connection failed: {ex.Message}";
        }
    }

    private async Task RequestScreenshotAsync(System.Net.Sockets.TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var request = System.Text.Encoding.UTF8.GetBytes("SCREENSHOT\n");
            await stream.WriteAsync(request);

            // Read response header (length prefix)
            var headerBuf = new byte[4];
            var read = await stream.ReadAsync(headerBuf);
            if (read == 4)
            {
                var length = BitConverter.ToInt32(headerBuf, 0);
                var imgBuf = new byte[length];
                var totalRead = 0;
                while (totalRead < length)
                {
                    var chunk = await stream.ReadAsync(imgBuf.AsMemory(totalRead, length - totalRead));
                    if (chunk == 0) break;
                    totalRead += chunk;
                }

                // Convert to BitmapImage on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var bitmap = new BitmapImage();
                    using var ms = new System.IO.MemoryStream(imgBuf);
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    RemoteScreenshot = bitmap;
                });

                RemoteStatusText = $"Live - {RemoteClientIp} - {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Screenshot failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshScreenAsync()
    {
        if (!IsConnected) return;

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(RemoteClientIp, 9847);
            await RequestScreenshotAsync(client);
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Refresh failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        IsNotConnected = true;
        RemoteScreenshot = null;
        ConnectionStatus = "Disconnected";
        RemoteStatusText = string.Empty;
    }

    private async Task DetectIpAddressesAsync()
    {
        try
        {
            // Get local LAN IP
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    LocalIpAddress = ip.ToString();
                    break;
                }
            }
        }
        catch { LocalIpAddress = "Unable to detect"; }

        try
        {
            // Get public IP
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            PublicIpAddress = await httpClient.GetStringAsync("https://api.ipify.org");
        }
        catch { PublicIpAddress = "Unable to detect"; }

        CurrentIpAddress = $"Local: {LocalIpAddress} | Public: {PublicIpAddress}";
    }

    [RelayCommand]
    private async Task RefreshIpAsync()
    {
        CurrentIpAddress = "Refreshing...";
        await DetectIpAddressesAsync();
    }

    // ── Broadcast Notification Commands ─────────────────────────────

    [RelayCommand]
    private async Task SendBroadcastNotificationAsync()
    {
        BroadcastNotificationHasError = false;
        BroadcastNotificationSent = false;
        BroadcastNotificationStatus = string.Empty;

        if (string.IsNullOrWhiteSpace(BroadcastMessageText))
        {
            BroadcastNotificationHasError = true;
            BroadcastNotificationStatus = "Please enter a message.";
            return;
        }

        if (BroadcastDays == 0 && BroadcastHours == 0 && BroadcastMinutes == 0 && BroadcastSeconds == 0)
        {
            BroadcastNotificationHasError = true;
            BroadcastNotificationStatus = "Please set a countdown duration (at least 1 second).";
            return;
        }

        try
        {
            BroadcastNotificationStatus = "Sending...";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/admin/broadcast-notification";

            var payload = new
            {
                message = BroadcastMessageText.Trim(),
                countdownDays = BroadcastDays,
                countdownHours = BroadcastHours,
                countdownMinutes = BroadcastMinutes,
                countdownSeconds = BroadcastSeconds
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                BroadcastNotificationSent = true;
                BroadcastNotificationStatus = "Broadcast notification sent successfully!";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Broadcast notification sent: \"{BroadcastMessageText.Trim()}\" " +
                           $"(countdown: {BroadcastDays}d {BroadcastHours}h {BroadcastMinutes}m {BroadcastSeconds}s)\n";

                // Clear inputs after successful send
                BroadcastMessageText = string.Empty;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                BroadcastNotificationHasError = true;
                BroadcastNotificationStatus = $"Server returned {(int)response.StatusCode}: {errorBody}";
            }
        }
        catch (Exception ex)
        {
            BroadcastNotificationHasError = true;
            BroadcastNotificationStatus = $"Failed to send: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Broadcast notification send error: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task ClearBroadcastNotificationAsync()
    {
        BroadcastNotificationHasError = false;
        BroadcastNotificationSent = false;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/admin/broadcast-notification";
            var response = await http.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                BroadcastNotificationSent = true;
                BroadcastNotificationStatus = "Broadcast notification cleared.";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Broadcast notification cleared.\n";
            }
            else
            {
                BroadcastNotificationHasError = true;
                BroadcastNotificationStatus = $"Failed to clear (HTTP {(int)response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            BroadcastNotificationHasError = true;
            BroadcastNotificationStatus = $"Failed to clear: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Broadcast notification clear error: {ex.Message}\n";
        }
    }

    // ── Cloudflare & Server Methods ──────────────────────────────────

    /// <summary>
    /// Check if the Windows service "Cloudflared" is running by querying the service control manager.
    /// </summary>
    public async Task CheckCloudflaredStatusAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("sc", "query Cloudflared")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    IsCloudflaredRunning = true;
                    CloudflaredStatus = "Running";
                }
                else if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    IsCloudflaredRunning = false;
                    CloudflaredStatus = "Stopped";
                }
                else if (output.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    IsCloudflaredRunning = false;
                    CloudflaredStatus = "Pending...";
                }
                else
                {
                    IsCloudflaredRunning = false;
                    CloudflaredStatus = "Not installed";
                }
            }
        }
        catch (Exception ex)
        {
            IsCloudflaredRunning = false;
            CloudflaredStatus = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Attempt a TCP connection to localhost:5000 to verify the server is listening.
    /// </summary>
    public async Task CheckServerListeningAsync()
    {
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync("127.0.0.1", 5000);
            var completed = await Task.WhenAny(connectTask, Task.Delay(3000));

            if (completed == connectTask && tcpClient.Connected)
            {
                IsServerListening = true;
                ServerBindStatus = "Listening on :5000";
            }
            else
            {
                IsServerListening = false;
                ServerBindStatus = "Not listening";
            }
        }
        catch
        {
            IsServerListening = false;
            ServerBindStatus = "Not listening";
        }
    }

    [RelayCommand]
    private async Task StartCloudflaredServiceAsync()
    {
        try
        {
            CloudflaredStatus = "Starting...";
            var psi = new ProcessStartInfo("cmd.exe", "/c sc start Cloudflared")
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflared service start requested (exit code: {proc.ExitCode}).\n";
            }
            // Refresh status after a brief delay
            await Task.Delay(3000);
            await CheckCloudflaredStatusAsync();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            CloudflaredStatus = "Requires admin";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Admin elevation denied by user.\n";
        }
        catch (Exception ex)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Failed to start Cloudflared: {ex.Message}\n";
            CloudflaredStatus = "Start failed";
        }
    }

    [RelayCommand]
    private async Task StopCloudflaredServiceAsync()
    {
        try
        {
            CloudflaredStatus = "Stopping...";
            var psi = new ProcessStartInfo("cmd.exe", "/c sc stop Cloudflared")
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflared service stop requested (exit code: {proc.ExitCode}).\n";
            }
            await Task.Delay(3000);
            await CheckCloudflaredStatusAsync();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            CloudflaredStatus = "Requires admin";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Admin elevation denied by user.\n";
        }
        catch (Exception ex)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Failed to stop Cloudflared: {ex.Message}\n";
            CloudflaredStatus = "Stop failed";
        }
    }

    [RelayCommand]
    private async Task RestartServerAsync()
    {
        DiagLog += $"[{DateTime.Now:HH:mm:ss}] Server restart initiated...\n";
        try
        {
            // Create firewall exceptions for both primary (5000) and legacy (7743) ports
            var psi = new ProcessStartInfo("cmd.exe",
                "/c netsh advfirewall firewall delete rule name=\"SyntheticAI Server\" >nul 2>&1 & " +
                "netsh advfirewall firewall add rule name=\"SyntheticAI Server\" dir=in action=allow protocol=tcp localport=5000 profile=any enable=yes & " +
                "netsh advfirewall firewall add rule name=\"SyntheticAI Server Legacy\" dir=in action=allow protocol=tcp localport=7743 profile=any enable=yes")
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Firewall rules created for ports 5000 and 7743.\n";
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Firewall rule: admin elevation denied.\n";
        }
        catch (Exception ex)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Firewall rule failed: {ex.Message}\n";
        }

        await Task.Delay(1000);
        await CheckServerListeningAsync();
        await CheckCloudflaredStatusAsync();
    }

    /// <summary>
    /// Measure the response time of the local server by hitting localhost:5000/api/v1/server/status.
    /// </summary>
    [RelayCommand]
    private async Task MeasureServerResponseTimeAsync()
    {
        try
        {
            ServerResponseTime = "Testing...";
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var sw = Stopwatch.StartNew();
            var response = await httpClient.GetAsync("http://localhost:5000/api/v1/server/status");
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                ServerResponseTime = $"{sw.ElapsedMilliseconds} ms";
            }
            else
            {
                ServerResponseTime = $"{sw.ElapsedMilliseconds} ms (HTTP {(int)response.StatusCode})";
            }

            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Server response time: {ServerResponseTime}\n";
        }
        catch (TaskCanceledException)
        {
            ServerResponseTime = "Timeout";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Server response time measurement timed out.\n";
        }
        catch (HttpRequestException ex)
        {
            ServerResponseTime = "Unreachable";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Server unreachable: {ex.Message}\n";
        }
        catch (Exception ex)
        {
            ServerResponseTime = "Error";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Speed test error: {ex.Message}\n";
        }
    }

    /// <summary>
    /// Detect the public IPv4 address via an external service.
    /// </summary>
    private async Task DetectPublicIpv4Async()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            PublicIpv4 = await httpClient.GetStringAsync("https://api.ipify.org");
        }
        catch
        {
            PublicIpv4 = "Unable to detect";
        }
    }

    /// <summary>
    /// Install the cloudflared tunnel service with the configured token.
    /// Uses cloudflared.exe service install &lt;token&gt; to create and start the Windows service.
    /// </summary>
    [RelayCommand]
    private async Task InstallTunnelServiceAsync()
    {
        if (string.IsNullOrWhiteSpace(TunnelTokenInput))
        {
            TunnelInstallStatus = "Please enter a tunnel token.";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Tunnel install aborted: empty token.\n";
            return;
        }

        TunnelInstallStatus = "Installing cloudflared service...";
        DiagLog += $"[{DateTime.Now:HH:mm:ss}] Installing cloudflared service with tunnel token...\n";

        try
        {
            // Find cloudflared.exe
            var cloudflaredPath = FindCloudflaredExe();
            if (cloudflaredPath == null)
            {
                TunnelInstallStatus = "cloudflared.exe not found. Please place it in the app directory or install Cloudflare WARP.";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] cloudflared.exe not found on this system.\n";
                return;
            }

            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Found cloudflared at: {cloudflaredPath}\n";

            // Uninstall existing service first (ignore errors)
            await RunCommandAsync(cloudflaredPath, "service uninstall");
            await Task.Delay(2000);

            // Install with new token
            var result = await RunCommandAsync(cloudflaredPath, $"service install {TunnelTokenInput}");
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Install output: {result}\n";

            if (result.Contains("installed", StringComparison.OrdinalIgnoreCase) || result.Contains("INF", StringComparison.OrdinalIgnoreCase))
            {
                TunnelInstallStatus = "Cloudflared service installed. Starting...";

                // Wait for service to start
                await Task.Delay(3000);
                await CheckCloudflaredStatusAsync();

                if (IsCloudflaredRunning)
                {
                    TunnelInstallStatus = "Cloudflared service installed and RUNNING.";
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflared service is now RUNNING.\n";
                }
                else
                {
                    // Try to start it manually
                    try
                    {
                        var startPsi = new ProcessStartInfo("sc", "start Cloudflared")
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                        };
                        using var startProc = Process.Start(startPsi);
                        if (startProc != null)
                        {
                            await startProc.WaitForExitAsync();
                        }
                        await Task.Delay(3000);
                        await CheckCloudflaredStatusAsync();

                        TunnelInstallStatus = IsCloudflaredRunning
                            ? "Cloudflared service installed and RUNNING."
                            : "Installed but service not yet running. Check logs.";
                    }
                    catch (Exception ex)
                    {
                        TunnelInstallStatus = $"Installed but start failed: {ex.Message}";
                    }
                }
            }
            else
            {
                TunnelInstallStatus = $"Install may have failed: {result}";
            }
        }
        catch (Exception ex)
        {
            TunnelInstallStatus = $"Install error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Tunnel install error: {ex.Message}\n";
        }
    }

    /// <summary>
    /// Test the end-to-end tunnel connectivity by hitting the public domain through Cloudflare.
    /// </summary>
    [RelayCommand]
    private async Task TestTunnelConnectivityAsync()
    {
        DiagLog += $"[{DateTime.Now:HH:mm:ss}] Testing tunnel connectivity to {TunnelDomain}...\n";
        try
        {
            // Test 1: Local port reachable?
            await CheckServerListeningAsync();
            if (!IsServerListening)
            {
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] FAIL: Local server NOT listening on port 5000.\n";
                return;
            }
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] OK: Local server is listening on port 5000.\n";

            // Test 2: Local HTTP response?
            using var localClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var localResponse = await localClient.GetAsync("http://localhost:5000/health");
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Local /health: HTTP {(int)localResponse.StatusCode}\n";

            // Test 3: Cloudflared service status?
            await CheckCloudflaredStatusAsync();
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflared service: {CloudflaredStatus}\n";

            // Test 4: Public domain reachable?
            using var publicClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            try
            {
                var publicResponse = await publicClient.GetAsync($"https://{TunnelDomain}/health");
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Public https://{TunnelDomain}/health: HTTP {(int)publicResponse.StatusCode}\n";

                if (publicResponse.IsSuccessStatusCode)
                {
                    var body = await publicResponse.Content.ReadAsStringAsync();
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] TUNNEL IS WORKING! Response: {body}\n";
                }
                else
                {
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] Public endpoint returned error status.\n";
                }
            }
            catch (HttpRequestException ex)
            {
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Public endpoint failed: {ex.Message}\n";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] This means the Cloudflare tunnel is NOT routing correctly.\n";
            }
        }
        catch (Exception ex)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Connectivity test error: {ex.Message}\n";
        }
    }

    private static string? FindCloudflaredExe()
    {
        var baseDir = AppContext.BaseDirectory;

        // Resolve project root by walking up from base directory
        string? projectRoot = null;
        try
        {
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Web Documentation")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Toolset")))
                {
                    projectRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }
        }
        catch { }

        var candidates = new List<string>
        {
            // Original "cloudflared.exe" paths
            Path.Combine(baseDir, "cloudflared.exe"),
            Path.Combine(baseDir, "Toolset", "cloudflared.exe"),

            // Actual binary name: cloudflared-windows-amd64.exe
            Path.Combine(baseDir, "cloudflared-windows-amd64.exe"),
            Path.Combine(baseDir, "Toolset", "cloudflared-windows-amd64.exe"),

            // Bundled in Web Documentation folder (relative to base dir)
            Path.Combine(baseDir, "Web Documentation", "Cloudflared", "cloudflared-windows-amd64.exe"),

            // Standard Windows install locations
            @"C:\Program Files\Cloudflare\cloudflared.exe",
            @"C:\Program Files (x86)\Cloudflare\cloudflared.exe",
            @"C:\Program Files\cloudflared\cloudflared.exe",
            @"C:\ProgramData\cloudflared\cloudflared.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "cloudflared", "cloudflared.exe"),
            @"C:\Windows\System32\cloudflared.exe",
        };

        // Add project root paths if resolved
        if (projectRoot != null)
        {
            candidates.Add(Path.Combine(projectRoot, "Web Documentation", "Cloudflared", "cloudflared-windows-amd64.exe"));
            candidates.Add(Path.Combine(projectRoot, "Toolset", "cloudflared-windows-amd64.exe"));
            candidates.Add(Path.Combine(projectRoot, "Toolset", "cloudflared.exe"));
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        // Try PATH lookup for both binary names
        foreach (var exeName in new[] { "cloudflared.exe", "cloudflared-windows-amd64.exe" })
        {
            try
            {
                var psi = new ProcessStartInfo("where", exeName)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        var first = output.Split('\n')[0].Trim();
                        if (File.Exists(first))
                            return first;
                    }
                }
            }
            catch { }
        }

        return null;
    }

    private static async Task<string> RunCommandAsync(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return "Failed to start process.";

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrEmpty(stderr) ? stdout.Trim() : $"{stdout}\n{stderr}".Trim();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ── Cloudflare API Login Commands ────────────────────────────────

    [RelayCommand]
    private async Task CfLoginAsync()
    {
        CfLoginHasError = false;
        CfLoginStatus = "Connecting to Cloudflare...";
        CfIsLoggingIn = true;
        CfTunnelConfigured = false;
        CfTunnelConfigStatus = "";

        try
        {
            _cfApiClient ??= new CloudflareApiClient();

            bool success;
            if (CfUseToken)
            {
                success = await _cfApiClient.LoginWithTokenAsync(CfApiToken);
            }
            else
            {
                success = await _cfApiClient.LoginWithApiKeyAsync(CfEmail, CfApiKey);
            }

            if (success)
            {
                CfIsAuthenticated = true;
                CfAccountName = _cfApiClient.AccountName ?? "Unknown";
                CfAccountEmail = _cfApiClient.AccountEmail ?? "";
                CfLoginStatus = $"Authenticated as {CfAccountName}";
                CfLoginHasError = false;

                if (!string.IsNullOrEmpty(_cfApiClient.ZoneId))
                    ZoneId = _cfApiClient.ZoneId;
                if (!string.IsNullOrEmpty(_cfApiClient.AccountId))
                    AccountId = _cfApiClient.AccountId;

                if (_cfApiClient.Zones.Count > 0)
                {
                    CfZoneInfo = string.Join("\n", _cfApiClient.Zones.Select(z =>
                        $"{z.Name} (Status: {z.Status}, ID: {z.Id})"));
                }
                else
                    CfZoneInfo = "No zones found in account.";

                if (_cfApiClient.Tunnels.Count > 0)
                {
                    CfTunnelInfo = string.Join("\n", _cfApiClient.Tunnels.Select(t =>
                        $"{t.Name} (Status: {t.Status}, ID: {t.Id[..8]}...)"));
                }
                else
                    CfTunnelInfo = "No tunnels found.";

                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflare API login successful ({_cfApiClient.AuthMethod}).\n";

                // Save API credentials immediately after successful auth
                // so TunnelAutoInstaller can auto-configure on future startups
                if (_appSettings != null && !CfUseToken && !string.IsNullOrEmpty(CfEmail) && !string.IsNullOrEmpty(CfApiKey))
                {
                    _appSettings.Server.CloudflareApiEmail = CfEmail;
                    _appSettings.Server.CloudflareApiKey = CfApiKey;
                    _appSettings.SaveToFile();
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] API credentials saved for auto-config on startup.\n";
                }

                // ── CRITICAL: Actually configure the tunnel ──────────────
                // Previously login was cosmetic-only: it fetched account info but
                // NEVER configured the tunnel ingress or DNS. This caused 522.
                CfLoginStatus = "Authenticated. Configuring tunnel...";
                var tunnelId = ExtractTunnelIdFromToken(TunnelTokenInput);
                if (!string.IsNullOrEmpty(tunnelId) && !string.IsNullOrEmpty(TunnelDomain))
                {
                    int port = 5000;
                    if (_appSettings?.IsServerMode == true)
                        port = _appSettings.Server.Port;

                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] Configuring tunnel {tunnelId[..8]}... for {TunnelDomain} -> localhost:{port}\n";

                    var result = await _cfApiClient.VerifyAndConfigureTunnelAsync(
                        tunnelId, TunnelDomain, port, ZoneId);

                    foreach (var step in result.Steps)
                        DiagLog += $"[{DateTime.Now:HH:mm:ss}]   {step}\n";

                    CfTunnelConfigured = result.Success;
                    CfTunnelConfigStatus = result.Summary;
                    TunnelConnections = result.ActiveConnections;

                    if (result.Success)
                    {
                        CfLoginStatus = $"Authenticated & Tunnel Configured ({CfAccountName})";
                        DiagLog += $"[{DateTime.Now:HH:mm:ss}] TUNNEL CONFIGURED SUCCESSFULLY.\n";
                    }
                    else
                    {
                        CfLoginStatus = $"Auth OK but tunnel config incomplete: {result.Summary}";
                        CfLoginHasError = true;
                        DiagLog += $"[{DateTime.Now:HH:mm:ss}] TUNNEL CONFIG ISSUE: {result.Summary}\n";
                    }
                }
                else
                {
                    CfTunnelConfigStatus = "No tunnel token configured. Set tunnel token first.";
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] No tunnel token - skipping tunnel configuration.\n";
                }
            }
            else
            {
                CfIsAuthenticated = false;
                CfLoginStatus = _cfApiClient.LastError ?? "Login failed.";
                CfLoginHasError = true;
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflare API login failed: {CfLoginStatus}\n";
            }
        }
        catch (Exception ex)
        {
            CfIsAuthenticated = false;
            CfLoginStatus = ex.Message;
            CfLoginHasError = true;
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflare API login error: {ex.Message}\n";
        }
        finally
        {
            CfIsLoggingIn = false;
        }
    }

    /// <summary>
    /// Extracts the tunnel ID from a base64-encoded Cloudflare tunnel token.
    /// Token format: base64({"a":"accountId","t":"tunnelId","s":"secret"})
    /// </summary>
    private static string? ExtractTunnelIdFromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token.Trim()));
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("t", out var tunnelIdProp))
                return tunnelIdProp.GetString();
        }
        catch { }
        return null;
    }

    [RelayCommand]
    private void CfLogout()
    {
        _cfApiClient?.Logout();
        CfIsAuthenticated = false;
        CfAccountName = string.Empty;
        CfAccountEmail = string.Empty;
        CfLoginStatus = "Logged out.";
        CfLoginHasError = false;
        CfZoneInfo = string.Empty;
        CfTunnelInfo = string.Empty;
        DiagLog += $"[{DateTime.Now:HH:mm:ss}] Cloudflare API logged out.\n";
    }

    // ── Hardware Monitor Commands ────────────────────────────────────

    [RelayCommand]
    private async Task RefreshHardwareAsync()
    {
        HwStatus = "Refreshing...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/admin/hardware";
            var response = await http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("cpuUsagePercent", out var cpu))
                    HwCpuUsage = cpu.GetDouble();
                if (root.TryGetProperty("ramUsagePercent", out var ram))
                    HwRamUsage = ram.GetDouble();
                if (root.TryGetProperty("gpuUsagePercent", out var gpu))
                    HwGpuUsage = gpu.GetDouble();
                if (root.TryGetProperty("cpuTemperatureC", out var temp))
                    HwTemperature = temp.GetDouble();
                if (root.TryGetProperty("fans", out var fans) && fans.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fanItem in fans.EnumerateArray())
                    {
                        if (fanItem.TryGetProperty("speedRpm", out var rpm))
                        {
                            var speed = rpm.GetInt32();
                            if (speed > 0) { HwFanSpeed = speed; break; }
                        }
                    }
                }

                HwDiskUsage.Clear();
                if (root.TryGetProperty("disks", out var disks) && disks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var disk in disks.EnumerateArray())
                    {
                        var name = disk.TryGetProperty("name", out var n) ? n.GetString() : "?";
                        var totalGb = disk.TryGetProperty("totalGB", out var t) ? t.GetDouble() : 0;
                        var freeGb = disk.TryGetProperty("freeGB", out var f) ? f.GetDouble() : 0;
                        var usedGb = totalGb - freeGb;
                        var pct = disk.TryGetProperty("usagePercent", out var p) ? p.GetDouble() : 0;
                        HwDiskUsage.Add($"{name}  {usedGb:F1} / {totalGb:F1} GB  ({pct:F1}%)");
                    }
                }

                HwStatus = $"Updated at {DateTime.Now:HH:mm:ss}";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Hardware snapshot refreshed.\n";
            }
            else
            {
                HwStatus = $"Server returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            HwStatus = $"Error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Hardware refresh error: {ex.Message}\n";
        }
    }

    // ── Multi-LLM Commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task LoadLlmCatalogAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/llm/catalog";
            var response = await http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                LlmCatalogModels.Clear();
                _catalogDisplayNameToId.Clear();

                // Server returns { "models": [...], "count": N }
                JsonElement modelsArray;
                if (root.ValueKind == JsonValueKind.Array)
                    modelsArray = root;
                else if (root.TryGetProperty("models", out var m) && m.ValueKind == JsonValueKind.Array)
                    modelsArray = m;
                else
                {
                    DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM catalog: unexpected response shape.\n";
                    return;
                }

                foreach (var item in modelsArray.EnumerateArray())
                {
                    var displayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                    var modelId = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        LlmCatalogModels.Add(displayName);
                        if (!string.IsNullOrEmpty(modelId))
                            _catalogDisplayNameToId[displayName] = modelId;
                    }
                }

                DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM catalog loaded ({LlmCatalogModels.Count} models).\n";
            }
        }
        catch (Exception ex)
        {
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM catalog load error: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task RefreshLlmSlotsAsync()
    {
        LlmStatus = "Refreshing...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/llm/slots";
            var response = await http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                LlmSlots.Clear();
                if (root.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array)
                {
                    foreach (var slot in slots.EnumerateArray())
                    {
                        LlmSlots.Add(new LlmSlotDisplayInfo
                        {
                            SlotIndex = slot.TryGetProperty("slotIndex", out var si) ? si.GetInt32() : 0,
                            ModelName = slot.TryGetProperty("modelName", out var mn) ? mn.GetString() ?? "" : "",
                            ModelId = slot.TryGetProperty("modelId", out var mi) ? mi.GetString() ?? "" : "",
                            IsMounted = slot.TryGetProperty("isMounted", out var im) && im.GetBoolean(),
                            Role = slot.TryGetProperty("role", out var r) ? r.GetString() ?? "Unassigned" : "Unassigned",
                            Ability = slot.TryGetProperty("ability", out var a) ? a.GetString() ?? "None" : "None",
                        });
                    }
                }

                LlmStatus = $"Updated at {DateTime.Now:HH:mm:ss}";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM slots refreshed ({LlmSlots.Count} slots).\n";
            }
            else
            {
                LlmStatus = $"Server returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LlmStatus = $"Error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM slots refresh error: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private async Task MountOrUnmountSlotAsync(int slotIndex)
    {
        var slot = LlmSlots.FirstOrDefault(s => s.SlotIndex == slotIndex);
        if (slot == null) return;

        if (slot.IsMounted)
        {
            await UnmountSlotAsync(slotIndex);
        }
        else
        {
            await MountSlotAsync(slotIndex);
        }
    }

    private async Task MountSlotAsync(int slotIndex)
    {
        var slot = LlmSlots.FirstOrDefault(s => s.SlotIndex == slotIndex);
        var selectedDisplayName = slot?.SelectedCatalogModel ?? string.Empty;
        if (string.IsNullOrEmpty(selectedDisplayName))
        {
            LlmStatus = $"Select a model for slot {slotIndex} before mounting.";
            return;
        }

        // Resolve display name to model ID
        var modelId = _catalogDisplayNameToId.TryGetValue(selectedDisplayName, out var id) ? id : selectedDisplayName;

        LlmStatus = $"Mounting slot {slotIndex} with {selectedDisplayName}...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/llm/slots/mount";

            // Convert role/ability strings to camelCase for the server's JsonStringEnumConverter
            var roleStr = ToCamelCase(slot?.Role ?? "Unassigned");
            var abilityStr = ToCamelCase(slot?.Ability ?? "None");

            var payload = new { slotIndex, modelId, role = roleStr, ability = abilityStr };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                LlmStatus = $"Slot {slotIndex} mounted.";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM slot {slotIndex} mounted with {selectedDisplayName}.\n";
                await RefreshLlmSlotsAsync();
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LlmStatus = $"Mount failed ({(int)response.StatusCode}): {errorBody}";
            }
        }
        catch (Exception ex)
        {
            LlmStatus = $"Mount error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM mount error: {ex.Message}\n";
        }
    }

    private async Task UnmountSlotAsync(int slotIndex)
    {
        LlmStatus = $"Unmounting slot {slotIndex}...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/llm/slots/unmount?slot={slotIndex}";

            var response = await http.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                LlmStatus = $"Slot {slotIndex} unmounted.";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM slot {slotIndex} unmounted.\n";
                await RefreshLlmSlotsAsync();
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LlmStatus = $"Unmount failed ({(int)response.StatusCode}): {errorBody}";
            }
        }
        catch (Exception ex)
        {
            LlmStatus = $"Unmount error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] LLM unmount error: {ex.Message}\n";
        }
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    // ── Website Editor Commands ────────────────────────────────────────

    [RelayCommand]
    private async Task LoadWebsiteContentAsync()
    {
        WeStatus = "Loading...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/website/content";
            var response = await http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("heroTitle", out var ht))
                    WeHeroTitle = ht.GetString() ?? string.Empty;
                if (root.TryGetProperty("heroSubtitle", out var hs))
                    WeHeroSubtitle = hs.GetString() ?? string.Empty;
                if (root.TryGetProperty("announcementBanner", out var ab))
                    WeAnnouncementBanner = ab.GetString() ?? string.Empty;
                if (root.TryGetProperty("announcementVisible", out var av))
                    WeAnnouncementVisible = av.GetBoolean();
                if (root.TryGetProperty("supportEmail", out var se))
                    WeSupportEmail = se.GetString() ?? string.Empty;

                WeStatus = $"Loaded at {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                WeStatus = $"Server returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            WeStatus = $"Load error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveWebsiteContentAsync()
    {
        WeStatus = "Saving...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/website/content";

            var payload = new
            {
                heroTitle = WeHeroTitle,
                heroSubtitle = WeHeroSubtitle,
                announcementBanner = WeAnnouncementBanner,
                announcementVisible = WeAnnouncementVisible,
                videoUrls = new Dictionary<string, string>(),
                supportEmail = WeSupportEmail,
                supportLinks = new List<object>()
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                WeStatus = $"Saved at {DateTime.Now:HH:mm:ss}";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Website content saved.\n";
            }
            else
            {
                WeStatus = $"Save failed: server returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            WeStatus = $"Save error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Website content save error: {ex.Message}\n";
        }
    }

    // ── Website Metrics Commands ─────────────────────────────────────

    [RelayCommand]
    private async Task RefreshWebMetricsAsync()
    {
        WmStatus = "Refreshing...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var port = _appSettings?.Server.Port ?? 5000;
            var url = $"http://localhost:{port}/api/v1/admin/metrics/website";
            var response = await http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("totalVisitors", out var tv))
                    WmTotalVisitors = tv.GetInt64();
                if (root.TryGetProperty("activeVisitors", out var av))
                    WmActiveVisitors = av.GetInt32();
                if (root.TryGetProperty("avgSessionDuration", out var asd))
                    WmAvgDuration = asd.GetString() ?? "0:00";
                if (root.TryGetProperty("totalDownloads", out var td))
                    WmTotalDownloads = td.GetInt64();

                WmDownloadsByPlatform.Clear();
                if (root.TryGetProperty("downloadsByPlatform", out var dbp) && dbp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in dbp.EnumerateObject())
                    {
                        WmDownloadsByPlatform.Add(new KeyValuePair<string, long>(prop.Name, prop.Value.GetInt64()));
                    }
                }

                WmTopCountries.Clear();
                if (root.TryGetProperty("topCountries", out var tc) && tc.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in tc.EnumerateObject())
                    {
                        WmTopCountries.Add(new KeyValuePair<string, long>(prop.Name, prop.Value.GetInt64()));
                    }
                }

                WmStatus = $"Updated at {DateTime.Now:HH:mm:ss}";
                DiagLog += $"[{DateTime.Now:HH:mm:ss}] Website metrics refreshed.\n";
            }
            else
            {
                WmStatus = $"Server returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            WmStatus = $"Error: {ex.Message}";
            DiagLog += $"[{DateTime.Now:HH:mm:ss}] Website metrics refresh error: {ex.Message}\n";
        }
    }
}

/// <summary>
/// Represents an LLM model slot in the Multi-LLM control panel.
/// </summary>
public partial class LlmSlotInfo : ObservableObject
{
    [ObservableProperty]
    private int _slotIndex;

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private bool _isMounted;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _ability = string.Empty;

    [ObservableProperty]
    private string _selectedCatalogModel = string.Empty;
}

/// <summary>
/// Represents a server-registered user (from UserAccountStore JSON).
/// </summary>
public class ServerUserInfo
{
    public string Username { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsBanned { get; set; }
    public bool IsAdmin { get; set; }
}

/// <summary>
/// UI-specific display model for LLM slots. Uses strings for Role/Ability
/// (matching WPF ComboBox string items) and adds SelectedCatalogModel for binding.
/// </summary>
public class LlmSlotDisplayInfo : ObservableObject
{
    public int SlotIndex { get; set; }
    public bool IsMounted { get; set; }
    public string? ModelName { get; set; }
    public string? ModelId { get; set; }

    private string _role = "Unassigned";
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private string _ability = "None";
    public string Ability
    {
        get => _ability;
        set => SetProperty(ref _ability, value);
    }

    private string _selectedCatalogModel = "";
    public string SelectedCatalogModel
    {
        get => _selectedCatalogModel;
        set => SetProperty(ref _selectedCatalogModel, value);
    }
}
