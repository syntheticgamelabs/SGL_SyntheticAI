using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class TestSpeedViewModel : ViewModelBase
{
    [ObservableProperty] private double _downloadSpeed;
    [ObservableProperty] private double _uploadSpeed;
    [ObservableProperty] private double _pingLatency;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private string _testStatus = "Ready";
    [ObservableProperty] private double _testProgress;
    [ObservableProperty] private string _serverUsed = "\u2014";
    [ObservableProperty] private string _localIp = "\u2014";
    [ObservableProperty] private string _publicIp = "\u2014";
    [ObservableProperty] private string _isp = "\u2014";

    // Network Check
    [ObservableProperty] private bool _isCheckingNetwork;
    [ObservableProperty] private string _networkCheckStatus = "Ready";

    public ObservableCollection<SpeedSample> DownloadSamples { get; } = [];
    public ObservableCollection<SpeedSample> UploadSamples { get; } = [];
    public ObservableCollection<TraceHop> TraceResults { get; } = [];
    public ObservableCollection<string> NetworkLog { get; } = [];

    private CancellationTokenSource? _cts;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Real speed test endpoints (public CDN test files)
    private static readonly string[] DownloadTestUrls = new[]
    {
        "http://speedtest.tele2.net/10MB.zip",
        "http://proof.ovh.net/files/10Mb.dat",
        "http://speedtest.ftp.otenet.gr/files/test10Mb.db"
    };

    private static readonly string[] UploadTestUrls = new[]
    {
        "http://speedtest.tele2.net/upload.php",
        "https://httpbin.org/post"
    };

    public TestSpeedViewModel()
    {
        Title = "Test Speed";
        DetectLocalIp();
    }

    private void DetectLocalIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            var endpoint = socket.LocalEndPoint as IPEndPoint;
            LocalIp = endpoint?.Address.ToString() ?? "Unknown";
        }
        catch { LocalIp = "Unknown"; }
    }

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (IsTesting) return;
        _cts = new CancellationTokenSource();
        IsTesting = true;
        DownloadSpeed = 0;
        UploadSpeed = 0;
        PingLatency = 0;
        TestProgress = 0;
        DownloadSamples.Clear();
        UploadSamples.Clear();

        try
        {
            // Step 1: Detect public IP and ISP
            TestStatus = "Detecting connection info...";
            TestProgress = 5;
            await DetectPublicInfoAsync(_cts.Token);

            // Step 2: Ping test
            TestStatus = "Testing latency...";
            TestProgress = 15;
            await RunPingTestAsync(_cts.Token);

            // Step 3: Download test
            TestStatus = "Testing download speed...";
            await RunDownloadTestAsync(_cts.Token);

            // Step 4: Upload test
            TestStatus = "Testing upload speed...";
            await RunUploadTestAsync(_cts.Token);

            TestStatus = $"Complete \u2014 Down: {DownloadSpeed:F2} Mbps, Up: {UploadSpeed:F2} Mbps, Ping: {PingLatency:F0} ms";
            TestProgress = 100;
        }
        catch (OperationCanceledException)
        {
            TestStatus = "Test cancelled.";
        }
        catch (Exception ex)
        {
            TestStatus = $"Test error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void StopSpeedTest()
    {
        _cts?.Cancel();
    }

    private async Task DetectPublicInfoAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://ipinfo.io/json", ct);
            // Parse simple JSON fields
            var ipMatch = System.Text.RegularExpressions.Regex.Match(response, "\"ip\":\\s*\"([^\"]+)\"");
            var orgMatch = System.Text.RegularExpressions.Regex.Match(response, "\"org\":\\s*\"([^\"]+)\"");
            if (ipMatch.Success) PublicIp = ipMatch.Groups[1].Value;
            if (orgMatch.Success) Isp = orgMatch.Groups[1].Value;
        }
        catch { PublicIp = "Could not detect"; }
    }

    private async Task RunPingTestAsync(CancellationToken ct)
    {
        using var pinger = new Ping();
        double totalMs = 0;
        int success = 0;
        string[] targets = { "8.8.8.8", "1.1.1.1", "208.67.222.222" };

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var reply = await pinger.SendPingAsync(target, 3000);
                if (reply.Status == IPStatus.Success)
                {
                    totalMs += reply.RoundtripTime;
                    success++;
                }
            }
            catch { }
        }

        PingLatency = success > 0 ? totalMs / success : -1;
    }

    private async Task RunDownloadTestAsync(CancellationToken ct)
    {
        double bestSpeed = 0;

        foreach (var url in DownloadTestUrls)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ServerUsed = new Uri(url).Host;
                var sw = Stopwatch.StartNew();
                long totalBytes = 0;

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) continue;

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[81920];
                int bytesRead;
                var sampleSw = Stopwatch.StartNew();

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    totalBytes += bytesRead;
                    double elapsedSec = sw.Elapsed.TotalSeconds;
                    if (elapsedSec > 0)
                    {
                        double currentMbps = (totalBytes * 8.0) / (elapsedSec * 1_000_000);
                        DownloadSpeed = currentMbps;
                        TestProgress = 15 + Math.Min(40, (elapsedSec / 12.0) * 40);
                    }

                    if (sampleSw.ElapsedMilliseconds > 500)
                    {
                        DownloadSamples.Add(new SpeedSample { Time = sw.Elapsed.TotalSeconds, SpeedMbps = DownloadSpeed });
                        sampleSw.Restart();
                    }

                    if (sw.Elapsed.TotalSeconds > 12) break;
                }

                sw.Stop();
                double speed = (totalBytes * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000);
                if (speed > bestSpeed) bestSpeed = speed;
                break; // Use first working server
            }
            catch (Exception) { continue; }
        }

        DownloadSpeed = bestSpeed;
        TestProgress = 55;
    }

    private async Task RunUploadTestAsync(CancellationToken ct)
    {
        try
        {
            // Generate 2MB of random data for upload test
            var data = new byte[2 * 1024 * 1024];
            new Random().NextBytes(data);

            var sw = Stopwatch.StartNew();
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            ServerUsed = new Uri(UploadTestUrls[0]).Host;
            TestProgress = 60;

            // Time the upload
            var response = await _httpClient.PostAsync(UploadTestUrls[0], content, ct);
            sw.Stop();

            double uploadMbps = (data.Length * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000);
            UploadSpeed = uploadMbps;
            UploadSamples.Add(new SpeedSample { Time = sw.Elapsed.TotalSeconds, SpeedMbps = uploadMbps });
            TestProgress = 90;
        }
        catch
        {
            UploadSpeed = 0;
        }
    }

    [RelayCommand]
    private async Task CheckNetworkAsync()
    {
        if (IsCheckingNetwork) return;
        IsCheckingNetwork = true;
        NetworkCheckStatus = "Running network analysis...";
        TraceResults.Clear();
        NetworkLog.Clear();

        try
        {
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Starting network path analysis...");

            // Traceroute to 8.8.8.8 (Google DNS)
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Tracing route to 8.8.8.8 (Google DNS)...");
            await RunTracerouteAsync("8.8.8.8");

            // DNS check
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Checking DNS resolution...");
            await CheckDnsAsync();

            // Check for DNS hijacking
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Testing for DNS hijacking...");
            await CheckDnsHijackAsync();

            // Check for packet interception indicators
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Checking for packet interception...");
            await CheckInterceptionAsync();

            // Network adapter info
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Gathering network adapter information...");
            GatherAdapterInfo();

            NetworkCheckStatus = "Network analysis complete.";
            NetworkLog.Add($"[{DateTime.Now:HH:mm:ss}] Analysis complete.");
        }
        catch (Exception ex)
        {
            NetworkCheckStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsCheckingNetwork = false;
        }
    }

    private async Task RunTracerouteAsync(string target)
    {
        using var pinger = new Ping();
        for (int ttl = 1; ttl <= 30; ttl++)
        {
            try
            {
                var options = new PingOptions(ttl, true);
                var reply = await pinger.SendPingAsync(target, 3000, new byte[32], options);

                string hopAddress = reply.Address?.ToString() ?? "*";
                string hostname = "\u2014";
                string status;

                if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.Success)
                {
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(reply.Address!);
                        hostname = hostEntry.HostName;
                    }
                    catch { }

                    status = reply.Status == IPStatus.Success ? "Destination" : "Router";
                }
                else
                {
                    hopAddress = "*";
                    status = "Timeout";
                }

                var hop = new TraceHop
                {
                    HopNumber = ttl,
                    Address = hopAddress,
                    Hostname = hostname,
                    LatencyMs = reply.RoundtripTime,
                    Status = status
                };
                TraceResults.Add(hop);
                NetworkLog.Add($"  Hop {ttl}: {hopAddress} ({hostname}) - {reply.RoundtripTime}ms [{status}]");

                if (reply.Status == IPStatus.Success) break;
            }
            catch
            {
                TraceResults.Add(new TraceHop { HopNumber = ttl, Address = "*", Hostname = "\u2014", LatencyMs = -1, Status = "Error" });
            }
        }
    }

    private async Task CheckDnsAsync()
    {
        string[] testDomains = { "google.com", "microsoft.com", "cloudflare.com" };
        foreach (var domain in testDomains)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var addresses = await Dns.GetHostAddressesAsync(domain);
                sw.Stop();
                NetworkLog.Add($"  DNS: {domain} -> {string.Join(", ", addresses.Select(a => a.ToString()))} ({sw.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                NetworkLog.Add($"  DNS FAIL: {domain} -> {ex.Message}");
            }
        }
    }

    private async Task CheckDnsHijackAsync()
    {
        // Resolve a non-existent domain -- if it resolves, DNS may be hijacked
        try
        {
            var addresses = await Dns.GetHostAddressesAsync($"nonexistent-{Guid.NewGuid():N}.com");
            if (addresses.Length > 0)
            {
                NetworkLog.Add($"  WARNING: Non-existent domain resolved to {addresses[0]} \u2014 possible DNS hijacking!");
            }
        }
        catch (SocketException)
        {
            NetworkLog.Add("  DNS hijack check: Clean (NXDOMAIN returned correctly)");
        }
    }

    private async Task CheckInterceptionAsync()
    {
        // Check if HTTPS certificates are being intercepted (MITM proxy detection)
        try
        {
            using var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert != null)
                {
                    var issuer = cert.Issuer;
                    NetworkLog.Add($"  SSL Certificate issuer for google.com: {issuer}");
                    // Known MITM proxy issuers
                    string[] mitmIssuers = { "mitmproxy", "Charles Proxy", "Fiddler", "Burp", "ZScaler", "Fortinet", "Blue Coat" };
                    foreach (var mi in mitmIssuers)
                    {
                        if (issuer.Contains(mi, StringComparison.OrdinalIgnoreCase))
                        {
                            NetworkLog.Add($"  ALERT: Possible MITM proxy detected! Issuer matches: {mi}");
                        }
                    }
                }
                return true;
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            await client.GetAsync("https://www.google.com");
            NetworkLog.Add("  HTTPS interception check: Connection successful");
        }
        catch (Exception ex)
        {
            NetworkLog.Add($"  HTTPS check error: {ex.Message}");
        }
    }

    private void GatherAdapterInfo()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var ipProps = nic.GetIPProperties();
            var ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4 == null) continue;

            NetworkLog.Add($"  Adapter: {nic.Name} ({nic.NetworkInterfaceType})");
            NetworkLog.Add($"    MAC: {nic.GetPhysicalAddress()}");
            NetworkLog.Add($"    IP: {ipv4.Address}");
            NetworkLog.Add($"    Speed: {nic.Speed / 1_000_000} Mbps");

            var gateway = ipProps.GatewayAddresses.FirstOrDefault();
            if (gateway != null)
                NetworkLog.Add($"    Gateway: {gateway.Address}");

            var dns = ipProps.DnsAddresses;
            if (dns.Count > 0)
                NetworkLog.Add($"    DNS Servers: {string.Join(", ", dns)}");
        }
    }
}

public class SpeedSample
{
    public double Time { get; set; }
    public double SpeedMbps { get; set; }
}

public class TraceHop
{
    public int HopNumber { get; set; }
    public string Address { get; set; } = "";
    public string Hostname { get; set; } = "";
    public long LatencyMs { get; set; }
    public string Status { get; set; } = "";
}
