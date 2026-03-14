using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services.Auth;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Server;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _iconName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;

    public bool IsAdminOnly { get; set; }

    public string Description { get; }

    public ViewModelBase ViewModel { get; }

    private readonly string? _locKey;

    public NavigationItem(string label, string iconName, ViewModelBase viewModel, string description = "", bool adminOnly = false, string? locKey = null)
    {
        _locKey = locKey;
        Label = locKey != null ? Shared.Localization.LocalizationService.Instance[locKey] : label;
        IconName = iconName;
        ViewModel = viewModel;
        Description = description;
        IsAdminOnly = adminOnly;

        if (locKey != null)
        {
            Shared.Localization.LocalizationService.Instance.LanguageChanged += (_, _) =>
            {
                Label = Shared.Localization.LocalizationService.Instance[_locKey!];
            };
        }
    }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly ILlmService _llmService;
    private readonly IScanEngine _scanEngine;
    private readonly IFirewallManager _firewallManager;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly AppSettings _appSettings;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private bool _isProtectionActive = true;

    [ObservableProperty]
    private string _threatLevel = "Low";

    [ObservableProperty]
    private string _statusText = "All systems operational.";

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    // Toast notification
    [ObservableProperty]
    private string _notificationText = string.Empty;

    [ObservableProperty]
    private bool _isNotificationVisible;

    private System.Windows.Threading.DispatcherTimer? _notificationTimer;

    // User context
    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private bool _isAdminUser;

    // DDoS Alert overlay
    [ObservableProperty]
    private bool _isDdosAlertActive;

    [ObservableProperty]
    private string _ddosAlertMessage = string.Empty;

    // Server control (admin only)
    [ObservableProperty]
    private bool _isServerRunning;

    // Server-down state — tracks whether the backend server is unreachable
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundImageSource))]
    private bool _isServerDown;

    /// <summary>
    /// Returns the server-down background image path when the server is unreachable,
    /// or an empty string when the server is online.
    /// </summary>
    public string BackgroundImageSource => IsServerDown ? "Assets/SVD.png" : string.Empty;

    private SGL.JudgeDredd.Server.ApiServer.JudgeDreddApiServer? _apiServer;

    public event EventHandler? LogoutRequested;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    // Child ViewModels
    public DashboardViewModel DashboardVm { get; }
    public ScanViewModel ScanVm { get; }
    public FirewallViewModel FirewallVm { get; }
    public SecurityMonitorViewModel SecurityVm { get; }
    public SystemCleanerViewModel CleanerVm { get; }
    public ProcessesViewModel ProcessesVm { get; }
    public VpnViewModel VpnVm { get; }
    public ChatViewModel ChatVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public TrackerViewModel TrackerVm { get; }
    public DataLeakViewModel DataLeakVm { get; }
    public AiActivityViewModel AiActivityVm { get; }
    public EmailScanViewModel EmailScanVm { get; }
    public BrowserProtectionViewModel BrowserProtectionVm { get; }
    public WebcamMicViewModel WebcamMicVm { get; }
    public QuarantineVaultViewModel QuarantineVm { get; }
    public ScheduledScanViewModel ScheduledScanVm { get; }
    public FileIntegrityViewModel FileIntegrityVm { get; }
    public NetworkIntrusionViewModel NetworkIntrusionVm { get; }
    public YaraRuleViewModel YaraRuleVm { get; }
    public CloudUpdateViewModel CloudUpdateVm { get; }
    public HelpReadMeViewModel HelpVm { get; }
    public GitViewModel GitVm { get; }
    public TestSpeedViewModel TestSpeedVm { get; }
    public ShareViewModel ShareVm { get; }
    public FaqViewModel FaqVm { get; }
    public AvatarViewModel AvatarVm => AvatarViewModel.Instance;

    // Logo image source — swaps between Cover.png and Scan.png when scanning
    [ObservableProperty]
    private string _logoImageSource = "Assets/Cover.png";

    // V1.1.10 — New security modules
    public EndpointMonitorViewModel EndpointMonitorVm { get; }
    public NetworkMonitorViewModel NetworkMonitorVm { get; }
    public PersistenceScannerViewModel PersistenceScannerVm { get; }
    public BehavioralAnomalyViewModel BehavioralAnomalyVm { get; }
    public ThreatReputationViewModel ThreatReputationVm { get; }
    public IncidentReportViewModel IncidentReportVm { get; }

    // V1.1.11 — Composite ViewModels (tab consolidation)
    public NetworkSecurityViewModel NetworkSecurityVm { get; }
    public SystemMonitorViewModel SystemMonitorVm { get; }
    public ThreatIntelViewModel ThreatIntelVm { get; }
    public PrivacyViewModel PrivacyVm { get; }
    public ToolsViewModel ToolsVm { get; }

    private readonly HelpReadMeViewModel _helpVm;
    private System.Windows.Threading.DispatcherTimer? _protectionTimer;

    public MainViewModel(
        ILlmService llmService,
        IScanEngine scanEngine,
        IFirewallManager firewallManager,
        ISecurityMonitor securityMonitor,
        IKnowledgeBase knowledgeBase,
        AppSettings appSettings,
        CloudflareService? cloudflareService = null,
        ITtsService? ttsService = null)
    {
        _llmService = llmService;
        _scanEngine = scanEngine;
        _firewallManager = firewallManager;
        _securityMonitor = securityMonitor;
        _appSettings = appSettings;

        Title = "SyntheticAI - AI Security Suite";

        // Create child viewmodels
        DashboardVm = new DashboardViewModel(scanEngine, firewallManager, securityMonitor, appSettings: appSettings, cloudflareService: cloudflareService);
        ScanVm = new ScanViewModel(scanEngine, knowledgeBase, llmService);
        FirewallVm = new FirewallViewModel(firewallManager);
        SecurityVm = new SecurityMonitorViewModel(securityMonitor);
        CleanerVm = new SystemCleanerViewModel();
        ProcessesVm = new ProcessesViewModel();
        VpnVm = new VpnViewModel(appSettings);
        ChatVm = new ChatViewModel(llmService, scanEngine, securityMonitor, knowledgeBase, ttsService);
        SettingsVm = new SettingsViewModel(appSettings);
        TrackerVm = new TrackerViewModel();
        DataLeakVm = new DataLeakViewModel();
        AiActivityVm = new AiActivityViewModel();
        EmailScanVm = new EmailScanViewModel();
        BrowserProtectionVm = new BrowserProtectionViewModel();
        WebcamMicVm = new WebcamMicViewModel();
        QuarantineVm = new QuarantineVaultViewModel();
        ScheduledScanVm = new ScheduledScanViewModel(scanEngine);
        FileIntegrityVm = new FileIntegrityViewModel();
        NetworkIntrusionVm = new NetworkIntrusionViewModel();
        YaraRuleVm = new YaraRuleViewModel();
        CloudUpdateVm = new CloudUpdateViewModel(appSettings);
        _helpVm = new HelpReadMeViewModel();
        HelpVm = _helpVm;
        GitVm = new GitViewModel();
        TestSpeedVm = new TestSpeedViewModel();
        ShareVm = new ShareViewModel(appSettings);
        FaqVm = new FaqViewModel(appSettings);
        SettingsVm.FaqVm = FaqVm;

        // V1.1.10 — New security modules
        EndpointMonitorVm = new EndpointMonitorViewModel();
        NetworkMonitorVm = new NetworkMonitorViewModel();
        PersistenceScannerVm = new PersistenceScannerViewModel();
        BehavioralAnomalyVm = new BehavioralAnomalyViewModel();
        ThreatReputationVm = new ThreatReputationViewModel();
        IncidentReportVm = new IncidentReportViewModel();

        // V1.1.11 — Composite ViewModels (tab consolidation)
        NetworkSecurityVm = new NetworkSecurityViewModel(NetworkIntrusionVm, NetworkMonitorVm, DataLeakVm, BrowserProtectionVm);
        SystemMonitorVm = new SystemMonitorViewModel(EndpointMonitorVm, PersistenceScannerVm, BehavioralAnomalyVm);
        ThreatIntelVm = new ThreatIntelViewModel(ThreatReputationVm, IncidentReportVm, YaraRuleVm);
        PrivacyVm = new PrivacyViewModel(TrackerVm, WebcamMicVm, AiActivityVm);
        ToolsVm = new ToolsViewModel(GitVm, VpnVm, TestSpeedVm, CleanerVm);

        // Build navigation (base items visible to all users)
        NavigationItems.Add(new NavigationItem("Dashboard", "ViewDashboard", DashboardVm, "Security overview with threat level, scan stats, and recent alerts", locKey: "nav.dashboard"));
        NavigationItems.Add(new NavigationItem("Scan", "ShieldSearch", ScanVm, "Run Quick, Extended, or Custom antivirus scans on your system", locKey: "nav.scan"));
        NavigationItems.Add(new NavigationItem("Firewall", "Fire", FirewallVm, "Manage Windows Firewall rules, profiles, and quick-block actions", locKey: "nav.firewall"));
        NavigationItems.Add(new NavigationItem("Security", "ShieldLock", SecurityVm, "Real-time monitoring of processes, network, USB devices, and registry", locKey: "nav.security"));
        NavigationItems.Add(new NavigationItem("Network", "Globe", NetworkSecurityVm, "NIDS, connection monitor, data leak detection, and browser protection", locKey: "nav.network"));
        NavigationItems.Add(new NavigationItem("Privacy", "Eye", PrivacyVm, "Tracker detection, webcam/mic monitoring, and AI activity scanning", locKey: "nav.privacy"));
        NavigationItems.Add(new NavigationItem("Email Scan", "Mail", EmailScanVm, "Scan desktop email clients and browser webmail for phishing threats", locKey: "nav.email"));
        NavigationItems.Add(new NavigationItem("Quarantine", "Shield", QuarantineVm, "AES-256 encrypted vault for isolated threats with restore capability", locKey: "nav.quarantine"));
        NavigationItems.Add(new NavigationItem("Schedule", "Clock", ScheduledScanVm, "Schedule automatic scans daily, weekly, or at custom intervals", locKey: "nav.schedule"));
        NavigationItems.Add(new NavigationItem("FIM", "Page", FileIntegrityVm, "File Integrity Monitoring — detect unauthorized changes to system files", locKey: "nav.fim"));
        NavigationItems.Add(new NavigationItem("Monitor", "Monitor", SystemMonitorVm, "Endpoint monitoring, persistence scanning, and behavioral anomaly detection", locKey: "nav.monitor"));
        NavigationItems.Add(new NavigationItem("Threats", "Shield", ThreatIntelVm, "Threat reputation lookup, incident reports, and YARA rules", locKey: "nav.threats"));
        NavigationItems.Add(new NavigationItem("Updates", "Download", CloudUpdateVm, "Check for and download signature database updates from the server", locKey: "nav.updates"));
        NavigationItems.Add(new NavigationItem("Processes", "Monitor", ProcessesVm, "Task manager with CPU, memory, disk I/O, and security tagging", locKey: "nav.processes"));
        NavigationItems.Add(new NavigationItem("Tools", "Code", ToolsVm, "Git, VPN, speed test, and system cleaner utilities", locKey: "nav.tools"));
        NavigationItems.Add(new NavigationItem("Share", "Share", ShareVm, "Generate QR code to share SyntheticAI Mobile with friends", locKey: "nav.share"));
        // FAQ moved into Settings tab — no longer a standalone sidebar item
        NavigationItems.Add(new NavigationItem("Chat", "Chat", ChatVm, "AI assistant powered by SyntheticAI LLM with /scan, /security, /analyze", locKey: "nav.chat"));
        NavigationItems.Add(new NavigationItem("Settings", "Settings", SettingsVm, "Configure scanner, firewall, LLM, monitoring, and app preferences", locKey: "nav.settings"));
        NavigationItems.Add(new NavigationItem("Help", "Help", _helpVm, "Comprehensive documentation and guide for all features", locKey: "nav.help"));

        // Default to dashboard
        SelectedNavigationItem = NavigationItems[0];
        CurrentView = DashboardVm;

        // Watch for scan state changes to swap logo image
        DashboardVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.IsScanRunning))
            {
                LogoImageSource = DashboardVm.IsScanRunning
                    ? "Assets/Scan.png"
                    : "Assets/Cover.png";
            }
        };

        // Watch for server online/offline state to show SVD.png background
        DashboardVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.IsServerOnline))
            {
                IsServerDown = !DashboardVm.IsServerOnline;
            }
        };

        // Start real-time protection (if enabled in settings) and security monitors immediately
        try
        {
            if (_appSettings.Scanner.RealTimeProtection)
            {
                scanEngine.StartRealTimeProtection();
            }
            securityMonitor.StartAllMonitors();
        }
        catch { /* Non-critical - monitors run best-effort */ }

        // Set initial protection state
        IsProtectionActive = scanEngine.IsRealTimeProtectionActive;
        StatusText = IsProtectionActive
            ? "All systems operational."
            : "Real-time protection is disabled.";

        // Poll protection state every 3 seconds to stay in sync
        _protectionTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _protectionTimer.Tick += (_, _) =>
        {
            var rtpActive = _scanEngine.IsRealTimeProtectionActive;
            if (IsProtectionActive != rtpActive)
            {
                IsProtectionActive = rtpActive;
                StatusText = rtpActive
                    ? $"Welcome, {CurrentUserName}. All systems operational."
                    : "Real-time protection is disabled.";
            }
        };
        _protectionTimer.Start();
    }

    public void SetCurrentUser(UserAccount user)
    {
        CurrentUserName = user.Username;
        IsAdminUser = user.IsAdmin;
        StatusText = $"Welcome, {user.Username}. All systems operational.";

        // Filter admin-only navigation items for non-admin users
        foreach (var item in NavigationItems)
        {
            if (item.IsAdminOnly)
                item.IsVisible = IsAdminUser;
        }

        // If current view is admin-only and user is not admin, switch to dashboard
        if (SelectedNavigationItem?.IsAdminOnly == true && !IsAdminUser)
        {
            SelectedNavigationItem = NavigationItems.FirstOrDefault(n => !n.IsAdminOnly);
        }
    }

    /// <summary>
    /// Show a toast notification for the specified duration (default 3 seconds).
    /// </summary>
    public void ShowNotification(string message, int durationMs = 3000)
    {
        NotificationText = message;
        IsNotificationVisible = true;

        _notificationTimer?.Stop();
        _notificationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(durationMs)
        };
        _notificationTimer.Tick += (_, _) =>
        {
            IsNotificationVisible = false;
            _notificationTimer.Stop();
        };
        _notificationTimer.Start();
    }

    public void EnableAdminPanel(AdminPanelViewModel adminVm)
    {
        IsAdminUser = true;
        _helpVm.SetAdminMode(true);

        // Prevent duplicate admin tabs on re-login (MainViewModel is a singleton)
        if (NavigationItems.Any(n => n.Label == "Admin"))
            return;

        NavigationItems.Add(new NavigationItem("Admin", "Shield", adminVm, "User management, reports, diagnostics, and remote assist", adminOnly: true));
        NavigationItems.Add(new NavigationItem("UI Editor", "Palette", new UIEditorViewModel(), "Customize themes, colors, fonts, and UI appearance", adminOnly: true));
    }

    public void SetApiServer(SGL.JudgeDredd.Server.ApiServer.JudgeDreddApiServer apiServer)
    {
        _apiServer = apiServer;
        IsServerRunning = true; // If we have a reference, it was started at boot
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value is null) return;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        CurrentView = value.ViewModel;
    }

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        SelectedNavigationItem = item;
    }

    [RelayCommand]
    private void ShowWindow()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow is not null)
            {
                mainWindow.Show();
                mainWindow.WindowState = System.Windows.WindowState.Normal;
                mainWindow.Activate();
            }
        });
    }

    [RelayCommand]
    private void Logout()
    {
        _protectionTimer?.Stop();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Exit()
    {
        // Use a custom topmost Window instead of MessageBox.Show because
        // MessageBox can auto-dismiss when triggered from a tray context menu.
        var dialog = new System.Windows.Window
        {
            Title = "SGL SyntheticAI - Security Warning",
            Width = 480,
            Height = 260,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Topmost = true,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            WindowStyle = System.Windows.WindowStyle.SingleBorderWindow,
            ShowInTaskbar = true,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 30, 46)),
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "⚠ SECURITY WARNING",
            FontSize = 18,
            FontWeight = System.Windows.FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 200, 50)),
            Margin = new System.Windows.Thickness(0, 0, 0, 12),
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "If you exit SGL SyntheticAI, your system will be vulnerable and unprotected.\n\n" +
                   "Real-time protection, security monitoring, and AI threat detection will all be disabled.\n\n" +
                   "Are you sure you want to shut down?\n\n" +
                   "You can also shut down safely from the Settings tab.",
            FontSize = 13,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(220, 220, 230)),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new System.Windows.Thickness(0, 0, 0, 20),
        });

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };

        var noButton = new System.Windows.Controls.Button
        {
            Content = "No, Keep Protected",
            Padding = new System.Windows.Thickness(24, 10, 24, 10),
            Margin = new System.Windows.Thickness(0, 0, 12, 0),
            FontSize = 14,
            FontWeight = System.Windows.FontWeights.Bold,
            IsDefault = true,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 136, 229)),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 255, 255)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 136, 229)),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        noButton.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };

        var yesButton = new System.Windows.Controls.Button
        {
            Content = "Yes, Shut Down",
            Padding = new System.Windows.Thickness(24, 10, 24, 10),
            FontSize = 14,
            FontWeight = System.Windows.FontWeights.Bold,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(60, 60, 80)),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 80, 80)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 80, 80)),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        yesButton.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };

        buttonPanel.Children.Add(noButton);
        buttonPanel.Children.Add(yesButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;

        var confirmed = dialog.ShowDialog() == true;

        if (!confirmed)
            return;

        _protectionTimer?.Stop();
        _securityMonitor.StopAllMonitors();
        _scanEngine.StopRealTimeProtection();
        System.Windows.Application.Current?.Shutdown();
    }

    [RelayCommand]
    private async Task QuickScanAsync()
    {
        ShowWindow();
        SelectedNavigationItem = NavigationItems.FirstOrDefault(n => n.Label == "Scan");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Quick scan initiated from tray!");

        if (ScanVm.StartScanCommand.CanExecute(null))
        {
            ScanVm.SelectedScanType = ScanType.Quick;
            await ScanVm.StartScanCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void OpenChat()
    {
        ShowWindow();
        SelectedNavigationItem = NavigationItems.FirstOrDefault(n => n.Label == "Chat");
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ShowWindow();
        SelectedNavigationItem = NavigationItems.FirstOrDefault(n => n.Label == "Settings");
    }

    // ── Keyboard hotkey commands ──────────────────────────────────────

    [RelayCommand]
    private void NavigateByIndex(string indexStr)
    {
        if (int.TryParse(indexStr, out var index) && index >= 0 && index < NavigationItems.Count)
        {
            SelectedNavigationItem = NavigationItems[index];
        }
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        var settingsItem = NavigationItems.FirstOrDefault(n => n.Label == "Settings");
        if (settingsItem != null) SelectedNavigationItem = settingsItem;
    }

    [RelayCommand]
    private void NavigateToHelp()
    {
        var helpItem = NavigationItems.FirstOrDefault(n => n.Label == "Help");
        if (helpItem != null) SelectedNavigationItem = helpItem;
    }

    [RelayCommand]
    private async Task FullScanAsync()
    {
        SelectedNavigationItem = NavigationItems.FirstOrDefault(n => n.Label == "Scan");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Full scan initiated!");

        if (ScanVm.StartScanCommand.CanExecute(null))
        {
            ScanVm.SelectedScanType = ScanType.Full;
            await ScanVm.StartScanCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        if (ScanVm.StopScanCommand.CanExecute(null))
            ScanVm.StopScanCommand.Execute(null);
    }

    [RelayCommand]
    private void MinimizeToTray()
    {
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Hide();
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatVm.ClearChatCommand.Execute(null);
    }

    [RelayCommand]
    private async Task ToggleServerAsync()
    {
        if (_apiServer == null)
        {
            ShowNotification("Server is not available — this instance is running in Client mode. Set DeploymentMode to Server in settings.json and restart.", 5000);
            return;
        }

        try
        {
            if (IsServerRunning)
            {
                await _apiServer.StopAsync(CancellationToken.None);
                IsServerRunning = false;
                ShowNotification("Server has been stopped.", 3000);
                StatusText = "Server stopped manually by admin.";
            }
            else
            {
                await _apiServer.StartAsync(CancellationToken.None);
                IsServerRunning = _apiServer.IsRunning;
                if (IsServerRunning)
                {
                    ShowNotification("Server has been started successfully.", 3000);
                    StatusText = $"Welcome, {CurrentUserName}. Server is running.";
                }
                else
                {
                    var errorDetail = _apiServer.StartupError ?? "Unknown error — check logs/server-crash.log";
                    ShowNotification($"SERVER FAILED TO START: {errorDetail}", 8000);
                    StatusText = $"Server startup FAILED: {errorDetail}";
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification($"Server toggle failed: {ex.Message}", 5000);
            IsServerRunning = _apiServer.IsRunning;
        }
    }
}
