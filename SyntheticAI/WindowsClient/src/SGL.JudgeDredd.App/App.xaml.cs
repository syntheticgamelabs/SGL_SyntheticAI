using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.JudgeDredd.App.Services.Auth;
using SGL.JudgeDredd.App.ViewModels;
using SGL.JudgeDredd.Antivirus.Scanners;
using SGL.JudgeDredd.Antivirus.Monitoring;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Firewall.WindowsFirewall;
using SGL.JudgeDredd.KnowledgeBase;
using SGL.JudgeDredd.KnowledgeBase.Data;
using SGL.JudgeDredd.LLM;
using SGL.JudgeDredd.Security.Monitors;
using SGL.JudgeDredd.Server;
using SGL.JudgeDredd.Server.ApiServer;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.App.Services.ServerSync;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Helpers;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.App;

public partial class App : Application
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private IHost? _host;
    private Window? _currentWindow;
    private TaskbarIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Admin privilege check
        if (!AdminHelper.IsRunningAsAdmin())
        {
            AdminHelper.RestartAsAdmin();
            Shutdown();
            return;
        }

        // Initialize logging
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        SglLogger.Initialize(logDir);
        SglLogger.Information("SGL SyntheticAI starting up...");

        // Build DI host
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();

        // Initialize databases
        try
        {
            var knowledgeBase = _host.Services.GetRequiredService<KnowledgeBaseService>();
            await knowledgeBase.InitializeAsync();
            SglLogger.Information("Knowledge base initialized.");

            var authService = _host.Services.GetRequiredService<AuthService>();
            await authService.InitializeAsync();
            SglLogger.Information("Auth system initialized.");
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to initialize databases.", ex);
        }

        // Start hosted services (including API server in Server mode)
        try
        {
            await _host.StartAsync();
            SglLogger.Information("Hosted services started.");

            // Check if the API server actually started (it swallows exceptions internally)
            var appSettingsCheck = _host.Services.GetRequiredService<AppSettings>();
            if (appSettingsCheck.IsServerMode)
            {
                var apiServer = _host.Services.GetService<JudgeDreddApiServer>();
                if (apiServer != null && !apiServer.IsRunning)
                {
                    var errorMsg = apiServer.StartupError ?? "API server failed to bind to port. Check if another process is using port " + appSettingsCheck.Server.Port;
                    SglLogger.Error("API server did not start: " + errorMsg);
                    MessageBox.Show(
                        $"Server mode is enabled but the API server failed to start:\n\n{errorMsg}\n\nThe application will continue in degraded mode. Check logs/server-crash.log for details.",
                        "SyntheticAI - Server Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to start hosted services.", ex);
            MessageBox.Show(
                $"Failed to start services:\n\n{ex.Message}\n\nThe application may not function correctly.",
                "SyntheticAI - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Start behavioral monitoring (user-mode process & file watcher)
        try
        {
            var behavioralMonitor = _host.Services.GetRequiredService<BehavioralMonitor>();
            behavioralMonitor.Start();
            SglLogger.Information("Behavioral monitor started.");
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to start behavioral monitor.", ex);
        }

        // Global exception handler
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Check if EULA was previously accepted (first-run check)
        var eulaFlagPath = Path.Combine(AppContext.BaseDirectory, "data", ".eula_accepted");
        if (!File.Exists(eulaFlagPath))
        {
            ShowEulaScreen(eulaFlagPath);
        }
        else
        {
            // Try auto-login with saved credentials
            await TryAutoLoginOrShowLoginScreen();
        }
    }

    private void ShowEulaScreen(string eulaFlagPath)
    {
        CloseCurrentWindow();

        var eulaVm = new EulaViewModel();
        eulaVm.Accepted += async (_, _) =>
        {
            // Write EULA acceptance flag
            var dir = Path.GetDirectoryName(eulaFlagPath);
            if (dir != null) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(eulaFlagPath,
                $"EULA accepted on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            SglLogger.Information("EULA accepted by user.");
            await TryAutoLoginOrShowLoginScreen();
        };
        eulaVm.Declined += (_, _) =>
        {
            SglLogger.Information("EULA declined. Shutting down.");
            Shutdown();
        };

        var eulaWindow = new Window
        {
            Title = "SGL SyntheticAI - License Agreement",
            Width = 800,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            Content = new Views.EulaView { DataContext = eulaVm },
        };

        _currentWindow = eulaWindow;
        eulaWindow.Show();
    }

    private async Task TryAutoLoginOrShowLoginScreen()
    {
        var autoLoginService = new Services.Auth.AutoLoginService();
        if (autoLoginService.HasSavedCredentials())
        {
            var creds = autoLoginService.LoadCredentials();
            if (creds.HasValue)
            {
                var authService = _host!.Services.GetRequiredService<AuthService>();
                try
                {
                    var (success, _) = await authService.LoginAsync(creds.Value.username, creds.Value.password);
                    if (success)
                    {
                        SglLogger.Information("Auto-login successful for user: {Username}", creds.Value.username);
                        try
                        {
                            ShowMainWindow(authService.CurrentUser!);
                        }
                        catch (Exception ex)
                        {
                            SglLogger.Error("ShowMainWindow failed after auto-login.", ex);
                            try
                            {
                                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                                Directory.CreateDirectory(logDir);
                                File.AppendAllText(
                                    Path.Combine(logDir, "crash.log"),
                                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AutoLogin ShowMainWindow FAILED: {ex}\n");
                            }
                            catch { }
                            MessageBox.Show(
                                $"Failed to load main window:\n\n{ex.Message}",
                                "SyntheticAI - Startup Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            ShowLoginScreen();
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SglLogger.Error("Auto-login failed: " + ex.Message);
                }

                // Auto-login failed — clear stale credentials
                autoLoginService.ClearCredentials();
            }
        }

        ShowLoginScreen();
    }

    private void ShowLoginScreen()
    {
        CloseCurrentWindow();

        var authService = _host!.Services.GetRequiredService<AuthService>();
        var loginVm = new LoginViewModel(authService);

        loginVm.LoginSuccessful += (_, user) =>
        {
            SglLogger.Information("User logged in: {Username}", user.Username);
            // Dispatch to avoid re-entrancy from event handler within login window scope
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
            {
                try
                {
                    ShowMainWindow(user);
                }
                catch (Exception ex)
                {
                    SglLogger.Error("Failed to show main window after login.", ex);
                    try
                    {
                        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                        Directory.CreateDirectory(logDir);
                        File.AppendAllText(
                            Path.Combine(logDir, "crash.log"),
                            $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ShowMainWindow FAILED: {ex}\n");
                    }
                    catch { }
                    MessageBox.Show(
                        $"Failed to load main window:\n\n{ex.Message}\n\nCheck the logs folder for details.",
                        "SyntheticAI - Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        };

        loginVm.NavigateToRegister += (_, _) =>
        {
            ShowRegisterScreen();
        };

        var loginWindow = new Window
        {
            Title = "SGL SyntheticAI - Login",
            Width = 500,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            Content = new Views.LoginView { DataContext = loginVm },
        };

        // Allow dragging
        loginWindow.MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                loginWindow.DragMove();
        };

        _currentWindow = loginWindow;
        loginWindow.Show();
    }

    private void ShowRegisterScreen()
    {
        SglLogger.Information("Navigating to registration screen...");
        CloseCurrentWindow();

        var authService = _host!.Services.GetRequiredService<AuthService>();
        var registerVm = new RegisterViewModel(authService);

        registerVm.RegistrationSuccessful += (_, message) =>
        {
            SglLogger.Information("New user registered.");
            // Go back to login screen with success message
            ShowLoginScreen();
            // Attempt to show success message on the new login screen
            if (_currentWindow?.Content is Views.LoginView loginView &&
                loginView.DataContext is LoginViewModel loginVm)
            {
                loginVm.ShowSuccess(message);
            }
        };

        registerVm.NavigateToLogin += (_, _) =>
        {
            ShowLoginScreen();
        };

        var registerWindow = new Window
        {
            Title = "SGL SyntheticAI - Create Account",
            Width = 560,
            Height = 750,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            Content = new Views.RegisterView { DataContext = registerVm },
        };

        registerWindow.MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                registerWindow.DragMove();
        };

        _currentWindow = registerWindow;
        registerWindow.Show();
        registerWindow.Activate();
        SglLogger.Information("Registration screen displayed.");
    }

    private void ShowMainWindow(UserAccount user)
    {
        CloseCurrentWindow();

        var mainViewModel = _host!.Services.GetRequiredService<MainViewModel>();
        mainViewModel.SetCurrentUser(user);

        // If admin, add admin panel navigation
        if (user.IsAdmin)
        {
            var authService = _host.Services.GetRequiredService<AuthService>();
            var knowledgeBase = _host.Services.GetRequiredService<IKnowledgeBase>();
            var llmService = _host.Services.GetRequiredService<ILlmService>();
            var appSettings = _host.Services.GetRequiredService<AppSettings>();
            var adminVm = new AdminPanelViewModel(authService, knowledgeBase, llmService, appSettings);

            // If server mode, embed server dashboard inside admin panel
            if (appSettings.IsServerMode)
            {
                try
                {
                    var tracker = _host.Services.GetRequiredService<ConnectedClientTracker>();
                    var serverDashVm = new ServerDashboardViewModel(tracker, appSettings.Server.Port, appSettings.Server);
                    adminVm.SetServerDashboard(serverDashVm);
                }
                catch (InvalidOperationException)
                {
                    // ConnectedClientTracker not registered — show unavailable message instead of crashing
                    adminVm.SetServerDashboardUnavailable(
                        "Server Dashboard unavailable - ConnectedClientTracker not initialized. " +
                        "The server hosting service may not have started. Check logs for startup errors.");
                }
            }

            mainViewModel.EnableAdminPanel(adminVm);
        }

        // Pass API server reference to MainViewModel for server on/off toggle
        var appSettingsForServer = _host.Services.GetRequiredService<AppSettings>();
        if (appSettingsForServer.IsServerMode)
        {
            var apiServer = _host.Services.GetService<JudgeDreddApiServer>();
            if (apiServer != null)
                mainViewModel.SetApiServer(apiServer);
        }

        var appSettingsForWindow = _host.Services.GetRequiredService<AppSettings>();
        var mainWindow = new MainWindow(mainViewModel, appSettingsForWindow);
        MainWindow = mainWindow;
        _currentWindow = mainWindow;

        // Setup system tray icon
        SetupTrayIcon(mainViewModel);

        // Handle logout
        mainViewModel.LogoutRequested += (_, _) =>
        {
            SglLogger.Information("User logged out.");
            DisposeTrayIcon();
            ShowLoginScreen();
        };

        mainWindow.Show();
        SglLogger.Information("Main window displayed. Application ready.");

        // Hook server sync notifications to the main view model toast
        var appSettingsForSync = _host.Services.GetRequiredService<AppSettings>();
        if (appSettingsForSync.IsClientMode && appSettingsForSync.Client.AutoSync)
        {
            try
            {
                var syncService = _host.Services.GetRequiredService<ServerSyncService>();
                syncService.StatusChanged += (_, message) =>
                {
                    mainWindow.Dispatcher.Invoke(() =>
                    {
                        if (message.Contains("Connected"))
                            mainViewModel.ShowNotification("Connected to secure server.", 3000);
                        else if (message.Contains("lost"))
                            mainViewModel.ShowNotification("Server connection lost.", 3000);
                    });
                };

                // Wire scan engine threat events to server sync for threat learning
                var scanEngine = _host.Services.GetRequiredService<IScanEngine>();
                scanEngine.ThreatDetected += (_, args) =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await syncService.ReportThreatAsync(
                                args.Result.FileName,
                                args.Result.Sha256Hash,
                                args.Result.ThreatName ?? "Unknown",
                                args.Result.HeuristicScore);
                        }
                        catch { /* best effort */ }
                    });
                };
            }
            catch { /* Sync service may not be registered */ }
        }
        else if (appSettingsForSync.IsServerMode)
        {
            mainViewModel.ShowNotification("Server mode active. Listening for clients.", 3000);
        }

        // First-run LLM model check — prompt user if no models found
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        var firstRunLlmFlag = Path.Combine(dataDir, ".llm_prompted");
        if (!File.Exists(firstRunLlmFlag))
        {
            try
            {
                var llmRoot = FindProjectRoot();
                var llmDir = Path.Combine(llmRoot, "LLM");
                bool hasModels = Directory.Exists(llmDir) &&
                    Directory.EnumerateFiles(llmDir, "*.gguf", SearchOption.AllDirectories).Any();

                if (!hasModels)
                {
                    var result = System.Windows.MessageBox.Show(
                        "No local AI models (GGUF) were found on this system.\n\n" +
                        "SyntheticAI uses local LLM models for AI chat, threat analysis, and intelligent security.\n\n" +
                        "You can download models from Settings > AI Models tab.\n\n" +
                        "Would you like to go to Settings after the app loads to download a model?",
                        "SGL SyntheticAI — AI Model Setup",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        mainViewModel.ShowNotification("Go to Settings > AI Models to download an LLM model.", 8000);
                    }
                }

                File.WriteAllText(firstRunLlmFlag, DateTime.UtcNow.ToString("O"));
            }
            catch { /* Non-critical */ }
        }

        // Start LLM auto-load immediately (runs in background, doesn't block UI)
        _ = Task.Run(async () =>
        {
            try
            {
                var llm = _host!.Services.GetRequiredService<ILlmService>();
                if (!llm.IsModelLoaded)
                {
                    SglLogger.Information("Starting background LLM model load...");
                    await llm.LoadModelAsync();
                    SglLogger.Information("Background LLM model load completed.");
                }
            }
            catch (Exception ex)
            {
                SglLogger.Error("Background LLM model load failed: " + ex.Message);
            }
        });
    }

    private void SetupTrayIcon(MainViewModel mainViewModel)
    {
        DisposeTrayIcon();

        // Load ICO from the embedded app.ico or generate from Cover.png
        System.Drawing.Icon? icon = null;
        var icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            try { icon = new System.Drawing.Icon(icoPath); }
            catch { /* fallback */ }
        }

        if (icon == null)
        {
            var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Cover.png");
            if (!File.Exists(pngPath))
                pngPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Cover.png");

            if (File.Exists(pngPath))
            {
                try
                {
                    var bmp = new System.Drawing.Bitmap(pngPath);
                    var resized = new System.Drawing.Bitmap(bmp, 32, 32);
                    var hIcon = resized.GetHicon();
                    icon = System.Drawing.Icon.FromHandle(hIcon);
                    icon = (System.Drawing.Icon)icon.Clone(); // detach from handle so bitmaps can be freed
                    DestroyIcon(hIcon);
                    resized.Dispose();
                    bmp.Dispose();
                }
                catch { /* fallback */ }
            }
        }

        // Final fallback: use the application's embedded icon
        if (icon == null)
        {
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }
            catch { /* use default */ }
        }

        // Absolute last resort: use SystemIcons
        icon ??= System.Drawing.SystemIcons.Shield;

        _trayIcon = new TaskbarIcon
        {
            Icon = icon,
            ToolTipText = "SGL SyntheticAI - Security Suite",
        };

        _trayIcon.Visibility = System.Windows.Visibility.Visible;

        // Context menu with readable dark text on white background
        var contextMenu = new System.Windows.Controls.ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
        };

        var menuItemStyle = new System.Windows.Style(typeof(System.Windows.Controls.MenuItem));
        menuItemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.ForegroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))));
        menuItemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.FontSizeProperty, 13.0));
        contextMenu.Resources.Add(typeof(System.Windows.Controls.MenuItem), menuItemStyle);

        var showItem = new System.Windows.Controls.MenuItem { Header = "Open SyntheticAI" };
        showItem.Click += (_, _) => mainViewModel.ShowWindowCommand.Execute(null);
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var scanItem = new System.Windows.Controls.MenuItem { Header = "Quick Scan" };
        scanItem.Click += async (_, _) => await mainViewModel.QuickScanCommand.ExecuteAsync(null);
        contextMenu.Items.Add(scanItem);

        var securityItem = new System.Windows.Controls.MenuItem { Header = "Security Monitor" };
        securityItem.Click += (_, _) =>
        {
            mainViewModel.ShowWindowCommand.Execute(null);
            mainViewModel.SelectedNavigationItem = mainViewModel.NavigationItems.FirstOrDefault(n => n.Label == "Security");
        };
        contextMenu.Items.Add(securityItem);

        var processesItem = new System.Windows.Controls.MenuItem { Header = "Processes" };
        processesItem.Click += (_, _) =>
        {
            mainViewModel.ShowWindowCommand.Execute(null);
            mainViewModel.SelectedNavigationItem = mainViewModel.NavigationItems.FirstOrDefault(n => n.Label == "Processes");
        };
        contextMenu.Items.Add(processesItem);

        var vpnItem = new System.Windows.Controls.MenuItem { Header = "VPN" };
        vpnItem.Click += (_, _) =>
        {
            mainViewModel.ShowWindowCommand.Execute(null);
            mainViewModel.SelectedNavigationItem = mainViewModel.NavigationItems.FirstOrDefault(n => n.Label == "VPN");
        };
        contextMenu.Items.Add(vpnItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var chatItem = new System.Windows.Controls.MenuItem { Header = "AI Chat" };
        chatItem.Click += (_, _) => mainViewModel.OpenChatCommand.Execute(null);
        contextMenu.Items.Add(chatItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => mainViewModel.OpenSettingsCommand.Execute(null);
        contextMenu.Items.Add(settingsItem);

        var helpItem = new System.Windows.Controls.MenuItem { Header = "Help" };
        helpItem.Click += (_, _) =>
        {
            mainViewModel.ShowWindowCommand.Execute(null);
            mainViewModel.SelectedNavigationItem = mainViewModel.NavigationItems.FirstOrDefault(n => n.Label == "Help");
        };
        contextMenu.Items.Add(helpItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => mainViewModel.ExitCommand.Execute(null);
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => mainViewModel.ShowWindowCommand.Execute(null);
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    internal bool IsNavigating => _isNavigating;
    private bool _isNavigating;

    private void CloseCurrentWindow()
    {
        if (_currentWindow != null)
        {
            _isNavigating = true;
            _currentWindow.Hide();
            _currentWindow.Close();
            _currentWindow = null;
            _isNavigating = false;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configuration - load from JSON if exists, otherwise use defaults
        // Check data/ subfolder first (primary), then root (fallback for quick-deploy layouts)
        // Fall back to server_settings.json or client_settings.json if settings.json doesn't exist
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");
        if (!File.Exists(settingsPath))
        {
            // Try server_settings.json first (server mode fallback)
            var serverSettingsPath = Path.Combine(AppContext.BaseDirectory, "data", "server_settings.json");
            var clientSettingsPath = Path.Combine(AppContext.BaseDirectory, "data", "client_settings.json");
            var rootSettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

            if (File.Exists(serverSettingsPath))
            {
                // Copy server_settings.json as settings.json so it persists for next launch
                try { File.Copy(serverSettingsPath, settingsPath, false); }
                catch { settingsPath = serverSettingsPath; }
                SglLogger.Information("Settings loaded from server_settings fallback: {Path}", settingsPath);
            }
            else if (File.Exists(clientSettingsPath))
            {
                try { File.Copy(clientSettingsPath, settingsPath, false); }
                catch { settingsPath = clientSettingsPath; }
                SglLogger.Information("Settings loaded from client_settings fallback: {Path}", settingsPath);
            }
            else if (File.Exists(rootSettingsPath))
            {
                settingsPath = rootSettingsPath;
                SglLogger.Information("Settings loaded from root: {Path}", rootSettingsPath);
            }
        }
        var appSettings = AppSettings.LoadFromFile(settingsPath);
        SglLogger.Information("Settings loaded from: {Path} | DeploymentMode={Mode} IsServerMode={IsServer}",
            settingsPath, appSettings.DeploymentMode, appSettings.IsServerMode);
        services.AddSingleton(appSettings);

        // Auth
        services.AddSingleton<AuthDbContext>();
        services.AddSingleton<AuthService>();

        // Knowledge Base - use factory to resolve ambiguous constructors
        services.AddSingleton<KnowledgeDbContext>();
        services.AddSingleton<KnowledgeBaseService>(sp =>
            new KnowledgeBaseService(sp.GetRequiredService<KnowledgeDbContext>()));
        services.AddSingleton<IKnowledgeBase>(sp => sp.GetRequiredService<KnowledgeBaseService>());

        // LLM Engine - real LLamaSharp implementation
        // Search from model path in settings, then project root, then app base dir
        var modelDir = !string.IsNullOrEmpty(appSettings.Llm.ModelPath) && Directory.Exists(appSettings.Llm.ModelPath)
            ? appSettings.Llm.ModelPath
            : FindProjectRoot();
        SglLogger.Information("LLM model directory resolved to: {ModelDir}", modelDir);
        var modelManager = new LlmModelManager(modelDir, appSettings.Llm.GpuLayers, (uint)appSettings.Llm.ContextSize);
        modelManager.LoadProgressChanged += (_, msg) => SglLogger.Information("LLM: {Message}", msg);
        services.AddSingleton(modelManager);
        services.AddSingleton<ChatSessionManager>();
        services.AddSingleton<LlmService>();
        services.AddSingleton<ILlmService>(sp =>
        {
            var llmService = sp.GetRequiredService<LlmService>();
            var settings = sp.GetRequiredService<AppSettings>();
            llmService.Temperature = settings.Llm.Temperature;
            return llmService;
        });

        // TTS Engine - connects to vLLM-Omni / Qwen3-TTS server
        services.AddSingleton<ITtsService>(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            var ttsUrl = settings.Llm.TtsServerUrl ?? "http://localhost:8091";
            return new TtsService(ttsUrl);
        });

        // Antivirus - real scan engine with optional LLM for AI-powered threat analysis
        services.AddSingleton<IScanEngine>(sp =>
        {
            var engine = new ScanEngine(sp.GetRequiredService<IKnowledgeBase>(), sp.GetRequiredService<ILlmService>());
            var settings = sp.GetRequiredService<AppSettings>();
            if (settings.Scanner.ScanExclusions.Count > 0)
            {
                engine.SetExclusions(settings.Scanner.ScanExclusions);
            }
            return engine;
        });

        // Behavioral Monitor - user-mode process & file monitoring
        services.AddSingleton<BehavioralMonitor>();

        // Firewall - real Windows Firewall integration
        services.AddSingleton<IFirewallManager, WindowsFirewallManager>();

        // Security Monitor - real process/network/USB/registry monitoring
        services.AddSingleton<SecurityMonitorService>(sp =>
        {
            var monitor = new SecurityMonitorService();
            var settings = sp.GetRequiredService<AppSettings>();
            monitor.EnableRemoteAccessDetection = settings.Security.EnableRemoteAccessDetection;
            monitor.EnableBadUsbDetection = settings.Security.EnableBadUsbDetection;
            monitor.EnableAiDetection = settings.Security.EnableAiDetection;
            monitor.EnableRegistryWatcher = settings.Security.EnableRegistryWatcher;
            return monitor;
        });
        services.AddSingleton<ISecurityMonitor>(sp => sp.GetRequiredService<SecurityMonitorService>());
        services.AddSingleton<IProcessMonitor>(sp => sp.GetRequiredService<SecurityMonitorService>());

        // Avatar (singleton)
        services.AddSingleton(AvatarViewModel.Instance);

        // ViewModels - MainViewModel as singleton to prevent duplicate ChatViewModel/auto-loads
        services.AddSingleton<MainViewModel>(sp =>
        {
            var llm = sp.GetRequiredService<ILlmService>();
            var scan = sp.GetRequiredService<IScanEngine>();
            var fw = sp.GetRequiredService<IFirewallManager>();
            var sec = sp.GetRequiredService<ISecurityMonitor>();
            var kb = sp.GetRequiredService<IKnowledgeBase>();
            var settings = sp.GetRequiredService<AppSettings>();
            var cf = sp.GetService<CloudflareService>(); // null when not in server mode
            var tts = sp.GetService<ITtsService>(); // TTS service (optional - requires TTS server)
            return new MainViewModel(llm, scan, fw, sec, kb, settings, cf, tts);
        });
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ScanViewModel>();
        services.AddTransient<FirewallViewModel>();
        services.AddTransient<SecurityMonitorViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>(sp =>
            new SettingsViewModel(
                sp.GetRequiredService<AppSettings>(),
                sp.GetService<ILlmService>(),
                sp.GetService<LlmModelManager>()));

        // Server mode: start embedded API server + client tracking
        if (appSettings.IsServerMode)
        {
            services.AddSingleton<ConnectedClientTracker>();
            services.AddSingleton<ClientDataStore>();
            services.AddSingleton(sp => new CloudflareService(appSettings.Server.Port, appSettings.Server.PublicDomain));
            services.AddSingleton(sp => new JudgeDreddApiServer(
                sp,
                appSettings.Server.Port,
                appSettings.Server.ListenAddress,
                appSettings.Server.LegacyPort));
            services.AddHostedService(sp => sp.GetRequiredService<JudgeDreddApiServer>());
            SglLogger.Information("Server mode: API server will start on port {Port} (legacy {LegacyPort})",
                appSettings.Server.Port, appSettings.Server.LegacyPort);

            // Auto-install cloudflared tunnel if token is configured but service not installed
            if (!string.IsNullOrEmpty(appSettings.Server.TunnelToken))
            {
                services.AddHostedService(sp =>
                {
                    return new TunnelAutoInstaller(
                        sp.GetRequiredService<CloudflareService>(),
                        appSettings.Server.TunnelToken,
                        appSettings);
                });
            }
        }

        // Client mode: start background sync service
        if (appSettings.IsClientMode && appSettings.Client.AutoSync)
        {
            services.AddSingleton(sp => new ServerSyncService(sp.GetRequiredService<AppSettings>()));
            services.AddHostedService(sp => sp.GetRequiredService<ServerSyncService>());
            SglLogger.Information("Client mode: ServerSyncService will connect to {ServerUrl}", appSettings.Client.ServerUrl);
        }
    }

    /// <summary>
    /// Walks up from the binary output directory to find the project root
    /// (the folder containing the .slnx file, LLM model subfolder, or Qwen3 model).
    /// </summary>
    private static string FindProjectRoot()
    {
        // First check: walk up from binary output to find project root or LLM folder
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            // Check for Qwen3 model in LLM subfolder (V3.2+ layout)
            if (Directory.Exists(Path.Combine(dir, "LLM", "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF")))
                return dir;
            // Check for Qwen3 model directly (installed layout — model folder at app root)
            if (Directory.Exists(Path.Combine(dir, "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF")))
                return dir;
            // Check for any .gguf file in LLM subfolder (generic installed layout)
            var llmDir = Path.Combine(dir, "LLM");
            if (Directory.Exists(llmDir) && Directory.GetFiles(llmDir, "*.gguf", SearchOption.AllDirectories).Length > 0)
                return dir;
            // Legacy: GLM model folder
            if (Directory.Exists(Path.Combine(dir, "GLM-4.6V-Flash-GGUF")))
                return dir;
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // Second check: known project paths
        var knownPaths = new[]
        {
            @"c:\Users\Ty\Documents\AIANTIVIRRUS",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AIANTIVIRRUS"),
        };

        foreach (var path in knownPaths)
        {
            if (Directory.Exists(path))
            {
                // Check for Qwen3 model (V3.2+ preferred)
                if (Directory.Exists(Path.Combine(path, "LLM", "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF")))
                    return path;
                // Check for GLM model (legacy)
                if (Directory.Exists(Path.Combine(path, "GLM-4.6V-Flash-GGUF")))
                    return path;
            }
        }

        // Third check: Program Files installed location
        var programFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SGL SyntheticAI Server");
        if (Directory.Exists(Path.Combine(programFilesPath, "LLM")))
            return programFilesPath;

        var programFilesClientPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SGL SyntheticAI");
        if (Directory.Exists(Path.Combine(programFilesClientPath, "LLM")))
            return programFilesClientPath;

        return AppContext.BaseDirectory;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SglLogger.Error("Unhandled exception caught by dispatcher.", e.Exception);

        // Log full stack trace for diagnostics
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        try
        {
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "crash.log"),
                $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED: {e.Exception}\n");
        }
        catch { /* best effort */ }

        // Show the error to the user so issues are visible instead of silently swallowed
        try
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails have been logged to the logs folder.",
                "SyntheticAI - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch { /* MessageBox may fail if app is shutting down */ }

        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        SglLogger.Information("SGL SyntheticAI shutting down.");
        DisposeTrayIcon();

        // Stop behavioral monitor
        if (_host != null)
        {
            try
            {
                var behavioralMonitor = _host.Services.GetRequiredService<BehavioralMonitor>();
                behavioralMonitor.Dispose();
            }
            catch { }
        }

        // Stop hosted services gracefully (includes API server)
        if (_host != null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
            catch { }
        }

        SglLogger.CloseAndFlush();
        _host?.Dispose();
        base.OnExit(e);
    }
}
