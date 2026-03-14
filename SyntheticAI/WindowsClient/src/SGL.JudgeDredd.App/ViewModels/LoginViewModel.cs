using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services.Auth;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly AutoLoginService _autoLoginService;

    [ObservableProperty]
    private string _username = string.Empty;

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _successMessage = string.Empty;

    [ObservableProperty]
    private bool _hasSuccess;

    [ObservableProperty]
    private bool _isLoggingIn;

    public event EventHandler<UserAccount>? LoginSuccessful;
    public event EventHandler? NavigateToRegister;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
        _autoLoginService = new AutoLoginService();
        Title = "Login";
    }

    public void ShowSuccess(string message)
    {
        SuccessMessage = message;
        HasSuccess = true;
        HasError = false;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        HasError = false;
        HasSuccess = false;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Please enter your username.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your password.";
            HasError = true;
            return;
        }

        IsLoggingIn = true;

        try
        {
            var (success, message) = await _authService.LoginAsync(Username, Password);

            if (success)
            {
                HasError = false;
                // Save credentials for auto-login on next startup
                _autoLoginService.SaveCredentials(Username, Password);
                LoginSuccessful?.Invoke(this, _authService.CurrentUser!);
            }
            else
            {
                ErrorMessage = message;
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private void NavigateToRegisterAction()
    {
        NavigateToRegister?.Invoke(this, EventArgs.Empty);
    }
}
