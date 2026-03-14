using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// Background service that:
/// 1. Ensures cloudflared is running (install/start if needed)
/// 2. Configures the tunnel ingress rules via Cloudflare API so traffic routes correctly
/// 3. Verifies DNS CNAME record exists
/// Without step 2, cloudflared connects but Cloudflare doesn't know where to route traffic → 522
/// </summary>
public sealed class TunnelAutoInstaller : IHostedService
{
    private readonly CloudflareService _cloudflareService;
    private readonly string _tunnelToken;
    private readonly AppSettings _appSettings;

    public TunnelAutoInstaller(CloudflareService cloudflareService, string tunnelToken, AppSettings appSettings)
    {
        _cloudflareService = cloudflareService;
        _tunnelToken = tunnelToken;
        _appSettings = appSettings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SglLogger.Information("[TunnelAutoInstaller] Starting tunnel auto-installer...");

        // Wait for the API server to be fully ready
        SglLogger.Information("[TunnelAutoInstaller] Waiting 5 seconds for API server to be fully ready...");
        await Task.Delay(5000, cancellationToken);

        // Step 1: Ensure cloudflared is running
        await EnsureCloudflaredRunningAsync(cancellationToken);

        // Step 2: Configure tunnel ingress via Cloudflare API
        // This is CRITICAL — without this, cloudflared connects but Cloudflare returns 522
        await ConfigureTunnelIngressAsync(cancellationToken);
    }

    private async Task EnsureCloudflaredRunningAsync(CancellationToken ct)
    {
        SglLogger.Information("[TunnelAutoInstaller] Checking if Cloudflared is running...");
        if (_cloudflareService.CheckCloudflaredServiceRunning())
        {
            SglLogger.Information("[TunnelAutoInstaller] Cloudflared service is already running.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_tunnelToken))
        {
            SglLogger.Warning("[TunnelAutoInstaller] No tunnel token — cannot start cloudflared.");
            return;
        }

        SglLogger.Information("[TunnelAutoInstaller] Cloudflared not running — attempting startup...");

        // Try managed process mode first
        var (managedOk, managedMsg) = await _cloudflareService.StartManagedTunnelAsync(_tunnelToken);
        if (managedOk)
        {
            SglLogger.Information("[TunnelAutoInstaller] Managed tunnel started: {Message}", managedMsg);
            await Task.Delay(8000, ct);
            return;
        }

        SglLogger.Warning("[TunnelAutoInstaller] Managed process failed: {Message}. Trying service install...", managedMsg);

        // Fall back to Windows service install
        var (success, message) = await _cloudflareService.InstallCloudflaredService(_tunnelToken);
        if (success)
        {
            SglLogger.Information("[TunnelAutoInstaller] Service install succeeded: {Message}", message);
            await Task.Delay(5000, ct);
        }
        else
        {
            SglLogger.Warning("[TunnelAutoInstaller] Service install failed: {Message}", message);
        }
    }

    /// <summary>
    /// Configures the tunnel's public hostname (ingress) via the Cloudflare API.
    /// This tells Cloudflare: "Route traffic for syntheticgamelabs.dpdns.org to http://localhost:5000"
    /// Without this configuration, cloudflared connects but Cloudflare returns 522 because
    /// it doesn't know where to route the traffic.
    /// </summary>
    private async Task ConfigureTunnelIngressAsync(CancellationToken ct)
    {
        // Extract tunnel ID and account ID from the token
        var (tunnelId, accountId) = ExtractTokenInfo(_tunnelToken);
        if (string.IsNullOrEmpty(tunnelId) || string.IsNullOrEmpty(accountId))
        {
            SglLogger.Warning("[TunnelAutoInstaller] Could not extract tunnel/account ID from token.");
            return;
        }

        // Get API credentials — need email + Global API Key to configure tunnel
        var email = _appSettings.Server.CloudflareApiEmail;
        var apiKey = _appSettings.Server.CloudflareApiKey;

        // Also try to get from settings that might have been saved from a previous login
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(apiKey))
        {
            SglLogger.Warning("[TunnelAutoInstaller] No Cloudflare API credentials in settings.");
            SglLogger.Warning("[TunnelAutoInstaller] To auto-configure tunnel, add 'cloudflareApiEmail' and 'cloudflareApiKey' to server settings.");
            SglLogger.Warning("[TunnelAutoInstaller] Or configure the public hostname manually in the Cloudflare Zero Trust Dashboard:");
            SglLogger.Warning("[TunnelAutoInstaller]   1. Go to https://one.dash.cloudflare.com/");
            SglLogger.Warning("[TunnelAutoInstaller]   2. Navigate to Networks > Tunnels");
            SglLogger.Warning("[TunnelAutoInstaller]   3. Click on your tunnel > Public Hostname tab");
            SglLogger.Warning("[TunnelAutoInstaller]   4. Add: Subdomain=syntheticgamelabs, Domain=dpdns.org, Type=HTTP, URL=localhost:{Port}", _appSettings.Server.Port);
            return;
        }

        var hostname = _appSettings.Server.PublicDomain;
        var port = _appSettings.Server.Port;
        var zoneId = _appSettings.Server.CloudflareZoneId;

        if (string.IsNullOrEmpty(hostname))
        {
            SglLogger.Warning("[TunnelAutoInstaller] No public domain configured.");
            return;
        }

        SglLogger.Information("[TunnelAutoInstaller] Configuring tunnel ingress: {Hostname} -> http://localhost:{Port}", hostname, port);
        SglLogger.Information("[TunnelAutoInstaller] Account: {AccountId}, Tunnel: {TunnelId}", accountId[..8] + "...", tunnelId[..8] + "...");

        try
        {
            var apiClient = new CloudflareApiClient();
            var loginOk = await apiClient.LoginWithApiKeyAsync(email, apiKey, ct);
            if (!loginOk)
            {
                SglLogger.Warning("[TunnelAutoInstaller] Cloudflare API login failed: {Error}", apiClient.LastError);
                return;
            }

            SglLogger.Information("[TunnelAutoInstaller] Cloudflare API authenticated as: {Name}", apiClient.AccountName ?? email);

            // Configure the tunnel ingress
            var result = await apiClient.VerifyAndConfigureTunnelAsync(tunnelId, hostname, port, zoneId, ct);

            foreach (var step in result.Steps)
                SglLogger.Information("[TunnelAutoInstaller]   {Step}", step);

            if (result.Success)
            {
                SglLogger.Information("[TunnelAutoInstaller] TUNNEL CONFIGURED SUCCESSFULLY: {Summary}", result.Summary);

                // Now test connectivity
                await Task.Delay(3000, ct);
                var (connOk, connMsg) = await _cloudflareService.TestTunnelConnectivityAsync();
                SglLogger.Information("[TunnelAutoInstaller] Connectivity test: success={Success}, message={Message}", connOk, connMsg);
            }
            else
            {
                SglLogger.Warning("[TunnelAutoInstaller] Tunnel config incomplete: {Summary}", result.Summary);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[TunnelAutoInstaller] Error configuring tunnel: " + ex.Message);
        }
    }

    /// <summary>
    /// Extracts the tunnel ID and account ID from the base64-encoded tunnel token.
    /// Token format: base64({"a":"accountId","t":"tunnelId","s":"secret"})
    /// </summary>
    private static (string? tunnelId, string? accountId) ExtractTokenInfo(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (null, null);
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token.Trim()));
            var doc = JsonDocument.Parse(json);
            string? tunnelId = null, accountId = null;
            if (doc.RootElement.TryGetProperty("t", out var tProp))
                tunnelId = tProp.GetString();
            if (doc.RootElement.TryGetProperty("a", out var aProp))
                accountId = aProp.GetString();
            return (tunnelId, accountId);
        }
        catch
        {
            return (null, null);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
