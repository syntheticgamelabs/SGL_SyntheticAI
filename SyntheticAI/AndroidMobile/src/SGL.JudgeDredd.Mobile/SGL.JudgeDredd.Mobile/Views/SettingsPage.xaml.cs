using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private SettingsViewModel? _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_viewModel == null)
        {
            _viewModel = Handler?.MauiContext?.Services.GetService<SettingsViewModel>();
            if (_viewModel != null)
            {
                BindingContext = _viewModel;
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel?.OnAppearing();
    }
}
