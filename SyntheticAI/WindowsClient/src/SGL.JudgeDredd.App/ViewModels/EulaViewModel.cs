using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services.Auth;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class EulaViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _eulaText = Services.Auth.EulaText.FullText;

    [ObservableProperty]
    private bool _hasScrolledToBottom;

    [ObservableProperty]
    private bool _hasAcceptedEula;

    public event EventHandler? Accepted;
    public event EventHandler? Declined;

    public EulaViewModel()
    {
        Title = "End-User License Agreement";
    }

    [RelayCommand]
    private void Accept()
    {
        if (HasAcceptedEula)
        {
            Accepted?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Decline()
    {
        Declined?.Invoke(this, EventArgs.Empty);
    }
}
