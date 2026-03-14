using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using SGL.JudgeDredd.Mobile.ViewModels;
using SGL.JudgeDredd.Mobile.Views;
using ZXing.Net.Maui.Controls;

namespace SGL.JudgeDredd.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register core services
        builder.Services.AddSingleton<AppPreferences>();
        builder.Services.AddSingleton<ApiClient>();

        // Register new services
        builder.Services.AddSingleton<ThreatKnowledgeService>();
        builder.Services.AddSingleton<BluetoothService>();
        builder.Services.AddSingleton<WifiScannerService>();
        builder.Services.AddSingleton<TelemetryService>();
        builder.Services.AddSingleton<DeviceProtectionService>();
        builder.Services.AddSingleton<GitService>();
        builder.Services.AddSingleton<MobileLlmService>();

        // Register ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<ScanViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<BluetoothViewModel>();
        builder.Services.AddSingleton<WifiScannerViewModel>();
        builder.Services.AddSingleton<TelemetryViewModel>();
        builder.Services.AddSingleton<DeviceProtectionViewModel>();
        builder.Services.AddTransient<QrShareViewModel>();
        builder.Services.AddSingleton<GitViewModel>();
        builder.Services.AddTransient<FaqViewModel>();
        builder.Services.AddSingleton<BroadcastViewModel>();

        // Register Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<BluetoothPage>();
        builder.Services.AddTransient<WifiScannerPage>();
        builder.Services.AddTransient<TelemetryPage>();
        builder.Services.AddTransient<DeviceProtectionPage>();
        builder.Services.AddTransient<QrSharePage>();
        builder.Services.AddTransient<GitPage>();
        builder.Services.AddTransient<FaqPage>();
        builder.Services.AddTransient<BroadcastPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
