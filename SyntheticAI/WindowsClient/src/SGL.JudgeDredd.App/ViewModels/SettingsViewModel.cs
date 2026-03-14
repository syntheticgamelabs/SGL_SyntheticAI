using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.LLM;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Localization;

namespace SGL.JudgeDredd.App.ViewModels;

/// <summary>
/// Represents a single LLM model in the UI with install/active/download status.
/// </summary>
public partial class LlmModelDisplayItem : ObservableObject
{
    public LlmModelInfo Info { get; set; } = new();

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = "";
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly ILlmService? _llmService;
    private readonly LlmModelManager? _modelManager;
    private CancellationTokenSource? _downloadCts;

    // General settings
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _notificationSounds = true;

    // Scanner settings
    [ObservableProperty]
    private bool _realTimeProtection = true;

    [ObservableProperty]
    private string _scanExclusions = string.Empty;

    // Firewall settings
    [ObservableProperty]
    private string _defaultFirewallAction = "Block";

    [ObservableProperty]
    private string _loggingVerbosity = "Normal";

    // LLM settings
    [ObservableProperty]
    private string _modelPath = string.Empty;

    [ObservableProperty]
    private int _contextSize = 8192;

    [ObservableProperty]
    private int _gpuLayers = 33;

    [ObservableProperty]
    private float _temperature = 0.8f;

    // LLM Prompt settings
    [ObservableProperty]
    private string _systemPrompt = string.Empty;

    // Security settings
    [ObservableProperty]
    private bool _enableRemoteAccessDetection = true;

    [ObservableProperty]
    private bool _enableBadUsbDetection = true;

    [ObservableProperty]
    private bool _enableAiDetection = true;

    [ObservableProperty]
    private bool _enableRegistryWatcher = true;

    // ── LLM Model Management ──

    // ── FAQ (embedded in Settings tab) ──
    public FaqViewModel? FaqVm { get; set; }

    // ── Language Settings ──
    public LanguageInfo[] SupportedLanguages => LocalizationService.SupportedLanguages;

    [ObservableProperty]
    private LanguageInfo _selectedLanguage = LocalizationService.SupportedLanguages[0];

    partial void OnSelectedLanguageChanged(LanguageInfo value)
    {
        LocalizationService.Instance.SetLanguage(value.Code);
        SaveLanguagePreference(value.Code);
    }

    private void LoadLanguagePreference()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "language.txt");
            if (File.Exists(path))
            {
                var code = File.ReadAllText(path).Trim();
                var lang = SupportedLanguages.FirstOrDefault(l => l.Code == code);
                if (lang != null)
                {
                    _selectedLanguage = lang;
                    LocalizationService.Instance.SetLanguage(code);
                }
            }
        }
        catch { }
    }

    private static void SaveLanguagePreference(string code)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "language.txt"), code);
        }
        catch { }
    }

    /// <summary>All models from the catalog with their install/active status.</summary>
    public ObservableCollection<LlmModelDisplayItem> AvailableModels { get; } = [];

    /// <summary>The currently loaded (active) model.</summary>
    [ObservableProperty]
    private LlmModelDisplayItem? _currentModel;

    /// <summary>The model selected in the dropdown for swapping.</summary>
    [ObservableProperty]
    private LlmModelDisplayItem? _selectedModel;

    /// <summary>Whether any model download is in progress.</summary>
    [ObservableProperty]
    private bool _isDownloading;

    /// <summary>Overall download progress (0-100).</summary>
    [ObservableProperty]
    private double _downloadProgress;

    /// <summary>Human-readable download status text.</summary>
    [ObservableProperty]
    private string _downloadStatusText = "";

    /// <summary>Status message shown in the model management section.</summary>
    [ObservableProperty]
    private string _modelStatusText = "";

    public SettingsViewModel(AppSettings appSettings, ILlmService? llmService = null, LlmModelManager? modelManager = null)
    {
        _appSettings = appSettings;
        _llmService = llmService;
        _modelManager = modelManager;
        Title = "Settings";

        LoadFromSettings();
        LoadModelCatalog();
        LoadLanguagePreference();
    }

    private void LoadFromSettings()
    {
        // General
        StartWithWindows = _appSettings.General.StartWithWindows;
        MinimizeToTray = _appSettings.General.MinimizeToTray;
        NotificationSounds = _appSettings.General.NotificationSounds;

        // Scanner
        RealTimeProtection = _appSettings.Scanner.RealTimeProtection;
        ScanExclusions = string.Join(Environment.NewLine, _appSettings.Scanner.ScanExclusions);

        // Firewall
        DefaultFirewallAction = _appSettings.Firewall.DefaultAction;
        LoggingVerbosity = _appSettings.Firewall.LoggingVerbosity;

        // LLM
        ModelPath = _appSettings.Llm.ModelPath;
        ContextSize = _appSettings.Llm.ContextSize;
        GpuLayers = _appSettings.Llm.GpuLayers;
        Temperature = _appSettings.Llm.Temperature;

        // LLM System Prompt
        LoadSystemPrompt();

        // Security
        EnableRemoteAccessDetection = _appSettings.Security.EnableRemoteAccessDetection;
        EnableBadUsbDetection = _appSettings.Security.EnableBadUsbDetection;
        EnableAiDetection = _appSettings.Security.EnableAiDetection;
        EnableRegistryWatcher = _appSettings.Security.EnableRegistryWatcher;
    }

    private void LoadSystemPrompt()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "llm_personality.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("systemPrompt", out var sp))
                {
                    SystemPrompt = sp.GetString() ?? "";
                }
            }
        }
        catch { /* Use default empty */ }
    }

    private void SaveSystemPrompt()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "llm_personality.json");

            var data = new { systemPrompt = SystemPrompt };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Scans the local LLM folder and populates the AvailableModels collection.
    /// </summary>
    private void LoadModelCatalog()
    {
        AvailableModels.Clear();

        var llmRoot = ResolveLlmRoot();
        var catalog = LlmModelInfo.GetCatalog();
        var currentModelPath = _appSettings.Llm.ModelPath;

        foreach (var model in catalog)
        {
            var item = new LlmModelDisplayItem { Info = model };

            // Check if the model file exists on disk
            item.IsInstalled = IsModelFilePresent(llmRoot, model);

            // Check if this is the currently active model
            if (_llmService?.IsModelLoaded == true && IsActiveModel(llmRoot, model, currentModelPath))
            {
                item.IsActive = true;
                CurrentModel = item;
            }

            AvailableModels.Add(item);
        }

        // If no model is marked active but the service has a model loaded, try to identify it
        if (CurrentModel == null && _llmService?.IsModelLoaded == true)
        {
            // Default to first installed model
            var firstInstalled = AvailableModels.FirstOrDefault(m => m.IsInstalled && !m.Info.IsEmbeddingModel);
            if (firstInstalled != null)
            {
                firstInstalled.IsActive = true;
                CurrentModel = firstInstalled;
            }
        }

        // Set selected model to current
        SelectedModel = CurrentModel ?? AvailableModels.FirstOrDefault(m => m.IsInstalled && !m.Info.IsEmbeddingModel);

        var installedCount = AvailableModels.Count(m => m.IsInstalled);
        ModelStatusText = $"{installedCount} of {AvailableModels.Count} models installed";
    }

    private static bool IsModelFilePresent(string llmRoot, LlmModelInfo model)
    {
        if (string.IsNullOrEmpty(llmRoot)) return false;

        // Check subfolder layout: LLM/{FolderName}/{FileName}
        if (!string.IsNullOrEmpty(model.FolderName))
        {
            var path = Path.Combine(llmRoot, model.FolderName, model.FileName);
            if (File.Exists(path)) return true;
        }

        // Check flat layout: LLM/{FileName}
        var flatPath = Path.Combine(llmRoot, model.FileName);
        return File.Exists(flatPath);
    }

    private static bool IsActiveModel(string llmRoot, LlmModelInfo model, string currentModelPath)
    {
        // If the settings model path contains this model's folder or filename, it's the active one
        if (!string.IsNullOrEmpty(currentModelPath))
        {
            if (currentModelPath.Contains(model.FolderName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (currentModelPath.Contains(model.FileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Default model check: if the default model's folder is found in the model directory path
        if (model.IsDefault)
        {
            var defaultPath = Path.Combine(llmRoot, model.FolderName, model.FileName);
            if (File.Exists(defaultPath)) return true;
        }

        return false;
    }

    private string ResolveLlmRoot()
    {
        // Use the configured model path if it points to a directory with an LLM subfolder
        if (!string.IsNullOrEmpty(_appSettings.Llm.ModelPath) && Directory.Exists(_appSettings.Llm.ModelPath))
        {
            var llmSub = Path.Combine(_appSettings.Llm.ModelPath, "LLM");
            if (Directory.Exists(llmSub)) return llmSub;
            return _appSettings.Llm.ModelPath;
        }

        // Walk up from binary to find LLM folder
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var llmDir = Path.Combine(dir, "LLM");
            if (Directory.Exists(llmDir)) return llmDir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "LLM");
    }

    /// <summary>
    /// Swap to the selected model: unload current, update path, reload.
    /// </summary>
    [RelayCommand]
    private async Task SwapModelAsync()
    {
        if (SelectedModel == null || !SelectedModel.IsInstalled || SelectedModel.Info.IsEmbeddingModel)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("Select an installed chat model to swap to.");
            return;
        }

        if (SelectedModel == CurrentModel)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("That model is already active.");
            return;
        }

        ModelStatusText = $"Swapping to {SelectedModel.Info.DisplayName}...";

        try
        {
            // Unload current model
            _modelManager?.Unload();

            // Update the active flags
            if (CurrentModel != null)
                CurrentModel.IsActive = false;

            // Update the model path in settings to point to the new model's directory
            var llmRoot = ResolveLlmRoot();
            var newModelDir = !string.IsNullOrEmpty(SelectedModel.Info.FolderName)
                ? Path.Combine(llmRoot, SelectedModel.Info.FolderName)
                : llmRoot;

            // Update settings - set ModelPath to the parent of LLM (project root)
            // since LlmModelManager.FindModelFile searches from the root
            var projectRoot = Directory.GetParent(llmRoot)?.FullName ?? llmRoot;
            ModelPath = projectRoot;
            _appSettings.Llm.ModelPath = projectRoot;
            _appSettings.SaveToFile();

            // Reset failure state and reload
            _modelManager?.ResetFailure();

            // Load the new model
            if (_llmService != null)
            {
                var progress = new Progress<double>(p =>
                {
                    ModelStatusText = p switch
                    {
                        < 0.1 => "Initializing model parameters...",
                        < 0.8 => $"Loading model weights... ({p * 100:F0}%)",
                        < 1.0 => "Creating inference context...",
                        _ => "Ready!"
                    };
                });

                await _llmService.LoadModelAsync(progress);
            }

            SelectedModel.IsActive = true;
            CurrentModel = SelectedModel;
            ModelStatusText = $"{SelectedModel.Info.DisplayName} loaded successfully.";

            await AvatarViewModel.Instance.ShowSpeechBubble($"Switched to {SelectedModel.Info.DisplayName}.");
        }
        catch (Exception ex)
        {
            ModelStatusText = $"Failed to swap model: {ex.Message}";
            await AvatarViewModel.Instance.ShowSpeechBubble($"Model swap failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Download a model from the server.
    /// </summary>
    [RelayCommand]
    private async Task DownloadModelAsync(LlmModelDisplayItem? model)
    {
        if (model == null || model.IsInstalled || model.IsDownloading) return;

        var serverUrl = _appSettings.Client.ServerUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(serverUrl))
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("No server URL configured. Set it in Client settings.");
            return;
        }

        model.IsDownloading = true;
        model.DownloadProgress = 0;
        model.DownloadStatusText = "Connecting to server...";
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = $"Downloading {model.Info.DisplayName}...";

        _downloadCts = new CancellationTokenSource();

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };

            // First get the file size
            var sizeUrl = $"{serverUrl}/api/v1/llm/models/{model.Info.Id}/size";
            long totalBytes = model.Info.SizeBytes; // fallback

            try
            {
                var sizeResponse = await httpClient.GetStringAsync(sizeUrl, _downloadCts.Token);
                using var sizeDoc = JsonDocument.Parse(sizeResponse);
                if (sizeDoc.RootElement.TryGetProperty("bytes", out var bytesEl))
                    totalBytes = bytesEl.GetInt64();
            }
            catch { /* Use catalog size as fallback */ }

            // Download the model file
            var downloadUrl = $"{serverUrl}/api/v1/llm/models/{model.Info.Id}/download";
            using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, _downloadCts.Token);
            response.EnsureSuccessStatusCode();

            // Ensure target directory exists
            var llmRoot = ResolveLlmRoot();
            var targetDir = !string.IsNullOrEmpty(model.Info.FolderName)
                ? Path.Combine(llmRoot, model.Info.FolderName)
                : llmRoot;
            Directory.CreateDirectory(targetDir);

            var targetPath = Path.Combine(targetDir, model.Info.FileName);
            var tempPath = targetPath + ".downloading";

            await using var contentStream = await response.Content.ReadAsStreamAsync(_downloadCts.Token);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var lastProgressUpdate = DateTime.UtcNow;

            while ((bytesRead = await contentStream.ReadAsync(buffer, _downloadCts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _downloadCts.Token);
                totalRead += bytesRead;

                // Throttle progress updates to avoid UI spam
                if ((DateTime.UtcNow - lastProgressUpdate).TotalMilliseconds > 250)
                {
                    var percent = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;
                    var downloadedMb = totalRead / (1024.0 * 1024.0);
                    var totalMb = totalBytes / (1024.0 * 1024.0);

                    model.DownloadProgress = percent;
                    model.DownloadStatusText = $"{downloadedMb:F1} / {totalMb:F1} MB ({percent:F0}%)";
                    DownloadProgress = percent;
                    DownloadStatusText = $"Downloading {model.Info.DisplayName}: {percent:F0}%";
                    lastProgressUpdate = DateTime.UtcNow;
                }
            }

            // Rename temp file to final name
            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(tempPath, targetPath);

            model.IsInstalled = true;
            model.DownloadProgress = 100;
            model.DownloadStatusText = "Download complete";
            DownloadStatusText = $"{model.Info.DisplayName} downloaded successfully.";
            ModelStatusText = $"{AvailableModels.Count(m => m.IsInstalled)} of {AvailableModels.Count} models installed";

            await AvatarViewModel.Instance.ShowSpeechBubble($"{model.Info.DisplayName} downloaded successfully.");
        }
        catch (OperationCanceledException)
        {
            model.DownloadStatusText = "Download cancelled";
            DownloadStatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            model.DownloadStatusText = $"Failed: {ex.Message}";
            DownloadStatusText = $"Download failed: {ex.Message}";
            await AvatarViewModel.Instance.ShowSpeechBubble($"Download failed: {ex.Message}");
        }
        finally
        {
            model.IsDownloading = false;
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    /// <summary>
    /// Cancel an in-progress download.
    /// </summary>
    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    /// <summary>
    /// Delete a model from disk. Cannot delete the only installed model or the active model.
    /// </summary>
    [RelayCommand]
    private async Task DeleteModelAsync(LlmModelDisplayItem? model)
    {
        if (model == null || !model.IsInstalled) return;

        if (model.IsActive)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("Cannot delete the active model. Swap to a different model first.");
            return;
        }

        if (model.Info.IsEmbeddingModel)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("Cannot delete the embedding model. It is required for knowledge base features.");
            return;
        }

        // Check if this is the only installed chat model
        var installedChatModels = AvailableModels.Count(m => m.IsInstalled && !m.Info.IsEmbeddingModel);
        if (installedChatModels <= 1)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble("Cannot delete the only installed chat model.");
            return;
        }

        try
        {
            var llmRoot = ResolveLlmRoot();

            // Try to delete the model file
            if (!string.IsNullOrEmpty(model.Info.FolderName))
            {
                var folderPath = Path.Combine(llmRoot, model.Info.FolderName);
                var filePath = Path.Combine(folderPath, model.Info.FileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);

                // Delete the folder if it's now empty
                if (Directory.Exists(folderPath) && !Directory.EnumerateFileSystemEntries(folderPath).Any())
                    Directory.Delete(folderPath);
            }
            else
            {
                var filePath = Path.Combine(llmRoot, model.Info.FileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            model.IsInstalled = false;
            ModelStatusText = $"{AvailableModels.Count(m => m.IsInstalled)} of {AvailableModels.Count} models installed";

            await AvatarViewModel.Instance.ShowSpeechBubble($"{model.Info.DisplayName} deleted.");
        }
        catch (Exception ex)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble($"Failed to delete model: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh the model catalog by re-scanning the LLM folder.
    /// </summary>
    [RelayCommand]
    private void RefreshModels()
    {
        LoadModelCatalog();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        // General
        _appSettings.General.StartWithWindows = StartWithWindows;
        _appSettings.General.MinimizeToTray = MinimizeToTray;
        _appSettings.General.NotificationSounds = NotificationSounds;

        // Scanner
        _appSettings.Scanner.RealTimeProtection = RealTimeProtection;
        _appSettings.Scanner.ScanExclusions = ScanExclusions
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        // Firewall
        _appSettings.Firewall.DefaultAction = DefaultFirewallAction;
        _appSettings.Firewall.LoggingVerbosity = LoggingVerbosity;

        // LLM
        _appSettings.Llm.ModelPath = ModelPath;
        _appSettings.Llm.ContextSize = ContextSize;
        _appSettings.Llm.GpuLayers = GpuLayers;
        _appSettings.Llm.Temperature = Temperature;

        // Security
        _appSettings.Security.EnableRemoteAccessDetection = EnableRemoteAccessDetection;
        _appSettings.Security.EnableBadUsbDetection = EnableBadUsbDetection;
        _appSettings.Security.EnableAiDetection = EnableAiDetection;
        _appSettings.Security.EnableRegistryWatcher = EnableRegistryWatcher;

        // Persist to disk
        _appSettings.SaveToFile();

        // Save system prompt separately
        SaveSystemPrompt();

        // Update Windows startup registry
        UpdateStartupRegistry(StartWithWindows);

        await AvatarViewModel.Instance.ShowSpeechBubble("Settings saved.");
    }

    private static void UpdateStartupRegistry(bool enable)
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "SGL SyntheticAI";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access may fail without admin rights - silently continue
        }
    }

    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        var defaults = new AppSettings();

        // General
        StartWithWindows = defaults.General.StartWithWindows;
        MinimizeToTray = defaults.General.MinimizeToTray;
        NotificationSounds = defaults.General.NotificationSounds;

        // Scanner
        RealTimeProtection = defaults.Scanner.RealTimeProtection;
        ScanExclusions = string.Empty;

        // Firewall
        DefaultFirewallAction = defaults.Firewall.DefaultAction;
        LoggingVerbosity = defaults.Firewall.LoggingVerbosity;

        // LLM
        ModelPath = defaults.Llm.ModelPath;
        ContextSize = defaults.Llm.ContextSize;
        GpuLayers = defaults.Llm.GpuLayers;
        Temperature = defaults.Llm.Temperature;

        // Security
        EnableRemoteAccessDetection = defaults.Security.EnableRemoteAccessDetection;
        EnableBadUsbDetection = defaults.Security.EnableBadUsbDetection;
        EnableAiDetection = defaults.Security.EnableAiDetection;
        EnableRegistryWatcher = defaults.Security.EnableRegistryWatcher;

        await AvatarViewModel.Instance.ShowSpeechBubble("Settings reset to defaults.");
    }

    [RelayCommand]
    private void ShutdownApp()
    {
        System.Windows.Application.Current?.Shutdown();
    }
}
