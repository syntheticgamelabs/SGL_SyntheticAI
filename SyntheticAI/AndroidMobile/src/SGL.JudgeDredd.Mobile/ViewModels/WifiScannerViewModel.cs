using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

#if ANDROID
/// <summary>
/// Custom permission for Android 13+ NEARBY_WIFI_DEVICES runtime permission.
/// Required for WiFi scanning on API 33+.
/// </summary>
public class NearbyWifiDevicesPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new[] { ("android.permission.NEARBY_WIFI_DEVICES", true) };
}
#endif

public partial class WifiScannerViewModel : ObservableObject
{
    private readonly WifiScannerService _wifiService;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready to scan";
    [ObservableProperty] private WifiNetworkInfo? _selectedNetwork;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _isTracing;
    [ObservableProperty] private string _testResult = string.Empty;

    public ObservableCollection<WifiNetworkInfo> Networks { get; } = new();
    public ObservableCollection<string> TraceHops { get; } = new();

    public WifiScannerViewModel(WifiScannerService wifiService)
    {
        _wifiService = wifiService;
        _wifiService.StatusChanged += (_, s) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = s);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Requesting permissions...";
        Networks.Clear();

#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // Android 13+ requires NEARBY_WIFI_DEVICES instead of Location for WiFi scanning
            var nearbyStatus = await Permissions.CheckStatusAsync<NearbyWifiDevicesPermission>();
            if (nearbyStatus != PermissionStatus.Granted)
            {
                nearbyStatus = await Permissions.RequestAsync<NearbyWifiDevicesPermission>();
                if (nearbyStatus != PermissionStatus.Granted)
                {
                    StatusText = "WiFi scan requires Nearby Devices permission";
                    IsScanning = false;
                    return;
                }
            }
        }
        else
        {
            // Android 12 and below: Location permission required for WiFi scanning
            var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locStatus != PermissionStatus.Granted)
            {
                locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locStatus != PermissionStatus.Granted)
                {
                    StatusText = "WiFi scan requires Location permission";
                    IsScanning = false;
                    return;
                }
            }
        }
#else
        var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locStatus != PermissionStatus.Granted)
            locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
#endif

        StatusText = "Scanning WiFi networks...";
        var results = await _wifiService.ScanNetworksAsync();
        foreach (var net in results)
            Networks.Add(net);

        StatusText = Networks.Count > 0
            ? $"Found {Networks.Count} networks"
            : "No networks found. Ensure WiFi is enabled.";
        IsScanning = false;
    }

    [RelayCommand]
    private async Task TestConnectionAsync(WifiNetworkInfo? network)
    {
        if (network == null) return;
        IsTesting = true;
        SelectedNetwork = network;
        StatusText = $"Testing {network.Ssid}...";

        var result = await _wifiService.TestConnectionAsync(network);
        SelectedNetwork = result;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Network: {result.Ssid}");
        sb.AppendLine($"Security: {result.SecurityType} ({result.SecurityRating})");
        sb.AppendLine($"Latency: {(result.LatencyMs >= 0 ? $"{result.LatencyMs}ms" : "Failed")}");
        sb.AppendLine($"External IP: {result.ExternalIp}");
        sb.AppendLine($"Gateway: {result.Gateway}");
        sb.AppendLine($"DNS Server: {result.DnsServer}");
        TestResult = sb.ToString();

        StatusText = $"Test complete for {network.Ssid}";
        IsTesting = false;
    }

    [RelayCommand]
    private async Task TraceRouteAsync()
    {
        IsTracing = true;
        TraceHops.Clear();
        StatusText = "Running network trace...";

        var hops = await _wifiService.TraceRouteAsync();
        foreach (var hop in hops)
            TraceHops.Add(hop);

        StatusText = $"Trace complete: {TraceHops.Count} hops";
        IsTracing = false;
    }
}
