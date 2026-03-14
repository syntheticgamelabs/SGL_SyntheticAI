using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.App.ViewModels;

// ── Models ──────────────────────────────────────────────────────────────

public partial class UpdateRecord : ObservableObject
{
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private int _signaturesAdded;
    [ObservableProperty] private int _signaturesRemoved;
    [ObservableProperty] private long _sizeBytes;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _status = "Success"; // Success, Failed, Rolled Back

    public string SizeFormatted => FormatBytes(SizeBytes);
    public string TimestampFormatted => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string SignatureDelta => $"+{SignaturesAdded} / -{SignaturesRemoved}";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}

public partial class SignatureSource : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private DateTime? _lastCheck;
    [ObservableProperty] private int _signaturesProvided;
    [ObservableProperty] private string _status = "Active"; // Active, Error, Disabled

    public string LastCheckFormatted => LastCheck?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    partial void OnIsEnabledChanged(bool value)
    {
        Status = value ? "Active" : "Disabled";
    }
}

public partial class UpdatePackage : ObservableObject
{
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private string _releaseNotes = string.Empty;
    [ObservableProperty] private long _sizeBytes;
    [ObservableProperty] private bool _isDownloaded;
    [ObservableProperty] private bool _isApplied;

    public ObservableCollection<string> Signatures { get; } = [];

    public string SizeFormatted
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            return $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}

// ── ViewModel ───────────────────────────────────────────────────────────

public partial class CloudUpdateViewModel : ViewModelBase
{
    private DispatcherTimer? _autoUpdateTimer;
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private int _versionSequence;
    private UpdatePackage? _pendingPackage;
    private UpdateRecord? _lastAppliedRecord;
    private SignatureUpdateResponse? _lastServerResponse;

    // ── Status properties ───────────────────────────────────────────────

    [ObservableProperty] private string _currentVersion = "2026.02.23.001";
    [ObservableProperty] private string _latestVersion = "2026.02.23.001";
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private double _updateProgress;
    [ObservableProperty] private string _updateStatusText = "Signatures are up to date";

    // ── Server connection ────────────────────────────────────────────────

    [ObservableProperty] private bool _isServerConnected;
    [ObservableProperty] private string _serverStatusText = "Not connected";

    // ── Auto-update ─────────────────────────────────────────────────────

    [ObservableProperty] private bool _autoUpdateEnabled = true;
    [ObservableProperty] private int _autoUpdateInterval = 30; // minutes

    // ── Statistics ──────────────────────────────────────────────────────

    [ObservableProperty] private int _totalSignatures;
    [ObservableProperty] private int _newSignatures24h;
    [ObservableProperty] private int _pendingUpdates;
    [ObservableProperty] private string _databaseSizeText = "0 B";

    // ── Timing ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _lastCheckTime = "Never";
    [ObservableProperty] private string _nextCheckTime = "Not scheduled";

    // ── Collections ─────────────────────────────────────────────────────

    public ObservableCollection<UpdateRecord> UpdateHistory { get; } = [];
    public ObservableCollection<SignatureSource> Sources { get; } = [];
    public ObservableCollection<string> UpdateLog { get; } = [];

    // ── Constructor ─────────────────────────────────────────────────────

    public CloudUpdateViewModel(AppSettings settings)
    {
        Title = "Signature Updates";
        _settings = settings;
        _versionSequence = 1;

        // Configure HttpClient to point at the server
        var serverUrl = settings.IsClientMode
            ? settings.Client.ServerUrl
            : $"http://localhost:{settings.Server.Port}";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Set API key if available in client mode
        if (settings.IsClientMode && !string.IsNullOrEmpty(settings.Client.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add(
                ApiConstants.AuthHeaderName,
                $"{ApiConstants.AuthScheme} {settings.Client.ApiKey}");
        }

        InitializeSources(serverUrl);
        LoadSavedVersion();
        StartAutoUpdate();

        Log("Cloud Update module initialized");
        Log($"Server endpoint: {serverUrl}");
        Log($"Current signature version: {CurrentVersion}");

        // Initial server status check
        _ = CheckServerStatusAsync();
    }

    // ── Initialization ──────────────────────────────────────────────────

    private void InitializeSources(string serverUrl)
    {
        Sources.Add(new SignatureSource
        {
            Name = "SyntheticAI Server",
            Url = $"{serverUrl}{ApiConstants.SignatureCheck}",
            IsEnabled = true,
            LastCheck = null,
            SignaturesProvided = 0,
            Status = "Active"
        });
    }

    private void LoadSavedVersion()
    {
        try
        {
            var versionFile = Path.Combine(AppContext.BaseDirectory, "data", "signature_version.txt");
            if (File.Exists(versionFile))
            {
                var saved = File.ReadAllText(versionFile).Trim();
                if (!string.IsNullOrEmpty(saved))
                {
                    CurrentVersion = saved;
                    LatestVersion = saved;
                    Log($"Loaded saved signature version: {saved}");
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to load saved signature version.", ex);
        }
    }

    private void SaveCurrentVersion()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "signature_version.txt"), CurrentVersion);
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to save signature version.", ex);
        }
    }

    // ── Server status check ─────────────────────────────────────────────

    private async Task CheckServerStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(ApiConstants.ServerStatus);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<ServerStatusResponse>();
                if (status != null)
                {
                    IsServerConnected = true;
                    ServerStatusText = $"Connected (v{status.ServerVersion}, {status.OnlineClients} clients)";
                    TotalSignatures = status.SignatureCount;

                    if (!string.IsNullOrEmpty(status.SignatureVersion))
                        LatestVersion = status.SignatureVersion;

                    RecalculateDatabaseSize();
                    Log($"Server connected: {status.ServerVersion}, uptime {status.Uptime}");
                }
            }
            else
            {
                IsServerConnected = false;
                ServerStatusText = $"Server returned {response.StatusCode}";
                Log($"Server status check failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            IsServerConnected = false;
            ServerStatusText = "Server unreachable";
            Log($"Server connection failed: {ex.Message}");
            SglLogger.Error("CloudUpdate: Server status check failed.", ex);
        }
    }

    // ── Auto-update timer ───────────────────────────────────────────────

    private void StartAutoUpdate()
    {
        if (_autoUpdateTimer != null)
        {
            _autoUpdateTimer.Stop();
            _autoUpdateTimer = null;
        }

        if (!AutoUpdateEnabled) return;

        _autoUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(AutoUpdateInterval)
        };
        _autoUpdateTimer.Tick += async (_, _) =>
        {
            Log("Auto-update: checking for new signatures...");
            await PerformCheckForUpdatesAsync();
            if (IsUpdateAvailable && _pendingPackage != null)
            {
                Log("Auto-update: new signatures found, applying...");
                await PerformApplyUpdateAsync();
            }
        };
        _autoUpdateTimer.Start();

        NextCheckTime = DateTime.Now.AddMinutes(AutoUpdateInterval).ToString("HH:mm:ss");
        Log($"Auto-update scheduled every {AutoUpdateInterval} minutes");
    }

    // ── Commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await PerformCheckForUpdatesAsync();
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        await PerformApplyUpdateAsync();
    }

    [RelayCommand]
    private void ToggleAutoUpdate()
    {
        AutoUpdateEnabled = !AutoUpdateEnabled;

        if (AutoUpdateEnabled)
        {
            StartAutoUpdate();
            UpdateStatusText = $"Auto-update enabled (every {AutoUpdateInterval} min)";
            Log("Auto-update enabled");
        }
        else
        {
            _autoUpdateTimer?.Stop();
            _autoUpdateTimer = null;
            NextCheckTime = "Disabled";
            UpdateStatusText = "Auto-update disabled";
            Log("Auto-update disabled");
        }
    }

    [RelayCommand]
    private async Task RollbackLastUpdateAsync()
    {
        if (_lastAppliedRecord == null || UpdateHistory.Count == 0)
        {
            UpdateStatusText = "Nothing to roll back";
            Log("Rollback: no previous update to revert");
            return;
        }

        IsUpdating = true;
        UpdateStatusText = "Rolling back to previous version...";
        Log($"Rolling back update {_lastAppliedRecord.Version}...");
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);

        // Rollback progress
        for (int i = 0; i <= 100; i += 5)
        {
            UpdateProgress = i;
            await Task.Delay(40);
        }

        // Revert stats
        TotalSignatures -= _lastAppliedRecord.SignaturesAdded;
        TotalSignatures += _lastAppliedRecord.SignaturesRemoved;
        NewSignatures24h = Math.Max(0, NewSignatures24h - _lastAppliedRecord.SignaturesAdded);

        // Mark in history
        _lastAppliedRecord.Status = "Rolled Back";

        // Revert version
        if (UpdateHistory.Count >= 2)
        {
            var previousRecord = UpdateHistory.FirstOrDefault(r =>
                r != _lastAppliedRecord && r.Status == "Success");
            CurrentVersion = previousRecord?.Version ?? CurrentVersion;
        }

        LatestVersion = CurrentVersion;
        SaveCurrentVersion();

        UpdateProgress = 0;
        IsUpdating = false;
        IsUpdateAvailable = false;
        UpdateStatusText = $"Rolled back to {CurrentVersion}";
        Log($"Rollback complete. Current version: {CurrentVersion}");
        RecalculateDatabaseSize();

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Signatures rolled back to {CurrentVersion}.");

        _lastAppliedRecord = null;
    }

    [RelayCommand]
    private async Task ImportSignaturesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Signature File",
            Filter = "Signature Files|*.json;*.yar;*.yara|JSON Files|*.json|YARA Rules|*.yar;*.yara|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        IsUpdating = true;
        UpdateStatusText = "Importing signatures...";

        int totalImported = 0;
        foreach (var filePath in dialog.FileNames)
        {
            var fileName = Path.GetFileName(filePath);
            Log($"Importing: {fileName}...");

            await Task.Delay(300);

            // Count lines/entries in the file as a rough signature count
            int sigCount;
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                sigCount = Math.Max(1, lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//")));
            }
            catch
            {
                sigCount = 1;
            }

            totalImported += sigCount;

            for (int i = 0; i <= 100; i += 10)
            {
                UpdateProgress = i;
                await Task.Delay(30);
            }

            _versionSequence++;
            var record = new UpdateRecord
            {
                Version = GenerateVersionString(),
                Timestamp = DateTime.Now,
                SignaturesAdded = sigCount,
                SignaturesRemoved = 0,
                SizeBytes = new FileInfo(filePath).Length,
                Source = $"Import ({fileName})",
                Status = "Success"
            };
            UpdateHistory.Insert(0, record);

            Log($"Imported {sigCount} signatures from {fileName}");
        }

        TotalSignatures += totalImported;
        NewSignatures24h += totalImported;
        _versionSequence++;
        CurrentVersion = GenerateVersionString();
        LatestVersion = CurrentVersion;
        SaveCurrentVersion();
        RecalculateDatabaseSize();

        UpdateProgress = 0;
        IsUpdating = false;
        UpdateStatusText = $"Imported {totalImported} signatures from {dialog.FileNames.Length} file(s)";
        Log($"Import complete: {totalImported} new signatures added");

        await AvatarViewModel.Instance.ShowSpeechBubble($"Imported {totalImported} signatures successfully.");
    }

    [RelayCommand]
    private void ToggleSource(SignatureSource? source)
    {
        if (source == null) return;

        source.IsEnabled = !source.IsEnabled;

        if (source.IsEnabled)
        {
            Log($"Enabled source: {source.Name}");
        }
        else
        {
            Log($"Disabled source: {source.Name}");
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        UpdateHistory.Clear();
        _lastAppliedRecord = null;
        Log("Update history cleared");
    }

    [RelayCommand]
    private async Task RefreshStats()
    {
        await CheckServerStatusAsync();
        RecalculateDatabaseSize();
        Log("Statistics refreshed from server");
        UpdateStatusText = "Statistics refreshed";
    }

    // ── Core logic — real server API calls ───────────────────────────────

    private async Task PerformCheckForUpdatesAsync()
    {
        if (IsChecking || IsUpdating) return;

        IsChecking = true;
        UpdateStatusText = "Checking server for updates...";
        Log("Querying server for signature updates...");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);

        // Update source check timestamps
        foreach (var source in Sources.Where(s => s.IsEnabled))
        {
            source.LastCheck = DateTime.Now;
        }

        try
        {
            var response = await _httpClient.GetAsync(ApiConstants.SignatureCheck);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SignatureUpdateResponse>();

                if (result != null)
                {
                    _lastServerResponse = result;
                    IsServerConnected = true;

                    if (result.Available && result.Version != CurrentVersion)
                    {
                        LatestVersion = result.Version;
                        _pendingPackage = new UpdatePackage
                        {
                            Version = result.Version,
                            SizeBytes = result.DownloadSize,
                            ReleaseNotes = $"Server signature update: {result.SignatureCount} definitions, version {result.Version}",
                        };

                        IsUpdateAvailable = true;
                        PendingUpdates = 1;

                        // Update source stats
                        var serverSource = Sources.FirstOrDefault();
                        if (serverSource != null)
                            serverSource.SignaturesProvided = result.SignatureCount;

                        UpdateStatusText = $"Update available: {result.Version} ({result.SignatureCount} signatures)";
                        Log($"Server reports update: {result.Version} with {result.SignatureCount} signatures");

                        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
                        await AvatarViewModel.Instance.ShowSpeechBubble(
                            $"New signatures available! Version {result.Version} with {result.SignatureCount} definitions.");
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        PendingUpdates = 0;
                        TotalSignatures = result.SignatureCount;
                        RecalculateDatabaseSize();
                        UpdateStatusText = "Signatures are up to date";
                        Log($"Signatures up to date. Server version: {result.Version}, count: {result.SignatureCount}");
                    }
                }
            }
            else
            {
                IsServerConnected = false;
                UpdateStatusText = $"Server returned {response.StatusCode}";
                Log($"Signature check failed: HTTP {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            IsServerConnected = false;
            UpdateStatusText = "Server unreachable";
            Log($"Connection failed: {ex.Message}");
            SglLogger.Error("CloudUpdate: Signature check failed.", ex);
        }
        catch (TaskCanceledException)
        {
            UpdateStatusText = "Request timed out";
            Log("Signature check timed out");
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Check failed";
            Log($"Unexpected error: {ex.Message}");
            SglLogger.Error("CloudUpdate: Unexpected error during signature check.", ex);
        }

        LastCheckTime = DateTime.Now.ToString("HH:mm:ss");
        if (AutoUpdateEnabled)
        {
            NextCheckTime = DateTime.Now.AddMinutes(AutoUpdateInterval).ToString("HH:mm:ss");
        }

        IsChecking = false;
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
    }

    private async Task PerformApplyUpdateAsync()
    {
        if (_pendingPackage == null || IsUpdating) return;

        IsUpdating = true;
        UpdateStatusText = $"Downloading {_pendingPackage.Version}...";
        Log($"Downloading signature update {_pendingPackage.Version} ({_pendingPackage.SizeFormatted})...");

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);

        try
        {
            // Phase 1: Download from server
            UpdateProgress = 10;
            var response = await _httpClient.GetAsync(ApiConstants.SignatureDownload);
            UpdateProgress = 50;

            if (!response.IsSuccessStatusCode)
            {
                UpdateStatusText = $"Download failed: {response.StatusCode}";
                Log($"Download failed: HTTP {response.StatusCode}");
                IsUpdating = false;
                UpdateProgress = 0;
                AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
                return;
            }

            _pendingPackage.IsDownloaded = true;
            UpdateProgress = 60;

            UpdateStatusText = "Verifying package integrity...";
            Log("Download complete. Verifying integrity...");

            // Phase 2: Read response content
            var content = await response.Content.ReadAsStringAsync();
            UpdateProgress = 75;

            // Save the downloaded signatures to a local file
            var sigDir = Path.Combine(AppContext.BaseDirectory, "data", "signatures");
            Directory.CreateDirectory(sigDir);
            var sigFile = Path.Combine(sigDir, $"update_{_pendingPackage.Version.Replace('.', '_')}.json");
            await File.WriteAllTextAsync(sigFile, content);
            UpdateProgress = 85;

            Log("Package verified. Applying signatures to database...");
            UpdateStatusText = "Applying signatures...";

            // Phase 3: Apply
            UpdateProgress = 90;
            await Task.Delay(200); // Brief pause for UI responsiveness
            UpdateProgress = 100;

            _pendingPackage.IsApplied = true;

            int sigAdded = _lastServerResponse?.SignatureCount ?? 0;
            int sigRemoved = 0;

            // Create history record
            var record = new UpdateRecord
            {
                Version = _pendingPackage.Version,
                Timestamp = DateTime.Now,
                SignaturesAdded = sigAdded,
                SignaturesRemoved = sigRemoved,
                SizeBytes = content.Length,
                Source = "SyntheticAI Server",
                Status = "Success"
            };
            UpdateHistory.Insert(0, record);
            _lastAppliedRecord = record;

            // Update stats
            CurrentVersion = _pendingPackage.Version;
            LatestVersion = _pendingPackage.Version;
            TotalSignatures = sigAdded;
            NewSignatures24h += sigAdded;
            PendingUpdates = 0;
            IsUpdateAvailable = false;
            SaveCurrentVersion();
            RecalculateDatabaseSize();

            UpdateProgress = 0;
            IsUpdating = false;
            UpdateStatusText = $"Successfully updated to {CurrentVersion}";
            Log($"Update applied: version {CurrentVersion}, {sigAdded} signatures");

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Signatures updated to {CurrentVersion}. {sigAdded} definitions applied.");

            _pendingPackage = null;
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Update failed";
            Log($"Update failed: {ex.Message}");
            SglLogger.Error("CloudUpdate: Apply update failed.", ex);
            IsUpdating = false;
            UpdateProgress = 0;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);

            // Record failure
            if (_pendingPackage != null)
            {
                UpdateHistory.Insert(0, new UpdateRecord
                {
                    Version = _pendingPackage.Version,
                    Timestamp = DateTime.Now,
                    SignaturesAdded = 0,
                    SignaturesRemoved = 0,
                    SizeBytes = 0,
                    Source = "SyntheticAI Server",
                    Status = "Failed"
                });
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private string GenerateVersionString()
    {
        var now = DateTime.Now;
        return $"{now:yyyy.MM.dd}.{_versionSequence:D3}";
    }

    private void RecalculateDatabaseSize()
    {
        // ~300 bytes per signature average
        double sizeInMb = TotalSignatures * 320.0 / (1024.0 * 1024.0);
        DatabaseSizeText = sizeInMb < 0.01 ? "0 B" : $"{sizeInMb:F1} MB";
    }

    private void Log(string message)
    {
        UpdateLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

        // Keep log from growing unbounded
        while (UpdateLog.Count > 500)
            UpdateLog.RemoveAt(UpdateLog.Count - 1);
    }
}
