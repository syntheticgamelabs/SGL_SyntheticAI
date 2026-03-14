using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private readonly MobileLlmService _llmService;

    public const string VersionString = "Mobile V3.5.0";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _versionDisplay = VersionString;

    public LoginViewModel(ApiClient apiClient, AppPreferences prefs, MobileLlmService llmService)
    {
        _apiClient = apiClient;
        _prefs = prefs;
        _llmService = llmService;

        // Restore saved preferences
        ServerUrl = _prefs.ServerUrl;
        RememberMe = _prefs.RememberMe;

        if (_prefs.RememberMe && !string.IsNullOrEmpty(_prefs.Username))
        {
            Username = _prefs.Username;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Please enter your username and password.");
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            // Update server URL if changed
            if (_prefs.ServerUrl != ServerUrl)
            {
                _prefs.ServerUrl = ServerUrl;
                _apiClient.RefreshClient();
            }

            var (success, message) = await _apiClient.LoginAsync(Username, Password);

            if (success)
            {
                _prefs.RememberMe = RememberMe;
                if (RememberMe)
                {
                    _prefs.Username = Username;
                }

                // Check if LLM models are downloaded, prompt if first time
                await CheckAndPromptLlmDownloadAsync();

                // Navigate to main app shell
                if (Application.Current != null)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            }
            else
            {
                ShowError(message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Login failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Please enter a username and password to register.");
            return;
        }

        if (Password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            // Update server URL if changed
            if (_prefs.ServerUrl != ServerUrl)
            {
                _prefs.ServerUrl = ServerUrl;
                _apiClient.RefreshClient();
            }

            string machineName = DeviceInfo.Current.Name;
            string platform = DeviceInfo.Current.Platform.ToString();
            string deviceModel = DeviceInfo.Current.Model;
            string osVersion = DeviceInfo.Current.VersionString;

            var (success, message) = await _apiClient.RegisterAsync(
                Username, Password, machineName, platform, deviceModel, osVersion);

            if (success)
            {
                _prefs.RememberMe = RememberMe;
                if (RememberMe)
                {
                    _prefs.Username = Username;
                }

                // Check if LLM models are downloaded, prompt if first time
                await CheckAndPromptLlmDownloadAsync();

                // Navigate to main app shell
                if (Application.Current != null)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            }
            else
            {
                ShowError(message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Registration failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckAndPromptLlmDownloadAsync()
    {
        try
        {
            // Skip if user has already seen the prompt or has models downloaded
            if (_prefs.HasSeenLlmPrompt && _llmService.GetDownloadedModels().Count > 0)
                return;

            var downloadedModels = _llmService.GetDownloadedModels();
            if (downloadedModels.Count > 0)
            {
                _prefs.HasSeenLlmPrompt = true;
                return;
            }

            // Get device RAM to make recommendations
            long deviceRamMb = 0;
#if ANDROID
            try
            {
                var activityManager = Android.App.Application.Context.GetSystemService(
                    Android.Content.Context.ActivityService) as Android.App.ActivityManager;
                if (activityManager != null)
                {
                    var memInfo = new Android.App.ActivityManager.MemoryInfo();
                    activityManager.GetMemoryInfo(memInfo);
                    deviceRamMb = memInfo.TotalMem / (1024 * 1024);
                }
            }
            catch { deviceRamMb = 4096; }
#endif

            string recommendation;
            if (deviceRamMb >= 8192)
                recommendation = "Your device has plenty of RAM. We recommend a 4B parameter model for best results.";
            else if (deviceRamMb >= 4096)
                recommendation = "Your device has moderate RAM. A smaller model (1-2B parameters) is recommended.";
            else
                recommendation = "Your device has limited RAM. We recommend using Server mode instead of offline models.";

            var page = Application.Current?.Windows[0].Page;
            if (page == null) return;

            var result = await page.DisplayAlert(
                "Download AI Model?",
                $"No local AI models found on this device.\n\n" +
                $"Device RAM: {(deviceRamMb > 0 ? $"{deviceRamMb:N0} MB" : "Unknown")}\n" +
                $"{recommendation}\n\n" +
                "You can download models now from Settings > AI Models, or use Server mode to connect to the SyntheticAI server.\n\n" +
                "Would you like to go to Settings to download a model?",
                "Go to Settings",
                "Skip (Use Server Mode)");

            _prefs.HasSeenLlmPrompt = true;

            if (result)
            {
                // Will navigate to settings after AppShell loads
                _prefs.UseOfflineMode = false;
            }
        }
        catch
        {
            // Non-critical - don't block login
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}
