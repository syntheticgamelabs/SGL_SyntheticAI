using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private readonly MobileLlmService _llmService;
    private Timer? _statusTimer;
    private bool _hasCheckedLlm;

    [ObservableProperty]
    private string _connectionStatus = "Checking...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _serverVersion = "Unknown";

    [ObservableProperty]
    private int _onlineClients;

    [ObservableProperty]
    private string _signatureVersion = "Unknown";

    [ObservableProperty]
    private int _signatureCount;

    [ObservableProperty]
    private string _lastScanTime = "Never";

    [ObservableProperty]
    private int _threatsBlocked;

    [ObservableProperty]
    private bool _protectionEnabled = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private bool _llmAvailable;

    [ObservableProperty]
    private string _llmModelName = "None";

    [ObservableProperty]
    private string _llmLocalStatus = string.Empty;

    [ObservableProperty]
    private bool _showLlmDownloadPrompt;

    // Admin broadcast message
    [ObservableProperty]
    private string _broadcastMessage = string.Empty;

    [ObservableProperty]
    private string _broadcastPriority = "info";

    [ObservableProperty]
    private bool _hasBroadcast;

    [ObservableProperty]
    private string _broadcastSentBy = string.Empty;

    private string? _lastDismissedBroadcast;

    public DashboardViewModel(ApiClient apiClient, AppPreferences prefs, MobileLlmService llmService)
    {
        _apiClient = apiClient;
        _prefs = prefs;
        _llmService = llmService;

        Username = _prefs.Username;
        ServerUrl = _prefs.ServerUrl;
        LastScanTime = _prefs.LastScanTime;
        ThreatsBlocked = _prefs.ThreatsBlocked;

        _apiClient.ConnectionStatusChanged += (_, status) =>
        {
            ConnectionStatus = status;
            IsConnected = status == "Connected";
        };

        // Show local LLM status
        if (_llmService.IsModelDownloaded)
        {
            LlmLocalStatus = $"Local: {_llmService.CurrentModelName}";
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        IsBusy = true;

        try
        {
            var status = await _apiClient.GetServerStatusAsync();
            if (status != null)
            {
                ConnectionStatus = "Connected";
                IsConnected = true;
                ServerVersion = status.ServerVersion;
                OnlineClients = status.OnlineClients;
                SignatureVersion = status.SignatureVersion;
                SignatureCount = status.SignatureCount;
                LlmAvailable = status.LlmModelLoaded;
                LlmModelName = status.LlmModelName;
            }
            else
            {
                ConnectionStatus = "Disconnected";
                IsConnected = false;
            }

            // Check for admin broadcast message
            var broadcast = await _apiClient.GetBroadcastAsync();
            if (broadcast != null && !string.IsNullOrWhiteSpace(broadcast.Message))
            {
                BroadcastMessage = broadcast.Message;
                BroadcastPriority = broadcast.Priority;
                BroadcastSentBy = broadcast.SentBy;
                HasBroadcast = true;

                // Show popup alert if this is a new broadcast the user hasn't dismissed
                if (_lastDismissedBroadcast != broadcast.Message)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var title = broadcast.Priority?.ToUpperInvariant() == "CRITICAL"
                            ? "CRITICAL Admin Broadcast"
                            : "Admin Broadcast";
                        await Shell.Current.DisplayAlert(title,
                            $"{broadcast.Message}\n\n- {broadcast.SentBy}", "OK");
                    });
                    _lastDismissedBroadcast = broadcast.Message;
                }
            }
            else
            {
                BroadcastMessage = string.Empty;
                HasBroadcast = false;
            }
        }
        catch
        {
            ConnectionStatus = "Error";
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task QuickScanAsync()
    {
        await Shell.Current.GoToAsync("//scanner");
    }

    [RelayCommand]
    private void ToggleProtection()
    {
        ProtectionEnabled = !ProtectionEnabled;

        if (ProtectionEnabled)
        {
            _apiClient.StartHeartbeat(TimeSpan.FromMinutes(5));
        }
        else
        {
            _apiClient.StopHeartbeat();
        }
    }

    [RelayCommand]
    private async Task GoToChatAsync()
    {
        await Shell.Current.GoToAsync("//chat");
    }

    [RelayCommand]
    private async Task GoToLlmDownloadAsync()
    {
        ShowLlmDownloadPrompt = false;
        await Shell.Current.GoToAsync("//settings");
    }

    [RelayCommand]
    private void DismissBroadcast()
    {
        _lastDismissedBroadcast = BroadcastMessage;
        HasBroadcast = false;
    }

    /// <summary>
    /// Check if the user needs to download an LLM model on first launch.
    /// </summary>
    private async Task CheckLlmFirstLaunchAsync()
    {
        if (_hasCheckedLlm || _llmService.IsModelDownloaded)
        {
            _hasCheckedLlm = true;
            if (_llmService.IsModelDownloaded)
                LlmLocalStatus = $"Local: {_llmService.CurrentModelName}";
            return;
        }

        _hasCheckedLlm = true;

        // Only prompt if connected
        if (!IsConnected) return;

        var models = await _apiClient.GetAvailableModelsDetailedAsync();
        var availableModels = models.Where(m => m.IsAvailableOnServer && !m.IsEmbeddingModel).ToList();

        if (availableModels.Count > 0)
        {
            LlmLocalStatus = "No local LLM model - download one for offline threat analysis";
            ShowLlmDownloadPrompt = true;
        }
        else
        {
            LlmLocalStatus = "Using server LLM for analysis";
        }
    }

    public void OnAppearing()
    {
        LastScanTime = _prefs.LastScanTime;
        ThreatsBlocked = _prefs.ThreatsBlocked;

        _ = RefreshStatusAsync().ContinueWith(async _ =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await CheckLlmFirstLaunchAsync();
            });
        });

        _statusTimer?.Dispose();
        _statusTimer = new Timer(async _ =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshStatusAsync();
            });
        }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        if (ProtectionEnabled)
        {
            _apiClient.StartHeartbeat(TimeSpan.FromMinutes(5));
        }
    }

    public void OnDisappearing()
    {
        _statusTimer?.Dispose();
        _statusTimer = null;
    }
}
