namespace SGL.JudgeDredd.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("login", typeof(Views.LoginPage));
        Routing.RegisterRoute("bluetooth", typeof(Views.BluetoothPage));
        Routing.RegisterRoute("wifi", typeof(Views.WifiScannerPage));
        Routing.RegisterRoute("telemetry", typeof(Views.TelemetryPage));
        Routing.RegisterRoute("protection", typeof(Views.DeviceProtectionPage));
        Routing.RegisterRoute("qrshare", typeof(Views.QrSharePage));
        Routing.RegisterRoute("git", typeof(Views.GitPage));
        Routing.RegisterRoute("broadcast", typeof(Views.BroadcastPage));
        Routing.RegisterRoute("faq", typeof(Views.FaqPage));
    }
}
