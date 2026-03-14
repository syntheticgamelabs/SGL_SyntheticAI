using SGL.JudgeDredd.Mobile.Models;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace SGL.JudgeDredd.Mobile.Services;

public class WifiScannerService
{
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Requests required WiFi scanning runtime permissions.
    /// Returns true if all permissions are granted, false otherwise.
    /// </summary>
    private async Task<bool> RequestPermissionsAsync()
    {
#if ANDROID
        // ACCESS_FINE_LOCATION is required for WiFi scanning on all Android versions
        var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locationStatus != PermissionStatus.Granted)
        {
            locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (locationStatus != PermissionStatus.Granted)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusChanged?.Invoke(this,
                        "Location permission denied. ACCESS_FINE_LOCATION is required for WiFi scanning."));
                return false;
            }
        }

        // Android 13+ (API 33) requires NEARBY_WIFI_DEVICES permission
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var nearbyStatus = await Permissions.CheckStatusAsync<NearbyWifiDevicesPermission>();
            if (nearbyStatus != PermissionStatus.Granted)
            {
                nearbyStatus = await Permissions.RequestAsync<NearbyWifiDevicesPermission>();
                if (nearbyStatus != PermissionStatus.Granted)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        StatusChanged?.Invoke(this,
                            "Nearby WiFi Devices permission denied. NEARBY_WIFI_DEVICES is required on Android 13+."));
                    return false;
                }
            }
        }
#endif
        return true;
    }

    public async Task<List<WifiNetworkInfo>> ScanNetworksAsync()
    {
        var networks = new List<WifiNetworkInfo>();

        // Request runtime permissions before scanning
        if (!await RequestPermissionsAsync())
            return networks;

        try
        {
#if ANDROID
            var wifiManager = (Android.Net.Wifi.WifiManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService);
            if (wifiManager == null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusChanged?.Invoke(this, "WiFi service not available."));
                return networks;
            }

            if (!wifiManager.IsWifiEnabled)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusChanged?.Invoke(this, "WiFi is disabled. Please enable WiFi."));
                return networks;
            }

            MainThread.BeginInvokeOnMainThread(() =>
                StatusChanged?.Invoke(this, "Scanning for WiFi networks..."));

            // Register a broadcast receiver to be notified when scan results are available
            var scanResultsReady = new TaskCompletionSource<bool>();
            var receiver = new WifiScanResultsReceiver(scanResultsReady);
            var intentFilter = new Android.Content.IntentFilter(
                Android.Net.Wifi.WifiManager.ScanResultsAvailableAction);

            Android.App.Application.Context.RegisterReceiver(receiver, intentFilter);

            try
            {
                // StartScan() is deprecated on Android 13+ (API 33) and throttled on Android 9+.
                // We still call it as a hint but rely on cached ScanResults regardless.
                if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    wifiManager.StartScan();
                }

                // Wait for the broadcast receiver callback or timeout after 10 seconds
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    var completedTask = await Task.WhenAny(
                        scanResultsReady.Task,
                        Task.Delay(10000, timeoutCts.Token));

                    if (completedTask == scanResultsReady.Task)
                    {
                        // Cancel the timeout since we got results
                        timeoutCts.Cancel();
                    }
                }
                catch (OperationCanceledException) { }
            }
            finally
            {
                try { Android.App.Application.Context.UnregisterReceiver(receiver); }
                catch { }
            }

            var results = wifiManager.ScanResults;
            if (results == null || results.Count == 0)
            {
                // Fallback: detect the currently connected WiFi network
                var fallbackNetwork = GetConnectedNetworkFallback(wifiManager);
                if (fallbackNetwork != null)
                {
                    networks.Add(fallbackNetwork);
                    MainThread.BeginInvokeOnMainThread(() =>
                        StatusChanged?.Invoke(this, "Full scan unavailable. Showing connected network."));
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        StatusChanged?.Invoke(this, "No scan results available. WiFi scanning may require Nearby Devices permission."));
                }
                return networks.Count > 0
                    ? networks.OrderByDescending(n => n.IsCurrentNetwork)
                              .ThenByDescending(n => n.SignalLevel).ToList()
                    : networks;
            }

            var connectionInfo = wifiManager.ConnectionInfo;
            var currentBssid = connectionInfo?.BSSID;

            foreach (var result in results)
            {
                var network = new WifiNetworkInfo
                {
                    Ssid = string.IsNullOrEmpty(result.Ssid) ? "<Hidden Network>" : result.Ssid,
                    Bssid = result.Bssid ?? "",
                    SignalLevel = result.Level,
                    Frequency = result.Frequency,
                    SecurityType = ParseSecurityType(result.Capabilities ?? ""),
                    IsCurrentNetwork = result.Bssid == currentBssid
                };

                network.SecurityRating = RateNetworkSecurity(network);
                networks.Add(network);
            }

            MainThread.BeginInvokeOnMainThread(() =>
                StatusChanged?.Invoke(this, $"Scan complete. Found {networks.Count} network(s)."));
#endif
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                StatusChanged?.Invoke(this, $"Scan error: {ex.Message}"));
        }

        return networks.OrderByDescending(n => n.IsCurrentNetwork)
                      .ThenByDescending(n => n.SignalLevel).ToList();
    }

#if ANDROID
    /// <summary>
    /// Fallback method to detect the currently connected WiFi network when
    /// full scanning is unavailable (e.g. Android 13+ without Nearby Devices permission).
    /// Uses ConnectivityManager and WifiManager to retrieve the active connection info.
    /// </summary>
    private WifiNetworkInfo? GetConnectedNetworkFallback(Android.Net.Wifi.WifiManager wifiManager)
    {
        try
        {
            var connectionInfo = wifiManager.ConnectionInfo;
            if (connectionInfo == null || connectionInfo.NetworkId == -1)
                return null;

            // SSID comes wrapped in quotes from WifiInfo
            var ssid = connectionInfo.SSID?.Trim('"') ?? "";
            if (string.IsNullOrEmpty(ssid) || ssid == "<unknown ssid>")
            {
                // Try ConnectivityManager on Android 10+
                var connectivityManager = (Android.Net.ConnectivityManager?)
                    Android.App.Application.Context.GetSystemService(Android.Content.Context.ConnectivityService);
                if (connectivityManager != null)
                {
                    var activeNetwork = connectivityManager.ActiveNetwork;
                    if (activeNetwork != null)
                    {
                        var capabilities = connectivityManager.GetNetworkCapabilities(activeNetwork);
                        if (capabilities != null)
                        {
                            var transportInfo = capabilities.TransportInfo;
                            if (transportInfo is Android.Net.Wifi.WifiInfo wifiInfo)
                            {
                                ssid = wifiInfo.SSID?.Trim('"') ?? "";
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(ssid) || ssid == "<unknown ssid>")
                return null;

            // Determine security type from current network capabilities
            string securityType = "Unknown";
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                try
                {
                    // Use raw integer constants since Android.Net.Wifi.SecurityType
                    // enum is not available in .NET 9 Android bindings.
                    // 0 = SECURITY_TYPE_OPEN, 1 = SECURITY_TYPE_WEP,
                    // 2 = SECURITY_TYPE_PSK,  3 = SECURITY_TYPE_EAP, 4 = SECURITY_TYPE_SAE
                    securityType = (int)connectionInfo.CurrentSecurityType switch
                    {
                        4 => "WPA3",
                        2 => "WPA2",
                        1 => "WEP",
                        0 => "Open",
                        3 => "WPA2-Enterprise",
                        _ => "Unknown"
                    };
                }
                catch { securityType = "Unknown"; }
            }

            var network = new WifiNetworkInfo
            {
                Ssid = ssid,
                Bssid = connectionInfo.BSSID ?? "",
                SignalLevel = connectionInfo.Rssi,
                Frequency = connectionInfo.Frequency,
                SecurityType = securityType,
                IsCurrentNetwork = true
            };

            network.SecurityRating = RateNetworkSecurity(network);
            return network;
        }
        catch
        {
            return null;
        }
    }
#endif

    private string ParseSecurityType(string capabilities)
    {
        if (capabilities.Contains("WPA3")) return "WPA3";
        if (capabilities.Contains("WPA2")) return "WPA2";
        if (capabilities.Contains("WPA")) return "WPA";
        if (capabilities.Contains("WEP")) return "WEP";
        if (capabilities.Contains("ESS") && !capabilities.Contains("WPA") && !capabilities.Contains("WEP"))
            return "Open";
        return "Unknown";
    }

    private string RateNetworkSecurity(WifiNetworkInfo network)
    {
        return network.SecurityType switch
        {
            "WPA3" => "Excellent",
            "WPA2" => "Good",
            "WPA" => "Fair - Consider upgrading",
            "WEP" => "Poor - Easily cracked",
            "Open" => "Dangerous - No encryption",
            _ => "Unknown"
        };
    }

    public async Task<WifiNetworkInfo> TestConnectionAsync(WifiNetworkInfo network)
    {
        MainThread.BeginInvokeOnMainThread(() =>
            StatusChanged?.Invoke(this, $"Testing {network.Ssid}..."));

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.GetAsync("https://www.google.com/generate_204");
            sw.Stop();
            network.LatencyMs = sw.ElapsedMilliseconds;
        }
        catch
        {
            network.LatencyMs = -1;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var ipResp = await client.GetStringAsync("https://api.ipify.org");
            network.ExternalIp = ipResp.Trim();
        }
        catch { network.ExternalIp = "Could not determine"; }

#if ANDROID
        try
        {
            var wifiManager = (Android.Net.Wifi.WifiManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService);
            if (wifiManager?.DhcpInfo != null)
            {
                var dhcp = wifiManager.DhcpInfo;
                network.Gateway = IntToIp(dhcp.Gateway);
                network.DnsServer = IntToIp(dhcp.Dns1);
            }
        }
        catch { }
#endif

        return network;
    }

    public async Task<List<string>> TraceRouteAsync(string host = "8.8.8.8", int maxHops = 15)
    {
        var hops = new List<string>();
        MainThread.BeginInvokeOnMainThread(() =>
            StatusChanged?.Invoke(this, "Running network trace..."));

        try
        {
            using var ping = new Ping();
            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                var options = new PingOptions(ttl, true);
                var reply = await ping.SendPingAsync(host, 3000, new byte[32], options);

                if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.Success)
                {
                    hops.Add($"Hop {ttl}: {reply.Address} ({reply.RoundtripTime}ms)");
                    if (reply.Status == IPStatus.Success) break;
                }
                else
                {
                    hops.Add($"Hop {ttl}: * * * (timeout)");
                }
            }
        }
        catch
        {
            // Fallback: use ping shell command when System.Net.Ping is unavailable (e.g., Android restrictions)
            hops.Clear();
            try
            {
                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "/system/bin/ping",
                        Arguments = $"-c 1 -t {ttl} -W 3 {host}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        hops.Add($"Hop {ttl}: * * * (ping unavailable)");
                        continue;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    // Parse the ping output for IP and time
                    var fromMatch = System.Text.RegularExpressions.Regex.Match(
                        output, @"from\s+([\d.]+).*time[=<]\s*([\d.]+)\s*ms");
                    if (fromMatch.Success)
                    {
                        hops.Add($"Hop {ttl}: {fromMatch.Groups[1].Value} ({fromMatch.Groups[2].Value}ms)");
                        // If we reached the destination, stop
                        if (fromMatch.Groups[1].Value == host)
                            break;
                    }
                    else
                    {
                        hops.Add($"Hop {ttl}: * * * (timeout)");
                    }
                }
            }
            catch (Exception ex)
            {
                hops.Add($"Trace failed: {ex.Message}");
            }
        }

        return hops;
    }

    private static string IntToIp(int ip)
    {
        return $"{ip & 0xFF}.{(ip >> 8) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 24) & 0xFF}";
    }
}

#if ANDROID
/// <summary>
/// BroadcastReceiver that listens for WiFi scan results becoming available.
/// Signals a TaskCompletionSource when results are ready.
/// </summary>
internal class WifiScanResultsReceiver : Android.Content.BroadcastReceiver
{
    private readonly TaskCompletionSource<bool> _scanResultsReady;

    public WifiScanResultsReceiver(TaskCompletionSource<bool> scanResultsReady)
    {
        _scanResultsReady = scanResultsReady;
    }

    public override void OnReceive(Android.Content.Context? context, Android.Content.Intent? intent)
    {
        if (intent?.Action == Android.Net.Wifi.WifiManager.ScanResultsAvailableAction)
        {
            _scanResultsReady.TrySetResult(true);
        }
    }
}

/// <summary>
/// Custom MAUI permission class for NEARBY_WIFI_DEVICES (Android 13+ / API 33+).
/// Also requests ACCESS_WIFI_STATE and CHANGE_WIFI_STATE for full WiFi scanning support.
/// </summary>
internal class NearbyWifiDevicesPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new (string androidPermission, bool isRuntime)[]
        {
            (Android.Manifest.Permission.NearbyWifiDevices, true),
            (Android.Manifest.Permission.AccessWifiState, true),
            (Android.Manifest.Permission.ChangeWifiState, true)
        };
}
#endif