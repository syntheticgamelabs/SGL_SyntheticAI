namespace SGL.JudgeDredd.Mobile;

public partial class App : Application
{
    public const string Version = "ABV 1.1.38";

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var prefs = new Models.AppPreferences();
        bool isLoggedIn = !string.IsNullOrEmpty(prefs.AuthToken) && prefs.RememberMe;

        var window = isLoggedIn
            ? new Window(new AppShell())
            : new Window(new Views.LoginPage());

        return window;
    }
}
