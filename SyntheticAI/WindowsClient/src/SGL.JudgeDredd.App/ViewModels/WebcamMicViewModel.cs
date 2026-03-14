using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class DeviceAccessEntry : ObservableObject
{
    [ObservableProperty]
    private string _processName = string.Empty;

    [ObservableProperty]
    private string _deviceType = string.Empty; // "Webcam", "Microphone", "Both"

    [ObservableProperty]
    private string _accessStatus = string.Empty; // "Active", "Recent", "Blocked"

    [ObservableProperty]
    private DateTime _startTime;

    [ObservableProperty]
    private int _pid;

    [ObservableProperty]
    private bool _isAllowed;

    [ObservableProperty]
    private bool _isSuspicious;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _networkConnections = "None detected";

    [ObservableProperty]
    private string _dataDirection = "Unknown";
}

public partial class ConnectedDeviceInfo : ObservableObject
{
    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string _deviceType = string.Empty; // "Webcam", "Microphone", "Audio Device"

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _deviceId = string.Empty;

    [ObservableProperty]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    private string _pnpClass = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}

public partial class WebcamMicViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;
    private DispatcherTimer? _previewTimer;
    private IntPtr _captureHandle = IntPtr.Zero;
    private bool _isCapturing;
    private bool _isScanning; // Re-entrancy guard for timer-based scans

    // Win32 API for webcam capture via avicap32
    private const int WM_CAP_START = 0x400;
    private const int WM_CAP_DRIVER_CONNECT = WM_CAP_START + 10;
    private const int WM_CAP_DRIVER_DISCONNECT = WM_CAP_START + 11;
    private const int WM_CAP_EDIT_COPY = WM_CAP_START + 30;
    private const int WM_CAP_SET_PREVIEW = WM_CAP_START + 50;
    private const int WM_CAP_SET_PREVIEWRATE = WM_CAP_START + 52;
    private const int WM_CAP_GET_FRAME = WM_CAP_START + 60;
    private const int WM_CAP_GRAB_FRAME_NOSTOP = WM_CAP_START + 61;
    private const int WM_CAP_SET_CALLBACK_FRAME = WM_CAP_START + 5;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;

    // BITMAPINFOHEADER structure for DIB frame parsing
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    // VIDEOHDR structure for frame callback
    [StructLayout(LayoutKind.Sequential)]
    private struct VIDEOHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesUsed;
        public uint dwTimeCaptured;
        public IntPtr dwUser;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public IntPtr[] dwReserved;
    }

    // Delegate for frame callback
    private delegate void FrameCallbackDelegate(IntPtr hWnd, ref VIDEOHDR lpVHdr);

    [DllImport("avicap32.dll", EntryPoint = "capCreateCaptureWindowW", CharSet = CharSet.Unicode)]
    private static extern IntPtr capCreateCaptureWindow(
        string lpszWindowName, int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hwndParent, int nID);

    [DllImport("avicap32.dll", EntryPoint = "capGetVideoFormatSize")]
    private static extern int capGetVideoFormatSize(IntPtr hWnd);

    [DllImport("avicap32.dll", EntryPoint = "capGetVideoFormat")]
    private static extern bool capGetVideoFormat(IntPtr hWnd, ref BITMAPINFOHEADER lpFormat, int cbFormat);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [ObservableProperty]
    private int _webcamAccessCount;

    [ObservableProperty]
    private int _micAccessCount;

    [ObservableProperty]
    private int _blockedAccessCount;

    [ObservableProperty]
    private int _activeDevices;

    [ObservableProperty]
    private bool _webcamProtectionEnabled = true;

    [ObservableProperty]
    private bool _micProtectionEnabled = true;

    [ObservableProperty]
    private string _monitorStatus = "Initializing...";

    [ObservableProperty]
    private string _connectedDevicesList = "Scanning for devices...";

    [ObservableProperty]
    private int _connectedWebcams;

    [ObservableProperty]
    private int _connectedMicrophones;

    [ObservableProperty]
    private bool _isPreviewActive;

    [ObservableProperty]
    private ImageSource? _webcamPreviewImage;

    [ObservableProperty]
    private string _previewStatusText = "Webcam preview is off. Click 'Open Preview' to view the camera feed.";

    [ObservableProperty]
    private bool _isWebcamDisabled;

    public ObservableCollection<DeviceAccessEntry> DeviceAccesses { get; } = [];
    public ObservableCollection<ConnectedDeviceInfo> ConnectedDevices { get; } = [];
    public ObservableCollection<string> AccessLog { get; } = [];

    // ---------- Known legitimate applications ----------
    private static readonly HashSet<string> KnownLegitimateApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Zoom", "zoom", "CptHost",
        "Teams", "ms-teams", "MSTeams",
        "Skype", "SkypeApp", "SkypeBridge",
        "Discord",
        "WebexHost", "CiscoCollabHost", "atmgr", "ptoneclk",
        "Slack", "FaceTime",
        "GoToMeeting", "g2mcomm", "g2mlauncher",
        "BlueJeans", "RingCentral",
        "obs64", "obs32", "obs",
        "Streamlabs OBS", "Streamlabs",
        "XSplit", "XSplitBroadcaster", "XSplitGameSource",
        "ManyCam", "OBSProcess", "wirecast",
        "chrome", "msedge", "firefox", "opera", "brave",
        "iexplore", "Safari", "vivaldi",
        "svchost", "RuntimeBroker", "SystemSettings",
        "SecurityHealthSystray", "WindowsCamera",
        "ShellExperienceHost", "SearchHost",
        "Photoshop", "Lightroom",
        "PremierePro", "AfterFX",
        "Camtasia", "SnagitEditor", "Snagit32",
        "ShareX", "loom", "Bandicam", "bdcam",
        "WhatsApp", "Telegram", "Signal",
        "Messenger", "FacebookCall",
        "Line", "Viber", "WeChat",
        "AnyDesk", "TeamViewer", "TeamViewer_Service",
        "vnc", "vncviewer", "vncserver",
        "mstsc", "msrdc",
        "GameBar", "GameBarPresenceWriter",
        "NVIDIA Share", "nvcontainer", "ShadowPlay",
    };

    private static readonly HashSet<string> CaptureApiProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ffmpeg", "vlc", "mpc-hc64", "mpc-hc",
        "WindowsCamera", "webcam", "Camera",
        "audiodg", "SoundRecorder",
    };

    private const string WebcamConsentPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    private const string MicrophoneConsentPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public WebcamMicViewModel()
    {
        Title = "Webcam/Mic";
        Application.Current?.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            async () =>
            {
                try
                {
                    await Task.Run(() => ScanConnectedDevices());
                    await ScanDeviceAccessAsync();
                    EnsureTimerRunning();
                    MonitorStatus = $"Monitoring active - {ConnectedWebcams} webcam(s), {ConnectedMicrophones} mic(s) detected - {DeviceAccesses.Count} access(es)";
                }
                catch (Exception ex)
                {
                    MonitorStatus = $"Initialization error: {ex.Message}";
                    AddLogEntry($"ERROR: Initialization failed - {ex.Message}");
                }
            });
    }

    // --------------- Commands ---------------

    [RelayCommand]
    private async Task RefreshAsync()
    {
        MonitorStatus = "Scanning for device access...";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Checking webcam and microphone access...");

        await Task.Run(() => ScanConnectedDevices());
        await ScanDeviceAccessAsync();

        MonitorStatus = $"Scan complete - {ConnectedWebcams} webcam(s), {ConnectedMicrophones} mic(s) - {DeviceAccesses.Count} access(es) - {DateTime.Now:HH:mm:ss}";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
    }

    [RelayCommand]
    private async Task BlockAppAsync(DeviceAccessEntry? entry)
    {
        if (entry is null) return;

        try
        {
            var proc = Process.GetProcessById(entry.Pid);
            proc.Kill();
            proc.Dispose();

            entry.AccessStatus = "Blocked";
            entry.IsAllowed = false;
            BlockedAccessCount++;
            AddLogEntry($"BLOCKED: {entry.ProcessName} (PID {entry.Pid}) - {entry.DeviceType} access terminated");

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Blocked {entry.ProcessName} from accessing {entry.DeviceType}.");
        }
        catch (ArgumentException)
        {
            entry.AccessStatus = "Blocked";
            AddLogEntry($"Process already exited: {entry.ProcessName} (PID {entry.Pid})");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MonitorStatus = $"Access denied: Cannot block {entry.ProcessName}. Requires elevated privileges.";
            AddLogEntry($"ACCESS DENIED: Could not block {entry.ProcessName} (PID {entry.Pid})");
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to block {entry.ProcessName}: {ex.Message}";
            AddLogEntry($"ERROR: Failed to block {entry.ProcessName} - {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AllowAppAsync(DeviceAccessEntry? entry)
    {
        if (entry is null) return;

        entry.IsAllowed = true;
        entry.IsSuspicious = false;
        entry.AccessStatus = "Active";
        AddLogEntry($"ALLOWED: {entry.ProcessName} (PID {entry.Pid}) - marked as trusted for {entry.DeviceType}");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Allowed {entry.ProcessName} to access {entry.DeviceType}.");
    }

    [RelayCommand]
    private async Task ToggleWebcamProtectionAsync()
    {
        WebcamProtectionEnabled = !WebcamProtectionEnabled;

        if (WebcamProtectionEnabled)
        {
            AddLogEntry("Webcam protection ENABLED");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
            await AvatarViewModel.Instance.ShowSpeechBubble("Webcam protection is now active. I'll watch for unauthorized access.");
            EnsureTimerRunning();
        }
        else
        {
            AddLogEntry("Webcam protection DISABLED");
            await AvatarViewModel.Instance.ShowSpeechBubble("Webcam protection disabled. Your camera is no longer being monitored.");
            StopTimerIfBothDisabled();
        }

        UpdateMonitorStatus();
    }

    [RelayCommand]
    private async Task ToggleMicProtectionAsync()
    {
        MicProtectionEnabled = !MicProtectionEnabled;

        if (MicProtectionEnabled)
        {
            AddLogEntry("Microphone protection ENABLED");
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
            await AvatarViewModel.Instance.ShowSpeechBubble("Microphone protection is now active. Listening for unauthorized listeners.");
            EnsureTimerRunning();
        }
        else
        {
            AddLogEntry("Microphone protection DISABLED");
            await AvatarViewModel.Instance.ShowSpeechBubble("Microphone protection disabled. Your mic is no longer being monitored.");
            StopTimerIfBothDisabled();
        }

        UpdateMonitorStatus();
    }

    [RelayCommand]
    private void TogglePreview()
    {
        if (IsPreviewActive)
        {
            StopPreview();
        }
        else
        {
            StartPreview();
        }
    }

    [RelayCommand]
    private async Task ToggleWebcamDeviceAsync()
    {
        if (ConnectedWebcams == 0)
        {
            MonitorStatus = "No webcam devices found to disable.";
            return;
        }

        try
        {
            // Use pnputil/devcon to disable/enable the webcam device
            var action = IsWebcamDisabled ? "enable" : "disable";
            IsWebcamDisabled = !IsWebcamDisabled;

            // Find webcam device instance IDs
            var deviceIds = new List<string>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Camera' OR PNPClass = 'Image' OR " +
                "Caption LIKE '%camera%' OR Caption LIKE '%webcam%' OR (Service = 'usbvideo')");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var devId = obj["DeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(devId))
                        deviceIds.Add(devId);
                }
                catch { }
                finally { obj.Dispose(); }
            }

            foreach (var devId in deviceIds)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = $"/{ action }-device \"{devId}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }

            if (IsWebcamDisabled)
            {
                StopPreview();
                AddLogEntry($"WEBCAM DISABLED: {deviceIds.Count} camera device(s) disabled via pnputil");
                MonitorStatus = "Webcam hardware disabled - camera access is blocked at device level";
                await AvatarViewModel.Instance.ShowSpeechBubble("Webcam hardware has been disabled. No applications can access the camera.");
            }
            else
            {
                AddLogEntry($"WEBCAM ENABLED: {deviceIds.Count} camera device(s) re-enabled");
                MonitorStatus = "Webcam hardware re-enabled";
                await AvatarViewModel.Instance.ShowSpeechBubble("Webcam hardware re-enabled. Applications can now access the camera.");
                await Task.Run(() => ScanConnectedDevices());
            }
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to toggle webcam: {ex.Message}. Try running as Administrator.";
            AddLogEntry($"ERROR: Webcam toggle failed - {ex.Message}");
            IsWebcamDisabled = !IsWebcamDisabled; // revert
        }
    }

    // --------------- Webcam Preview ---------------

    private void StartPreview()
    {
        try
        {
            // Get a window handle for the capture window parent
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return;

            var helper = new WindowInteropHelper(mainWindow);
            var parentHandle = helper.Handle;
            if (parentHandle == IntPtr.Zero) return;

            // Create hidden capture window
            _captureHandle = capCreateCaptureWindow(
                "JD_Webcam_Capture", WS_CHILD, 0, 0, 320, 240, parentHandle, 0);

            if (_captureHandle == IntPtr.Zero)
            {
                PreviewStatusText = "Failed to create capture window. No webcam driver found.";
                AddLogEntry("ERROR: capCreateCaptureWindow failed - no webcam driver");
                return;
            }

            // Connect to the first video capture driver (index 0)
            var connected = SendMessage(_captureHandle, WM_CAP_DRIVER_CONNECT, IntPtr.Zero, IntPtr.Zero);
            if (connected == IntPtr.Zero)
            {
                DestroyWindow(_captureHandle);
                _captureHandle = IntPtr.Zero;
                PreviewStatusText = "Failed to connect to webcam. Device may be in use or disconnected.";
                AddLogEntry("ERROR: WM_CAP_DRIVER_CONNECT failed - webcam may be in use");
                return;
            }

            IsPreviewActive = true;
            _isCapturing = true;
            PreviewStatusText = "Webcam preview active - capturing frames";
            AddLogEntry("PREVIEW: Webcam live preview started");

            // Start a timer to grab frames and convert to WPF ImageSource
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) }; // ~15 fps
            _previewTimer.Tick += (_, _) => GrabFrame();
            _previewTimer.Start();
        }
        catch (Exception ex)
        {
            PreviewStatusText = $"Preview error: {ex.Message}";
            AddLogEntry($"ERROR: Preview start failed - {ex.Message}");
        }
    }

    private void GrabFrame()
    {
        if (_captureHandle == IntPtr.Zero || !_isCapturing) return;

        try
        {
            // Grab a frame without using clipboard - use WM_CAP_GRAB_FRAME_NOSTOP
            // then copy to clipboard only as fallback, saving and restoring clipboard content
            SendMessage(_captureHandle, WM_CAP_GRAB_FRAME_NOSTOP, IntPtr.Zero, IntPtr.Zero);

            // Save current clipboard content
            var savedClipboard = Clipboard.GetDataObject();
            var hadImage = Clipboard.ContainsImage();
            var hadText = Clipboard.ContainsText() ? Clipboard.GetText() : null;

            SendMessage(_captureHandle, WM_CAP_EDIT_COPY, IntPtr.Zero, IntPtr.Zero);

            // Get from clipboard
            if (Clipboard.ContainsImage())
            {
                var bitmapSource = Clipboard.GetImage();
                if (bitmapSource != null)
                {
                    // Freeze so it can cross threads
                    bitmapSource.Freeze();
                    WebcamPreviewImage = bitmapSource;
                }
            }

            // Restore clipboard content
            try
            {
                if (hadText != null)
                    Clipboard.SetText(hadText);
                else if (!hadImage)
                    Clipboard.Clear();
            }
            catch { /* Clipboard restore can fail if another app has it locked */ }
        }
        catch
        {
            // Clipboard access can fail if another app is using it - just skip this frame
        }
    }

    private void StopPreview()
    {
        _isCapturing = false;
        _previewTimer?.Stop();
        _previewTimer = null;

        if (_captureHandle != IntPtr.Zero)
        {
            SendMessage(_captureHandle, WM_CAP_DRIVER_DISCONNECT, IntPtr.Zero, IntPtr.Zero);
            DestroyWindow(_captureHandle);
            _captureHandle = IntPtr.Zero;
        }

        IsPreviewActive = false;
        WebcamPreviewImage = null;
        PreviewStatusText = "Webcam preview is off. Click 'Open Preview' to view the camera feed.";
        AddLogEntry("PREVIEW: Webcam live preview stopped");
    }

    // --------------- Timer management ---------------

    private void EnsureTimerRunning()
    {
        if (_monitorTimer is not null) return;

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _monitorTimer.Tick += async (_, _) =>
        {
            if (_isScanning) return;
            _isScanning = true;
            try { await ScanDeviceAccessAsync(); }
            finally { _isScanning = false; }
        };
        _monitorTimer.Start();
    }

    private void StopTimerIfBothDisabled()
    {
        if (!WebcamProtectionEnabled && !MicProtectionEnabled)
        {
            _monitorTimer?.Stop();
            _monitorTimer = null;
        }
    }

    private void UpdateMonitorStatus()
    {
        if (WebcamProtectionEnabled && MicProtectionEnabled)
            MonitorStatus = $"Monitoring webcam & microphone - {ConnectedWebcams} cam(s), {ConnectedMicrophones} mic(s) - {DeviceAccesses.Count} access(es) - Refreshing every 5s";
        else if (WebcamProtectionEnabled)
            MonitorStatus = $"Monitoring webcam only - {ConnectedWebcams} cam(s) - {DeviceAccesses.Count} access(es) - Refreshing every 5s";
        else if (MicProtectionEnabled)
            MonitorStatus = $"Monitoring microphone only - {ConnectedMicrophones} mic(s) - {DeviceAccesses.Count} access(es) - Refreshing every 5s";
        else
            MonitorStatus = "Protection disabled - Enable webcam or microphone protection to begin monitoring";
    }

    // --------------- Core scanning logic ---------------

    private async Task ScanDeviceAccessAsync()
    {
        // Snapshot collections on UI thread before entering background work
        List<(int Pid, string ProcessName, string AccessStatus)> existingEntries = [];
        Application.Current?.Dispatcher.Invoke(() =>
        {
            existingEntries = DeviceAccesses.Select(d => (d.Pid, d.ProcessName, d.AccessStatus)).ToList();
        });

        await Task.Run(() =>
        {
            var currentPids = new HashSet<int>(existingEntries.Select(d => d.Pid));
            var foundPids = new HashSet<int>();

            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    var name = proc.ProcessName;
                    var pid = proc.Id;
                    var isCaptureApi = CaptureApiProcesses.Contains(name);
                    var isLegit = KnownLegitimateApps.Contains(name);

                    bool hasWebcam = WebcamProtectionEnabled && HasDeviceConsent(WebcamConsentPath, name);
                    bool hasMic = MicProtectionEnabled && HasDeviceConsent(MicrophoneConsentPath, name);

                    // Also check for processes that have open handles to camera/audio class GUIDs
                    if (!hasWebcam && !hasMic && !isCaptureApi)
                    {
                        // Check if this process has loaded camera-related DLLs
                        try
                        {
                            var modules = proc.Modules;
                            foreach (ProcessModule mod in modules)
                            {
                                var modName = mod.ModuleName.ToLowerInvariant();
                                if (modName.Contains("mfcaptureengine") || modName.Contains("mfplat") ||
                                    modName.Contains("frameserver") || modName.Contains("vidcap"))
                                {
                                    // Process uses media foundation capture - likely uses camera
                                    if (WebcamProtectionEnabled) hasWebcam = true;
                                    break;
                                }
                            }
                        }
                        catch { /* Cannot enumerate modules - access denied */ }
                    }

                    if (!hasWebcam && !hasMic && !isCaptureApi) continue;

                    foundPids.Add(pid);

                    if (currentPids.Contains(pid)) continue;

                    string deviceType;
                    if (hasWebcam && hasMic) deviceType = "Both";
                    else if (hasWebcam) deviceType = "Webcam";
                    else if (hasMic) deviceType = "Microphone";
                    else deviceType = isCaptureApi ? "Both" : "Webcam";

                    bool suspicious = !isLegit;

                    string exePath;
                    try { exePath = proc.MainModule?.FileName ?? "Unknown"; }
                    catch { exePath = "Access Denied"; }

                    var entry = new DeviceAccessEntry
                    {
                        ProcessName = name,
                        DeviceType = deviceType,
                        AccessStatus = "Active",
                        StartTime = DateTime.Now,
                        Pid = pid,
                        IsAllowed = isLegit,
                        IsSuspicious = suspicious,
                        ExecutablePath = exePath,
                        NetworkConnections = GetProcessNetworkInfo(pid),
                        DataDirection = hasWebcam && hasMic ? "Camera + Audio -> Process" : hasWebcam ? "Camera -> Process" : "Audio -> Process",
                    };

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        DeviceAccesses.Add(entry);

                        if (suspicious)
                            AddLogEntry($"SUSPICIOUS: {name} (PID {pid}) accessing {deviceType}");
                        else
                            AddLogEntry($"Detected: {name} (PID {pid}) accessing {deviceType}");
                    });
                }
                catch { /* access denied or process exited */ }
            }

            // Check registry consent stores
            var trackedNames = new HashSet<string>(existingEntries.Select(e => e.ProcessName), StringComparer.OrdinalIgnoreCase);
            if (WebcamProtectionEnabled) ScanRegistryConsent(WebcamConsentPath, "Webcam", currentPids, foundPids, trackedNames);
            if (MicProtectionEnabled) ScanRegistryConsent(MicrophoneConsentPath, "Microphone", currentPids, foundPids, trackedNames);

            // Also check HKCU consent stores
            if (WebcamProtectionEnabled) ScanRegistryConsentHkcu(WebcamConsentPath, "Webcam", trackedNames);
            if (MicProtectionEnabled) ScanRegistryConsentHkcu(MicrophoneConsentPath, "Microphone", trackedNames);

            // WMI active device count
            ScanWmiDevices();

            // Clean up entries for processes that no longer exist
            var activePids = new HashSet<int>(processes.Select(p =>
            {
                try { return p.Id; }
                catch { return 0; }
            }));

            // Use snapshot for identifying dead entries, then update on dispatcher
            var deadPids = existingEntries
                .Where(d => d.AccessStatus == "Active" && d.Pid > 0 && !activePids.Contains(d.Pid))
                .Select(d => d.Pid).ToHashSet();

            if (deadPids.Count > 0)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    foreach (var d in DeviceAccesses)
                    {
                        if (deadPids.Contains(d.Pid) && d.AccessStatus == "Active")
                            d.AccessStatus = "Recent";
                    }
                });
            }

            foreach (var p in processes) { try { p.Dispose(); } catch { } }
        });

        RecalculateStats();
        UpdateMonitorStatus();
    }

    private static bool HasDeviceConsent(string registryPath, string processName)
    {
        try
        {
            // Check HKLM
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key is not null)
            {
                using var nonPackaged = key.OpenSubKey("NonPackaged");
                if (nonPackaged is not null)
                {
                    foreach (var subKeyName in nonPackaged.GetSubKeyNames())
                    {
                        if (subKeyName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                        {
                            using var appKey = nonPackaged.OpenSubKey(subKeyName);
                            var stopValue = appKey?.GetValue("LastUsedTimeStop");
                            // If LastUsedTimeStop is 0, the app is currently using the device
                            if (stopValue is long stopLong && stopLong == 0) return true;
                            // Also return true if it has any consent record (recent usage)
                            if (stopValue is not null) return true;
                        }
                    }
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subKeyName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Check HKCU
            using var hkcuKey = Registry.CurrentUser.OpenSubKey(registryPath);
            if (hkcuKey is not null)
            {
                using var hkcuNonPackaged = hkcuKey.OpenSubKey("NonPackaged");
                if (hkcuNonPackaged is not null)
                {
                    foreach (var subKeyName in hkcuNonPackaged.GetSubKeyNames())
                    {
                        if (subKeyName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }

                foreach (var subKeyName in hkcuKey.GetSubKeyNames())
                {
                    if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase)) continue;
                    if (subKeyName.Contains(processName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch { /* Registry access may be restricted */ }

        return false;
    }

    private void ScanRegistryConsent(string registryPath, string deviceType, HashSet<int> currentPids, HashSet<int> foundPids, HashSet<string> trackedNames)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key is null) return;

            using var nonPackaged = key.OpenSubKey("NonPackaged");
            if (nonPackaged is null) return;

            foreach (var subKeyName in nonPackaged.GetSubKeyNames())
            {
                using var appKey = nonPackaged.OpenSubKey(subKeyName);
                if (appKey is null) continue;

                var lastUsed = appKey.GetValue("LastUsedTimeStart");
                if (lastUsed is null) continue;

                var parts = subKeyName.Split('#');
                var appName = parts.Length > 0 ? parts[0] : subKeyName;
                if (appName.Contains('\\'))
                    appName = appName[(appName.LastIndexOf('\\') + 1)..];
                if (appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    appName = appName[..^4];

                bool alreadyTracked = trackedNames.Contains(appName);

                if (!alreadyTracked)
                {
                    trackedNames.Add(appName);
                    bool isLegit = KnownLegitimateApps.Contains(appName);

                    // Check if LastUsedTimeStop is 0 (currently active)
                    var stopValue = appKey.GetValue("LastUsedTimeStop");
                    bool isCurrentlyActive = stopValue is long stopLong && stopLong == 0;

                    var entry = new DeviceAccessEntry
                    {
                        ProcessName = appName,
                        DeviceType = deviceType,
                        AccessStatus = isCurrentlyActive ? "Active" : "Recent",
                        StartTime = DateTime.Now,
                        Pid = 0,
                        IsAllowed = isLegit,
                        IsSuspicious = !isLegit,
                    };

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        DeviceAccesses.Add(entry);
                    });
                }
            }
        }
        catch { /* Registry inaccessible */ }
    }

    private void ScanRegistryConsentHkcu(string registryPath, string deviceType, HashSet<string> trackedNames)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath);
            if (key is null) return;

            using var nonPackaged = key.OpenSubKey("NonPackaged");
            if (nonPackaged is null) return;

            foreach (var subKeyName in nonPackaged.GetSubKeyNames())
            {
                using var appKey = nonPackaged.OpenSubKey(subKeyName);
                if (appKey is null) continue;

                var lastUsed = appKey.GetValue("LastUsedTimeStart");
                if (lastUsed is null) continue;

                var parts = subKeyName.Split('#');
                var appName = parts.Length > 0 ? parts[0] : subKeyName;
                if (appName.Contains('\\'))
                    appName = appName[(appName.LastIndexOf('\\') + 1)..];
                if (appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    appName = appName[..^4];

                bool alreadyTracked = trackedNames.Contains(appName);

                if (!alreadyTracked)
                {
                    trackedNames.Add(appName);
                    bool isLegit = KnownLegitimateApps.Contains(appName);
                    var stopValue = appKey.GetValue("LastUsedTimeStop");
                    bool isCurrentlyActive = stopValue is long stopLong && stopLong == 0;

                    var entry = new DeviceAccessEntry
                    {
                        ProcessName = appName,
                        DeviceType = deviceType,
                        AccessStatus = isCurrentlyActive ? "Active" : "Recent",
                        StartTime = DateTime.Now,
                        Pid = 0,
                        IsAllowed = isLegit,
                        IsSuspicious = !isLegit,
                    };

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        DeviceAccesses.Add(entry);
                    });
                }
            }
        }
        catch { /* Registry inaccessible */ }
    }

    private void ScanWmiDevices()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Camera' OR PNPClass = 'Image' OR " +
                "PNPClass = 'AudioEndpoint' OR " +
                "Caption LIKE '%camera%' OR Caption LIKE '%webcam%' OR " +
                "Caption LIKE '%video%' OR Caption LIKE '%microphone%' OR " +
                "Caption LIKE '%audio input%'");

            var results = searcher.Get();
            int activeCount = 0;
            foreach (ManagementObject obj in results)
            {
                try
                {
                    var status = obj["Status"]?.ToString();
                    if (status is not null && status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                        activeCount++;
                }
                catch { }
                finally { obj.Dispose(); }
            }

            ActiveDevices = activeCount;
        }
        catch { }
    }

    // --------------- Helpers ---------------

    private void RecalculateStats()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            WebcamAccessCount = DeviceAccesses.Count(d =>
                d.DeviceType is "Webcam" or "Both" && d.AccessStatus != "Blocked");

            MicAccessCount = DeviceAccesses.Count(d =>
                d.DeviceType is "Microphone" or "Both" && d.AccessStatus != "Blocked");

            BlockedAccessCount = DeviceAccesses.Count(d => d.AccessStatus == "Blocked");
        });
    }

    private void AddLogEntry(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            AccessLog.Insert(0, timestamped);
            TrimLog();
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AccessLog.Insert(0, timestamped);
                TrimLog();
            });
        }
    }

    private void TrimLog()
    {
        while (AccessLog.Count > 200)
            AccessLog.RemoveAt(AccessLog.Count - 1);
    }

    private static string GetProcessNetworkInfo(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "Unable to query";

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            var pidStr = pid.ToString();
            var connections = output.Split('\n')
                .Where(l =>
                {
                    var parts = l.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length > 0 && parts[^1] == pidStr &&
                           (l.Contains("ESTABLISHED") || l.Contains("LISTENING"));
                })
                .Select(l => l.Trim())
                .Take(5)
                .ToList();

            return connections.Count > 0
                ? string.Join(" | ", connections.Select(c => {
                    var parts = c.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 3 ? $"{parts[1]} -> {parts[2]}" : c;
                }))
                : "No active connections";
        }
        catch { return "Unable to query"; }
    }

    private void ScanConnectedDevices()
    {
        try
        {
            int webcamCount = 0;
            int micCount = 0;
            var devices = new List<ConnectedDeviceInfo>();

            // ---------- Webcam devices ----------
            // Method 1: PNPClass Camera / Image
            using (var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Camera' OR PNPClass = 'Image'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "Unknown Camera";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        var devId = obj["DeviceID"]?.ToString() ?? "";
                        var mfr = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        var pnpClass = obj["PNPClass"]?.ToString() ?? "";

                        webcamCount++;
                        devices.Add(new ConnectedDeviceInfo
                        {
                            DeviceName = caption,
                            DeviceType = "Webcam",
                            Status = status,
                            DeviceId = devId,
                            Manufacturer = mfr,
                            PnpClass = pnpClass,
                            IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            // Method 2: Caption-based search
            if (webcamCount == 0)
            {
                using var searcher2 = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%camera%' OR " +
                    "Caption LIKE '%webcam%' OR Caption LIKE '%video capture%' OR " +
                    "Caption LIKE '%USB Video%' OR Caption LIKE '%imaging device%'");
                foreach (ManagementObject obj in searcher2.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "Unknown Camera";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        var devId = obj["DeviceID"]?.ToString() ?? "";
                        var mfr = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        webcamCount++;
                        devices.Add(new ConnectedDeviceInfo
                        {
                            DeviceName = caption,
                            DeviceType = "Webcam",
                            Status = status,
                            DeviceId = devId,
                            Manufacturer = mfr,
                            IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            // Method 3: USB Video Class driver
            if (webcamCount == 0)
            {
                using var searcher3 = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Service = 'usbvideo' OR Service = 'ksthunk'");
                foreach (ManagementObject obj in searcher3.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "USB Video Device";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        var devId = obj["DeviceID"]?.ToString() ?? "";
                        webcamCount++;
                        devices.Add(new ConnectedDeviceInfo
                        {
                            DeviceName = caption,
                            DeviceType = "Webcam",
                            Status = status,
                            DeviceId = devId,
                            IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            // ---------- Microphone / Audio devices ----------
            // Method 1: PNPClass AudioEndpoint
            using (var micSearcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'"))
            {
                foreach (ManagementObject obj in micSearcher.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "Unknown Audio";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        var devId = obj["DeviceID"]?.ToString() ?? "";
                        var mfr = obj["Manufacturer"]?.ToString() ?? "Unknown";

                        // Filter - only count input/microphone endpoints
                        // AudioEndpoint captures both speakers and mics, so check names
                        bool isMicLikely = caption.Contains("Microphone", StringComparison.OrdinalIgnoreCase) ||
                                           caption.Contains("Mic", StringComparison.OrdinalIgnoreCase) ||
                                           caption.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
                                           caption.Contains("Recording", StringComparison.OrdinalIgnoreCase) ||
                                           caption.Contains("Line In", StringComparison.OrdinalIgnoreCase) ||
                                           caption.Contains("Headset", StringComparison.OrdinalIgnoreCase);

                        bool isSpeaker = caption.Contains("Speaker", StringComparison.OrdinalIgnoreCase) ||
                                         caption.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
                                         caption.Contains("Output", StringComparison.OrdinalIgnoreCase) ||
                                         caption.Contains("Playback", StringComparison.OrdinalIgnoreCase) ||
                                         caption.Contains("Digital Audio", StringComparison.OrdinalIgnoreCase);

                        if (isMicLikely || !isSpeaker) // If unsure, count it
                        {
                            micCount++;
                            devices.Add(new ConnectedDeviceInfo
                            {
                                DeviceName = caption,
                                DeviceType = isMicLikely ? "Microphone" : "Audio Endpoint",
                                Status = status,
                                DeviceId = devId,
                                Manufacturer = mfr,
                                IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                            });
                        }
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            // Method 2: Caption search for mic
            if (micCount == 0)
            {
                using var micSearcher2 = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%microphone%' OR " +
                    "Caption LIKE '%audio input%' OR Caption LIKE '%USB Audio%' OR " +
                    "Caption LIKE '%recording%'");
                foreach (ManagementObject obj in micSearcher2.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "Unknown Audio";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        micCount++;
                        devices.Add(new ConnectedDeviceInfo
                        {
                            DeviceName = caption,
                            DeviceType = "Microphone",
                            Status = status,
                            IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            // Method 3: Win32_SoundDevice fallback
            if (micCount == 0)
            {
                using var soundSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice");
                foreach (ManagementObject obj in soundSearcher.Get())
                {
                    try
                    {
                        var caption = obj["Caption"]?.ToString() ?? "Audio Device";
                        var status = obj["Status"]?.ToString() ?? "Unknown";
                        var mfr = obj["Manufacturer"]?.ToString() ?? "Unknown";
                        micCount++;
                        devices.Add(new ConnectedDeviceInfo
                        {
                            DeviceName = caption,
                            DeviceType = "Audio Device",
                            Status = status,
                            Manufacturer = mfr,
                            IsEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    catch { }
                    finally { obj.Dispose(); }
                }
            }

            var deviceListText = new System.Text.StringBuilder();
            foreach (var dev in devices)
            {
                deviceListText.AppendLine($"[{dev.DeviceType}] {dev.DeviceName} - Status: {dev.Status}" +
                    (string.IsNullOrEmpty(dev.Manufacturer) || dev.Manufacturer == "Unknown" ? "" : $" ({dev.Manufacturer})"));
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                ConnectedWebcams = webcamCount;
                ConnectedMicrophones = micCount;
                ConnectedDevices.Clear();
                foreach (var d in devices)
                    ConnectedDevices.Add(d);

                ConnectedDevicesList = deviceListText.Length > 0
                    ? deviceListText.ToString().TrimEnd()
                    : "No webcam or microphone devices detected. Check device connections.";
            });
        }
        catch (Exception ex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ConnectedDevicesList = $"Error scanning devices: {ex.Message}";
            });
        }
    }
}
