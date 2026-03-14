using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services
            .GetService<LoginViewModel>()
            ?? new LoginViewModel(
                new Services.ApiClient(new Models.AppPreferences()),
                new Models.AppPreferences(),
                new Services.MobileLlmService(new Services.ApiClient(new Models.AppPreferences()), new Services.ThreatKnowledgeService()));
    }
}
