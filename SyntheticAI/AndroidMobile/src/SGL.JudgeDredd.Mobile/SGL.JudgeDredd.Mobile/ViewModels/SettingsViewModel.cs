using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using SGL.JudgeDredd.Shared.Localization;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private readonly MobileLlmService _llmService;
    private CancellationTokenSource? _downloadCts;

    public const string VersionString = "ABV 1.1.38";

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private bool _ttsEnabled;

    [ObservableProperty]
    private string _selectedLlmModel = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _versionDisplay = VersionString;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private LlmModelListItem? _selectedDownloadModel;

    [ObservableProperty]
    private string _selectedModelDescription = string.Empty;

    public ObservableCollection<LlmModelListItem> AvailableModels { get; } = new();
    public ObservableCollection<string> DownloadedModels { get; } = new();

    // AI Chat Settings
    [ObservableProperty]
    private string _aiAssistantName = string.Empty;

    [ObservableProperty]
    private string _aiSystemPrompt = string.Empty;

    [ObservableProperty]
    private bool _aiCreativeMode;

    // Language selection
    public List<string> SupportedLanguages { get; } = LocalizationService.SupportedLanguages
        .Select(l => l.ToString()).ToList();

    [ObservableProperty]
    private string _selectedLanguage = LocalizationService.SupportedLanguages
        .FirstOrDefault(l => l.Code == LocalizationService.Instance.CurrentLanguage)?.ToString()
        ?? LocalizationService.SupportedLanguages[0].ToString();

    public SettingsViewModel(ApiClient apiClient, AppPreferences prefs, MobileLlmService llmService)
    {
        _apiClient = apiClient;
        _prefs = prefs;
        _llmService = llmService;

        ServerUrl = _prefs.ServerUrl;
        Username = _prefs.Username;
        NotificationsEnabled = _prefs.NotificationsEnabled;
        TtsEnabled = _prefs.TtsEnabled;
        SelectedLlmModel = _prefs.SelectedLlmModel;
        AiAssistantName = _prefs.AiAssistantName;
        AiSystemPrompt = _prefs.AiSystemPrompt;
        AiCreativeMode = _prefs.AiCreativeMode;

        RefreshDownloadedModels();
    }

    partial void OnSelectedDownloadModelChanged(LlmModelListItem? value)
    {
        if (value != null)
        {
            SelectedModelDescription = $"{value.Description}\n" +
                $"Size: {value.SizeDisplay} | Params: {value.ParameterCount}B | Quant: {value.Quantization}\n" +
                $"RAM Required: {value.RamRequiredMB} MB | Best for: {value.BestFor}";
        }
        else
        {
            SelectedModelDescription = string.Empty;
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var langInfo = LocalizationService.SupportedLanguages
            .FirstOrDefault(l => l.ToString() == value);
        if (langInfo != null)
        {
            LocalizationService.Instance.SetLanguage(langInfo.Code);
        }
    }

    private void RefreshDownloadedModels()
    {
        DownloadedModels.Clear();
        foreach (var model in _llmService.GetDownloadedModels())
        {
            DownloadedModels.Add(model);
        }
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading available models from server...";

        try
        {
            var models = await _apiClient.GetAvailableModelsDetailedAsync();
            AvailableModels.Clear();

            int availableCount = 0;
            foreach (var model in models.Where(m => !m.IsEmbeddingModel))
            {
                AvailableModels.Add(model);
                if (model.IsAvailableOnServer) availableCount++;
            }

            if (AvailableModels.Count > 0)
            {
                StatusMessage = $"Found {availableCount} downloadable model(s) of {AvailableModels.Count} in catalog";

                // Auto-select default or recommended model if none selected
                if (SelectedDownloadModel == null)
                {
                    SelectedDownloadModel = AvailableModels.FirstOrDefault(m => m.IsDefault && m.IsAvailableOnServer)
                        ?? AvailableModels.FirstOrDefault(m => m.IsRecommended && m.IsAvailableOnServer)
                        ?? AvailableModels.FirstOrDefault(m => m.IsAvailableOnServer);
                }
            }
            else
            {
                StatusMessage = "No models found in server catalog.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading models: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadModelAsync()
    {
        if (SelectedDownloadModel == null)
        {
            StatusMessage = "Select a model to download first.";
            return;
        }

        if (!SelectedDownloadModel.IsAvailableOnServer)
        {
            StatusMessage = "This model is not available on the server.";
            return;
        }

        if (IsDownloading) return;

        var modelId = SelectedDownloadModel.Id;
        var modelName = SelectedDownloadModel.DisplayName;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatus = $"Starting download of {modelName}...";
        _downloadCts = new CancellationTokenSource();

        try
        {
            // Get the file size first for progress tracking
            long totalSize = await _apiClient.GetModelSizeAsync(modelId);
            if (totalSize <= 0)
                totalSize = SelectedDownloadModel.SizeBytes;

            DownloadStatus = $"Downloading {modelName} ({SelectedDownloadModel.SizeDisplay})...";

            // Get the download stream with content length
            var (stream, contentLength) = await _apiClient.DownloadModelStreamWithSizeAsync(modelId);
            if (stream == null)
            {
                DownloadStatus = "Download failed - could not connect to server.";
                StatusMessage = "Model download failed.";
                return;
            }

            if (contentLength > 0) totalSize = contentLength;

            // Download with progress tracking
            var modelDir = Path.Combine(FileSystem.AppDataDirectory, "llm_models");
            Directory.CreateDirectory(modelDir);
            var modelPath = Path.Combine(modelDir, SelectedDownloadModel.FileName);

            await using var fileStream = File.Create(modelPath);
            var buffer = new byte[65536];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, _downloadCts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _downloadCts.Token);
                totalRead += bytesRead;

                var mb = totalRead / (1024.0 * 1024.0);
                var totalMb = totalSize / (1024.0 * 1024.0);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (totalSize > 0)
                    {
                        DownloadProgress = (double)totalRead / totalSize;
                        DownloadStatus = $"Downloading... {mb:F1} / {totalMb:F0} MB ({DownloadProgress:P0})";
                    }
                    else
                    {
                        DownloadStatus = $"Downloading... {mb:F1} MB";
                    }
                });
            }

            stream.Dispose();

            DownloadProgress = 1.0;
            DownloadStatus = $"Downloaded: {modelName} ({totalRead / (1024 * 1024)} MB)";
            StatusMessage = "Model downloaded successfully! You can now use it for threat analysis.";

            // Refresh the LLM service to detect the new model
            _llmService.SwitchModel(Path.GetFileNameWithoutExtension(SelectedDownloadModel.FileName));
            RefreshDownloadedModels();
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelled.";
            StatusMessage = "Download was cancelled.";
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Download failed: {ex.Message}";
            StatusMessage = $"Download error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts = null;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsBusy = true;

        try
        {
            bool serverUrlChanged = _prefs.ServerUrl != ServerUrl;
            _prefs.ServerUrl = ServerUrl;
            _prefs.NotificationsEnabled = NotificationsEnabled;
            _prefs.TtsEnabled = TtsEnabled;
            _prefs.SelectedLlmModel = SelectedLlmModel;
            _prefs.AiAssistantName = AiAssistantName;
            _prefs.AiSystemPrompt = AiSystemPrompt;
            _prefs.AiCreativeMode = AiCreativeMode;

            if (serverUrlChanged)
            {
                _apiClient.RefreshClient();
            }

            StatusMessage = "Settings saved successfully";

            var connected = await _apiClient.TestConnectionAsync();
            if (connected)
            {
                StatusMessage = "Settings saved. Server connection verified.";
            }
            else
            {
                StatusMessage = "Settings saved, but server connection failed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Testing connection...";

        try
        {
            if (_prefs.ServerUrl != ServerUrl)
            {
                _prefs.ServerUrl = ServerUrl;
                _apiClient.RefreshClient();
            }

            var status = await _apiClient.GetServerStatusAsync();
            if (status != null)
            {
                StatusMessage = $"Connected! Server v{status.ServerVersion}, " +
                               $"{status.OnlineClients} client(s) online, " +
                               $"{status.SignatureCount} signatures";
            }
            else
            {
                StatusMessage = "Could not connect to server.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShutdownAppAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Shut Down Protection",
            "Are you sure you want to shut down SyntheticAI?\n\nThis will stop background protection and close the app.",
            "Shut Down", "Cancel");

        if (!confirm) return;

        _apiClient.StopHeartbeat();

#if ANDROID
        // Stop the foreground service
        var context = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (context != null)
        {
            var stopIntent = new global::Android.Content.Intent(context,
                typeof(Platforms.Android.JudgeDreddForegroundService));
            context.StopService(stopIntent);
            context.FinishAffinity();
        }
        Java.Lang.JavaSystem.Exit(0);
#else
        Application.Current?.Quit();
#endif
    }

    [RelayCommand]
    private async Task GoToFaqAsync()
    {
        await Shell.Current.GoToAsync("//faq");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Logout",
            "Are you sure you want to log out?",
            "Yes", "Cancel");

        if (!confirm) return;

        _apiClient.StopHeartbeat();
        _prefs.ClearAuth();
        _prefs.RememberMe = false;

        if (Application.Current != null)
        {
            Application.Current.Windows[0].Page = new Views.LoginPage();
        }
    }

    public void OnAppearing()
    {
        ServerUrl = _prefs.ServerUrl;
        Username = _prefs.Username;
        RefreshDownloadedModels();
        _ = LoadModelsAsync();
    }
}
