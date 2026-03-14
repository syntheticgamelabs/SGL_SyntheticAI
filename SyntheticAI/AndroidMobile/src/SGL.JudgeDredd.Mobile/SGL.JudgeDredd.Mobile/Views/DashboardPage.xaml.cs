using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel? _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_viewModel == null)
        {
            _viewModel = Handler?.MauiContext?.Services.GetService<DashboardViewModel>();
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel?.OnDisappearing();
    }
}
