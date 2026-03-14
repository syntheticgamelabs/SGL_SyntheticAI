using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class VpnViewModel : ViewModelBase
{
    private const string VpnConnectionName = "SGL-SAI-VPN";
    private const int MaxHops = 3;

    private readonly HttpClient _httpClient = new();
    private readonly HttpClient _serverHttpClient;
    private readonly AppSettings _appSettings;
    private readonly System.Timers.Timer _durationTimer;
    private DateTime _connectionStartTime;
    private System.Threading.Timer? _statsTimer;
    private long _initialBytesSent;
    private long _initialBytesReceived;

    [ObservableProperty]
    private VpnServer? _selectedServer;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _publicIpAddress = "Checking...";

    [ObservableProperty]
    private string _vpnProtocol = "IKEv2";

    [ObservableProperty]
    private bool _killSwitchEnabled;

    [ObservableProperty]
    private bool _dnsLeakProtectionEnabled;

    [ObservableProperty]
    private string _bytesSent = "0 B";

    [ObservableProperty]
    private string _bytesReceived = "0 B";

    [ObservableProperty]
    private string _connectionDuration = "00:00:00";

    [ObservableProperty]
    private string _dnsServer = "1.1.1.1";

    // ── Multi-Hop ──────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _multiHopEnabled;

    [ObservableProperty]
    private string _multiHopStatus = "Select up to 3 servers for multi-hop routing";

    // ── ID Masking ─────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _ipMaskingEnabled;

    [ObservableProperty]
    private bool _hardwareIdMaskingEnabled;

    [ObservableProperty]
    private bool _firmwareIdMaskingEnabled;

    [ObservableProperty]
    private string _maskedIpAddress = "Not active";

    [ObservableProperty]
    private string _maskedHardwareId = "Not active";

    [ObservableProperty]
    private string _maskedFirmwareId = "Not active";

    [ObservableProperty]
    private string _realHardwareId = "Reading...";

    [ObservableProperty]
    private string _realFirmwareId = "Reading...";

    public ObservableCollection<VpnServer> Servers { get; } = [];
    public ObservableCollection<VpnServer> SelectedHops { get; } = [];
    public ObservableCollection<string> AvailableProtocols { get; } = [];
    public ObservableCollection<string> ConnectionLog { get; } = [];

    public VpnViewModel(AppSettings appSettings)
    {
        Title = "VPN";
        _appSettings = appSettings;

        // Configure HttpClient for server API calls
        var serverUrl = appSettings.IsClientMode
            ? appSettings.Client.ServerUrl
            : $"http://localhost:{appSettings.Server.Port}";

        _serverHttpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Set auth token if available in client mode
        if (appSettings.IsClientMode && !string.IsNullOrEmpty(appSettings.Client.ApiKey))
        {
            _serverHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {appSettings.Client.ApiKey}");
        }

        AvailableProtocols.Add("IKEv2");
        AvailableProtocols.Add("L2TP");
        AvailableProtocols.Add("PPTP");
        AvailableProtocols.Add("SSTP");

        _durationTimer = new System.Timers.Timer(1000);
        _durationTimer.Elapsed += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _connectionStartTime;
            App.Current?.Dispatcher.Invoke(() =>
            {
                ConnectionDuration = elapsed.ToString(@"hh\:mm\:ss");
            });
        };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RefreshServersAsync();
        await RefreshIpAsync();
        await ReadSystemIdsAsync();
    }

    // ── Multi-Hop Management ───────────────────────────────────────────

    [RelayCommand]
    private void AddToHopRoute()
    {
        if (SelectedServer is null)
        {
            AddLog("Select a server first to add to the hop route.");
            return;
        }

        if (SelectedHops.Count >= MaxHops)
        {
            AddLog($"Maximum {MaxHops} hops allowed. Remove a server first.");
            return;
        }

        if (SelectedHops.Contains(SelectedServer))
        {
            AddLog($"{SelectedServer.Name} is already in the hop route.");
            return;
        }

        SelectedHops.Add(SelectedServer);
        MultiHopStatus = $"Route: {string.Join(" → ", SelectedHops.Select(h => h.Country))}";
        AddLog($"Added {SelectedServer.Name} to hop route ({SelectedHops.Count}/{MaxHops}).");
    }

    [RelayCommand]
    private void RemoveFromHopRoute(VpnServer? server)
    {
        if (server is null) return;

        SelectedHops.Remove(server);
        MultiHopStatus = SelectedHops.Count > 0
            ? $"Route: {string.Join(" → ", SelectedHops.Select(h => h.Country))}"
            : "Select up to 3 servers for multi-hop routing";
        AddLog($"Removed {server.Name} from hop route.");
    }

    [RelayCommand]
    private void ClearHopRoute()
    {
        SelectedHops.Clear();
        MultiHopStatus = "Select up to 3 servers for multi-hop routing";
        AddLog("Hop route cleared.");
    }

    // ── System ID Reading ──────────────────────────────────────────────

    private async Task ReadSystemIdsAsync()
    {
        try
        {
            var hwId = await RunProcessAsync("powershell.exe",
                "-Command \"(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Cryptography' -Name MachineGuid).MachineGuid\"");
            RealHardwareId = string.IsNullOrWhiteSpace(hwId) ? "Unable to read" : hwId.Trim();

            var fwId = await RunProcessAsync("powershell.exe",
                "-Command \"(Get-WmiObject -Class Win32_BIOS).SerialNumber\"");
            RealFirmwareId = string.IsNullOrWhiteSpace(fwId) ? "Unable to read" : fwId.Trim();
        }
        catch (Exception ex)
        {
            RealHardwareId = "Error reading";
            RealFirmwareId = "Error reading";
            AddLog($"System ID read error: {ex.Message}");
        }
    }

    // ── ID Masking ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleIpMaskingAsync()
    {
        IpMaskingEnabled = !IpMaskingEnabled;

        if (IpMaskingEnabled)
        {
            MaskedIpAddress = "Detecting...";
            var realIp = await GetPublicIpAsync();
            MaskedIpAddress = realIp;
            AddLog($"IP masking ENABLED. Current public IP: {MaskedIpAddress}");
            await AvatarViewModel.Instance.ShowSpeechBubble("IP address detected. Connect to VPN to change it.");
        }
        else
        {
            MaskedIpAddress = "Not active";
            AddLog("IP masking DISABLED.");
        }
    }

    [RelayCommand]
    private async Task ToggleHardwareIdMaskingAsync()
    {
        HardwareIdMaskingEnabled = !HardwareIdMaskingEnabled;

        if (HardwareIdMaskingEnabled)
        {
            var spoofedId = Guid.NewGuid().ToString();
            MaskedHardwareId = spoofedId;

            try
            {
                await RunProcessAsync("powershell.exe",
                    $"-Command \"New-Item -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Force | Out-Null; " +
                    $"Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Name 'SpoofedHardwareId' -Value '{spoofedId}'\"");

                AddLog($"Hardware ID masking ENABLED. Spoofed HWID: {spoofedId[..8]}...");
                await AvatarViewModel.Instance.ShowSpeechBubble("Hardware ID masked.");
            }
            catch (Exception ex)
            {
                AddLog($"Hardware ID masking error: {ex.Message}");
                HardwareIdMaskingEnabled = false;
            }
        }
        else
        {
            MaskedHardwareId = "Not active";
            try
            {
                await RunProcessAsync("powershell.exe",
                    "-Command \"Remove-ItemProperty -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Name 'SpoofedHardwareId' -ErrorAction SilentlyContinue\"");
            }
            catch { /* cleanup best-effort */ }
            AddLog("Hardware ID masking DISABLED.");
        }
    }

    [RelayCommand]
    private async Task ToggleFirmwareIdMaskingAsync()
    {
        FirmwareIdMaskingEnabled = !FirmwareIdMaskingEnabled;

        if (FirmwareIdMaskingEnabled)
        {
            var rng = new Random();
            var spoofedFw = $"SGL-{rng.Next(100000, 999999)}-{rng.Next(1000, 9999)}";
            MaskedFirmwareId = spoofedFw;

            try
            {
                await RunProcessAsync("powershell.exe",
                    $"-Command \"New-Item -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Force | Out-Null; " +
                    $"Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Name 'SpoofedFirmwareId' -Value '{spoofedFw}'\"");

                AddLog($"Firmware ID masking ENABLED. Spoofed FWID: {spoofedFw}");
                await AvatarViewModel.Instance.ShowSpeechBubble("Firmware ID masked.");
            }
            catch (Exception ex)
            {
                AddLog($"Firmware ID masking error: {ex.Message}");
                FirmwareIdMaskingEnabled = false;
            }
        }
        else
        {
            MaskedFirmwareId = "Not active";
            try
            {
                await RunProcessAsync("powershell.exe",
                    "-Command \"Remove-ItemProperty -Path 'HKCU:\\SOFTWARE\\SGL-SAI' -Name 'SpoofedFirmwareId' -ErrorAction SilentlyContinue\"");
            }
            catch { /* cleanup best-effort */ }
            AddLog("Firmware ID masking DISABLED.");
        }
    }

    // ── Live Server List (Server API → VPN Gate fallback) ──────────────

    [RelayCommand]
    private async Task RefreshServersAsync()
    {
        try
        {
            IsRefreshing = true;

            // 1. Try fetching from SGL server API first
            var loadedFromServer = await TryFetchFromServerApiAsync();

            // 2. Fall back to direct VPN Gate API if server is unreachable
            if (!loadedFromServer)
            {
                await FetchFromVpnGateDirectAsync();
            }
        }
        catch (Exception ex)
        {
            AddLog($"Failed to refresh servers: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Attempts to fetch the VPN server list from the SGL server API.
    /// Returns true if successful, false if server is unreachable.
    /// </summary>
    private async Task<bool> TryFetchFromServerApiAsync()
    {
        try
        {
            AddLog("Fetching server list from SGL server API...");
            var response = await _serverHttpClient.GetAsync("/api/v1/vpn/servers");

            if (!response.IsSuccessStatusCode)
            {
                AddLog($"Server API returned {(int)response.StatusCode}. Falling back to VPN Gate.");
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("servers", out var serversElement))
            {
                AddLog("Server API response missing 'servers' field. Falling back to VPN Gate.");
                return false;
            }

            var newServers = new List<VpnServer>();
            foreach (var entry in serversElement.EnumerateArray())
            {
                var hostname = entry.TryGetProperty("hostname", out var h) ? h.GetString() ?? "" : "";
                var ip = entry.TryGetProperty("ipAddress", out var ipProp) ? ipProp.GetString() ?? "" : "";
                var country = entry.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "";
                var name = entry.TryGetProperty("name", out var n) ? n.GetString() : null;
                var speed = entry.TryGetProperty("speed", out var sp) ? sp.GetInt64() : 0;
                var source = entry.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "";
                var username = entry.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
                var password = entry.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(hostname) && string.IsNullOrWhiteSpace(ip))
                    continue;

                var displayName = name ?? $"{country} - {hostname}";
                if (source == "nordvpn")
                    displayName = $"[Nord] {displayName}";
                else if (source == "custom")
                    displayName = $"[Custom] {displayName}";

                newServers.Add(new VpnServer
                {
                    Name = displayName,
                    Hostname = !string.IsNullOrEmpty(ip) ? ip : hostname,
                    Country = country,
                    Username = username,
                    Password = password,
                    Speed = speed
                });
            }

            if (newServers.Count > 0)
            {
                Servers.Clear();
                foreach (var srv in newServers.OrderByDescending(s => s.Speed).Take(50))
                    Servers.Add(srv);
                AddLog($"Loaded {Servers.Count} servers from SGL server API.");
                return true;
            }

            AddLog("Server API returned empty list. Falling back to VPN Gate.");
            return false;
        }
        catch (Exception ex)
        {
            AddLog($"Server API unreachable: {ex.Message}. Falling back to VPN Gate.");
            return false;
        }
    }

    /// <summary>
    /// Direct VPN Gate API fetch — used as fallback when the SGL server is unavailable.
    /// </summary>
    private async Task FetchFromVpnGateDirectAsync()
    {
        try
        {
            AddLog("Fetching live server list from VPN Gate (fallback)...");
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            var csv = await http.GetStringAsync("https://www.vpngate.net/api/iphone/");
            var lines = csv.Split('\n').Skip(2); // Skip header lines

            var newServers = new List<VpnServer>();
            foreach (var line in lines)
            {
                var cols = line.Split(',');
                if (cols.Length < 15) continue;

                var hostname = cols[0]; // HostName
                var ip = cols[1]; // IP
                var country = cols[5]; // CountryShort
                var countryLong = cols[6]; // CountryLong
                var speed = long.TryParse(cols[4], out var s) ? s : 0; // Speed bps

                // Only add servers with valid data and speed
                if (!string.IsNullOrEmpty(hostname) && speed > 0)
                {
                    newServers.Add(new VpnServer
                    {
                        Name = $"{countryLong} - {hostname}",
                        Hostname = ip, // Use IP directly for rasdial
                        Country = countryLong,
                        Username = "vpn",
                        Password = "vpn",
                        Speed = speed
                    });
                }
            }

            if (newServers.Count > 0)
            {
                Servers.Clear();
                foreach (var srv in newServers.OrderByDescending(s => s.Speed).Take(20))
                    Servers.Add(srv);
                AddLog($"Loaded {Servers.Count} servers from VPN Gate (sorted by speed).");
            }
            else
            {
                AddLog("No servers returned from VPN Gate. Keeping existing list.");
            }
        }
        catch (Exception ex)
        {
            AddLog($"VPN Gate fallback also failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Admin-only: Triggers server-side refresh from online sources (VPN Gate + NordVPN API).
    /// After refresh, re-fetches the updated list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshServerListFromSourcesAsync()
    {
        try
        {
            IsRefreshing = true;
            AddLog("Requesting server-side VPN list refresh (admin)...");

            var response = await _serverHttpClient.PostAsync("/api/v1/vpn/servers/refresh", null);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                AddLog("Server refresh requires admin privileges.");
                await AvatarViewModel.Instance.ShowSpeechBubble("Admin access required to refresh server list.");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                AddLog($"Server refresh failed: HTTP {(int)response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Refresh completed";
            var total = doc.RootElement.TryGetProperty("totalCount", out var tc) ? tc.GetInt32() : 0;

            AddLog($"{msg} — {total} servers available.");

            // Re-fetch the updated list
            await RefreshServersAsync();
        }
        catch (Exception ex)
        {
            AddLog($"Server refresh error: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    // ── Real Public IP Detection ──────────────────────────────────────

    private async Task<string> GetPublicIpAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            var ip = await http.GetStringAsync("https://api.ipify.org");
            return ip.Trim();
        }
        catch
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                var ip = await http.GetStringAsync("https://checkip.amazonaws.com");
                return ip.Trim();
            }
            catch { return "Unknown"; }
        }
    }

    // ── Connection ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var servers = MultiHopEnabled && SelectedHops.Count > 0
            ? SelectedHops.ToList()
            : SelectedServer is not null ? new List<VpnServer> { SelectedServer } : new List<VpnServer>();

        if (servers.Count == 0)
        {
            AddLog("No server selected. Select a server or configure multi-hop route.");
            await AvatarViewModel.Instance.ShowSpeechBubble("Select a VPN server first!");
            return;
        }

        if (IsConnected)
        {
            AddLog("Already connected. Disconnect first.");
            return;
        }

        IsConnecting = true;
        var routeDescription = string.Join(" → ", servers.Select(s => s.Name));
        ConnectionStatus = $"Connecting: {routeDescription}...";
        AddLog($"Establishing VPN route: {routeDescription} via {VpnProtocol}...");

        try
        {
            for (int i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                var hopName = servers.Count > 1 ? $"{VpnConnectionName}-Hop{i + 1}" : VpnConnectionName;

                AddLog($"Hop {i + 1}/{servers.Count}: Connecting to {server.Name} ({server.Hostname})...");

                var addConnectionArgs = $"-Command \"Add-VpnConnection -Name '{hopName}' " +
                                        $"-ServerAddress '{server.Hostname}' " +
                                        $"-TunnelType {VpnProtocol} " +
                                        $"-AuthenticationMethod MSChapv2 -Force\"";

                var addResult = await RunProcessAsync("powershell.exe", addConnectionArgs);
                AddLog($"VPN entry '{hopName}' created. {addResult}");

                var dialArgs = $"\"{hopName}\"";
                if (!string.IsNullOrEmpty(server.Username))
                    dialArgs += $" {server.Username} {server.Password}";
                var dialResult = await RunProcessAsync("rasdial.exe", dialArgs);
                AddLog($"rasdial [{hopName}]: {dialResult}");

                // Verify rasdial actually succeeded before continuing
                if (!dialResult.Contains("successfully", StringComparison.OrdinalIgnoreCase) &&
                    !dialResult.Contains("Command completed successfully", StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"Hop {i + 1} failed: {dialResult}");
                    // Disconnect any partial connections from this hop
                    await RunProcessAsync("rasdial.exe", $"\"{hopName}\" /disconnect");
                    throw new Exception($"Connection to {server.Name} failed: {dialResult}");
                }
            }

            if (DnsLeakProtectionEnabled)
            {
                var dnsResult = await RunProcessAsync("netsh.exe",
                    $"interface ip set dns \"{VpnConnectionName}\" static {DnsServer}");
                AddLog($"DNS set to {DnsServer}: {dnsResult}");
            }

            IsConnected = true;
            IsConnecting = false;
            ConnectionStatus = MultiHopEnabled && servers.Count > 1
                ? $"Multi-hop: {routeDescription}"
                : $"Connected via {servers[0].Name}";
            _connectionStartTime = DateTime.UtcNow;
            _durationTimer.Start();

            // Capture initial byte counters and start traffic stats polling
            CaptureInitialTrafficCounters();
            _statsTimer?.Dispose();
            _statsTimer = new System.Threading.Timer(UpdateTrafficStats, null, 0, 1000);

            AddLog($"VPN route established: {routeDescription}");
            await AvatarViewModel.Instance.ShowSpeechBubble(
                MultiHopEnabled ? $"Multi-hop VPN active through {servers.Count} nodes!" : $"VPN connected to {servers[0].Name}.");

            await RefreshIpAsync();

            // Update masked IP with real VPN-assigned IP
            if (IpMaskingEnabled)
            {
                MaskedIpAddress = await GetPublicIpAsync();
                AddLog($"VPN IP detected: {MaskedIpAddress}");
            }
        }
        catch (Exception ex)
        {
            IsConnecting = false;
            IsConnected = false;
            ConnectionStatus = "Connection failed";
            _durationTimer.Stop();

            // Stop stats timer if it was started
            _statsTimer?.Dispose();
            _statsTimer = null;

            // Clean up any partially established connections
            var hopNames = new[] { VpnConnectionName, $"{VpnConnectionName}-Hop1", $"{VpnConnectionName}-Hop2", $"{VpnConnectionName}-Hop3" };
            foreach (var hopName in hopNames)
            {
                try
                {
                    await RunProcessAsync("rasdial.exe", $"\"{hopName}\" /disconnect");
                    await RunProcessAsync("powershell.exe",
                        $"-Command \"Remove-VpnConnection -Name '{hopName}' -Force -ErrorAction SilentlyContinue\"");
                }
                catch { /* cleanup best-effort */ }
            }

            AddLog($"Connection error: {ex.Message}");
            await AvatarViewModel.Instance.ShowSpeechBubble("VPN connection failed.");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!IsConnected) { AddLog("Not connected."); return; }

        AddLog("Disconnecting all VPN hops...");

        try
        {
            var hopNames = new[] { VpnConnectionName, $"{VpnConnectionName}-Hop1", $"{VpnConnectionName}-Hop2", $"{VpnConnectionName}-Hop3" };

            foreach (var hopName in hopNames)
            {
                try
                {
                    await RunProcessAsync("rasdial.exe", $"\"{hopName}\" /disconnect");
                    await RunProcessAsync("powershell.exe",
                        $"-Command \"Remove-VpnConnection -Name '{hopName}' -Force -ErrorAction SilentlyContinue\"");
                }
                catch { /* some hops may not exist */ }
            }

            IsConnected = false;
            ConnectionStatus = "Disconnected";
            _durationTimer.Stop();
            _statsTimer?.Dispose();
            _statsTimer = null;
            ConnectionDuration = "00:00:00";
            BytesSent = "0 B";
            BytesReceived = "0 B";

            AddLog("All VPN connections terminated.");
            await AvatarViewModel.Instance.ShowSpeechBubble("VPN disconnected.");
            await RefreshIpAsync();
        }
        catch (Exception ex)
        {
            AddLog($"Disconnect error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshIpAsync()
    {
        try
        {
            PublicIpAddress = "Checking...";
            var ip = await _httpClient.GetStringAsync("https://api.ipify.org");
            PublicIpAddress = ip.Trim();
            AddLog($"Public IP: {PublicIpAddress}");
        }
        catch (Exception ex)
        {
            PublicIpAddress = "Unable to determine";
            AddLog($"IP check failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ToggleKillSwitchAsync()
    {
        KillSwitchEnabled = !KillSwitchEnabled;
        try
        {
            if (KillSwitchEnabled)
            {
                var result = await RunProcessAsync("netsh.exe",
                    "advfirewall firewall add rule name=\"SGL-SAI-KillSwitch\" dir=out action=block");
                AddLog($"Kill switch ENABLED. {result}");
            }
            else
            {
                var result = await RunProcessAsync("netsh.exe",
                    "advfirewall firewall delete rule name=\"SGL-SAI-KillSwitch\"");
                AddLog($"Kill switch DISABLED. {result}");
            }
        }
        catch (Exception ex)
        {
            AddLog($"Kill switch error: {ex.Message}");
            KillSwitchEnabled = !KillSwitchEnabled;
        }
    }

    [RelayCommand]
    private async Task ToggleDnsLeakProtectionAsync()
    {
        DnsLeakProtectionEnabled = !DnsLeakProtectionEnabled;
        try
        {
            if (DnsLeakProtectionEnabled)
            {
                await RunProcessAsync("netsh.exe",
                    $"interface ip set dns \"Local Area Connection\" static {DnsServer}");
                AddLog($"DNS leak protection ENABLED. DNS: {DnsServer}.");
            }
            else
            {
                await RunProcessAsync("netsh.exe",
                    "interface ip set dns \"Local Area Connection\" dhcp");
                AddLog("DNS leak protection DISABLED.");
            }
        }
        catch (Exception ex)
        {
            AddLog($"DNS leak protection error: {ex.Message}");
            DnsLeakProtectionEnabled = !DnsLeakProtectionEnabled;
        }
    }

    [RelayCommand]
    private async Task PingServersAsync()
    {
        AddLog("Pinging all servers...");
        using var pinger = new Ping();

        foreach (var server in Servers)
        {
            try
            {
                var reply = await pinger.SendPingAsync(server.Hostname, 3000);
                if (reply.Status == IPStatus.Success)
                {
                    server.Latency = (int)reply.RoundtripTime;
                    server.IsAvailable = true;
                    AddLog($"  {server.Name}: {server.Latency} ms");
                }
                else
                {
                    server.Latency = -1;
                    server.IsAvailable = false;
                    AddLog($"  {server.Name}: unreachable ({reply.Status})");
                }
            }
            catch (Exception ex)
            {
                server.Latency = -1;
                server.IsAvailable = false;
                AddLog($"  {server.Name}: ping failed ({ex.Message})");
            }
        }
        AddLog("Server ping complete.");
    }

    // ── Traffic Stats Polling ───────────────────────────────────────────

    private void CaptureInitialTrafficCounters()
    {
        try
        {
            var vpnInterface = FindVpnNetworkInterface();
            if (vpnInterface != null)
            {
                var stats = vpnInterface.GetIPv4Statistics();
                _initialBytesSent = stats.BytesSent;
                _initialBytesReceived = stats.BytesReceived;
            }
            else
            {
                _initialBytesSent = 0;
                _initialBytesReceived = 0;
            }
        }
        catch
        {
            _initialBytesSent = 0;
            _initialBytesReceived = 0;
        }
    }

    private NetworkInterface? FindVpnNetworkInterface()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        // Try to find by VPN connection name first
        var vpnInterface = interfaces.FirstOrDefault(i =>
            i.Name.Contains("SGL-SAI-VPN", StringComparison.OrdinalIgnoreCase));

        // Fall back to any active PPP interface (typical for rasdial connections)
        vpnInterface ??= interfaces.FirstOrDefault(i =>
            i.OperationalStatus == OperationalStatus.Up &&
            i.NetworkInterfaceType == NetworkInterfaceType.Ppp);

        return vpnInterface;
    }

    private void UpdateTrafficStats(object? state)
    {
        try
        {
            var vpnInterface = FindVpnNetworkInterface();
            if (vpnInterface != null)
            {
                var stats = vpnInterface.GetIPv4Statistics();
                var sent = stats.BytesSent - _initialBytesSent;
                var received = stats.BytesReceived - _initialBytesReceived;
                App.Current?.Dispatcher.Invoke(() =>
                {
                    BytesSent = FormatBytes(sent);
                    BytesReceived = FormatBytes(received);
                });
            }
        }
        catch { /* interface may have disappeared during disconnect */ }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private async Task<string> RunProcessAsync(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process();
        process.StartInfo = startInfo;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(error))
            AddLog($"[stderr] {error.Trim()}");

        return output.Trim();
    }

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ConnectionLog.Add(entry);
    }
}

public partial class VpnServer : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private string _country = string.Empty;

    [ObservableProperty]
    private int _latency;

    [ObservableProperty]
    private bool _isAvailable = true;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public long Speed { get; set; }
}
