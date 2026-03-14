using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// Client for the Cloudflare API v4 (https://api.cloudflare.com/client/v4/).
/// Supports authentication via API Token (Bearer) or Global API Key (Email + Key).
/// Used by the Admin Panel to verify credentials and manage tunnel/zone configuration.
/// </summary>
public class CloudflareApiClient : IDisposable
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4";
    private readonly HttpClient _httpClient;

    private string? _authEmail;
    private string? _authKey;
    private string? _authToken;
    private bool _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated;
    public string? AccountName { get; private set; }
    public string? AccountEmail { get; private set; }
    public string? AccountId { get; private set; }
    public string? ZoneId { get; private set; }
    public string? ZoneName { get; private set; }
    public string? ZoneStatus { get; private set; }
    public string? AuthMethod { get; private set; }
    public string? LastError { get; private set; }
    public List<CfZoneInfo> Zones { get; private set; } = new();
    public List<CfTunnelInfo> Tunnels { get; private set; } = new();

    public CloudflareApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(BaseUrl)
        };
    }

    /// <summary>
    /// Authenticates using a Cloudflare API Token (Bearer authentication).
    /// This is the recommended authentication method.
    /// </summary>
    public async Task<bool> LoginWithTokenAsync(string apiToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            LastError = "API Token cannot be empty.";
            return false;
        }

        _authToken = apiToken.Trim();
        _authEmail = null;
        _authKey = null;
        AuthMethod = "API Token";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authToken);

        try
        {
            // Verify the token
            var response = await _httpClient.GetAsync("/client/v4/user/tokens/verify", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<CfTokenVerify>>(cancellationToken: ct);

            if (json?.Success != true)
            {
                _isAuthenticated = false;
                LastError = json?.Errors?.FirstOrDefault()?.Message ?? "Token verification failed.";
                SglLogger.Warning("Cloudflare token verification failed: {Error}", LastError);
                return false;
            }

            if (json.Result?.Status != "active")
            {
                _isAuthenticated = false;
                LastError = $"Token status is '{json.Result?.Status}', expected 'active'.";
                return false;
            }

            // Token verified — fetch user info
            _isAuthenticated = true;
            SglLogger.Information("Cloudflare API Token verified successfully.");

            await FetchUserInfoAsync(ct);
            await FetchZonesAsync(ct);
            await FetchTunnelsAsync(ct);

            return true;
        }
        catch (HttpRequestException ex)
        {
            _isAuthenticated = false;
            LastError = $"Network error: {ex.Message}";
            SglLogger.Error("Cloudflare login network error: " + ex.Message);
            return false;
        }
        catch (TaskCanceledException)
        {
            _isAuthenticated = false;
            LastError = "Request timed out.";
            return false;
        }
        catch (Exception ex)
        {
            _isAuthenticated = false;
            LastError = ex.Message;
            SglLogger.Error("Cloudflare login error: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Authenticates using Cloudflare Global API Key (Email + Key).
    /// Legacy method but still widely supported.
    /// </summary>
    public async Task<bool> LoginWithApiKeyAsync(string email, string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(apiKey))
        {
            LastError = "Email and API Key cannot be empty.";
            return false;
        }

        _authEmail = email.Trim();
        _authKey = apiKey.Trim();
        _authToken = null;
        AuthMethod = "Global API Key";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Email", _authEmail);
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Key", _authKey);

        try
        {
            // Verify by fetching user info
            var response = await _httpClient.GetAsync("/client/v4/user", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<CfUserInfo>>(cancellationToken: ct);

            if (json?.Success != true)
            {
                _isAuthenticated = false;
                LastError = json?.Errors?.FirstOrDefault()?.Message ?? "API Key verification failed.";
                SglLogger.Warning("Cloudflare API Key verification failed: {Error}", LastError);
                return false;
            }

            _isAuthenticated = true;
            AccountEmail = json.Result?.Email;
            AccountName = $"{json.Result?.FirstName} {json.Result?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(AccountName))
                AccountName = AccountEmail;

            SglLogger.Information("Cloudflare API Key verified for: {Email}", AccountEmail ?? "unknown");

            await FetchZonesAsync(ct);
            await FetchTunnelsAsync(ct);

            return true;
        }
        catch (HttpRequestException ex)
        {
            _isAuthenticated = false;
            LastError = $"Network error: {ex.Message}";
            SglLogger.Error("Cloudflare login network error: " + ex.Message);
            return false;
        }
        catch (TaskCanceledException)
        {
            _isAuthenticated = false;
            LastError = "Request timed out.";
            return false;
        }
        catch (Exception ex)
        {
            _isAuthenticated = false;
            LastError = ex.Message;
            SglLogger.Error("Cloudflare login error: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Fetches user information from the Cloudflare API.
    /// </summary>
    private async Task FetchUserInfoAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync("/client/v4/user", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<CfUserInfo>>(cancellationToken: ct);

            if (json?.Success == true && json.Result != null)
            {
                AccountEmail = json.Result.Email;
                AccountName = $"{json.Result.FirstName} {json.Result.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(AccountName))
                    AccountName = AccountEmail;
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to fetch Cloudflare user info: " + ex.Message);
        }
    }

    /// <summary>
    /// Fetches all zones from the Cloudflare account.
    /// </summary>
    private async Task FetchZonesAsync(CancellationToken ct)
    {
        Zones.Clear();
        try
        {
            var response = await _httpClient.GetAsync("/client/v4/zones?per_page=50", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<List<CfZoneInfo>>>(cancellationToken: ct);

            if (json?.Success == true && json.Result != null)
            {
                Zones = json.Result;
                // Set the first zone as default if available
                if (Zones.Count > 0)
                {
                    var first = Zones[0];
                    ZoneId = first.Id;
                    ZoneName = first.Name;
                    ZoneStatus = first.Status;
                }
                SglLogger.Information("Fetched {Count} Cloudflare zone(s).", Zones.Count);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to fetch Cloudflare zones: " + ex.Message);
        }
    }

    /// <summary>
    /// Fetches all Cloudflare Tunnels from the account.
    /// </summary>
    private async Task FetchTunnelsAsync(CancellationToken ct)
    {
        Tunnels.Clear();

        if (string.IsNullOrEmpty(AccountId))
        {
            // Try to get account ID from accounts endpoint
            try
            {
                var acctResponse = await _httpClient.GetAsync("/client/v4/accounts?per_page=5", ct);
                var acctJson = await acctResponse.Content.ReadFromJsonAsync<CfApiResponse<List<CfAccountInfo>>>(cancellationToken: ct);
                if (acctJson?.Success == true && acctJson.Result?.Count > 0)
                {
                    AccountId = acctJson.Result[0].Id;
                    if (string.IsNullOrWhiteSpace(AccountName))
                        AccountName = acctJson.Result[0].Name;
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(AccountId))
            return;

        try
        {
            var response = await _httpClient.GetAsync($"/client/v4/accounts/{AccountId}/cfd_tunnel?per_page=50", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<List<CfTunnelInfo>>>(cancellationToken: ct);

            if (json?.Success == true && json.Result != null)
            {
                Tunnels = json.Result;
                SglLogger.Information("Fetched {Count} Cloudflare tunnel(s).", Tunnels.Count);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to fetch Cloudflare tunnels: " + ex.Message);
        }
    }

    // ── Tunnel Configuration Management ────────────────────────────────

    public async Task<CfTunnelConfig?> GetTunnelConfigAsync(string accountId, string tunnelId, CancellationToken ct = default)
    {
        if (!_isAuthenticated) { LastError = "Not authenticated."; return null; }
        try
        {
            var response = await _httpClient.GetAsync(
                $"/client/v4/accounts/{accountId}/cfd_tunnel/{tunnelId}/configurations", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<CfTunnelConfig>>(cancellationToken: ct);
            if (json?.Success == true && json.Result != null)
            {
                SglLogger.Information("Fetched tunnel config for {TunnelId}.", tunnelId);
                return json.Result;
            }
            LastError = json?.Errors?.FirstOrDefault()?.Message ?? "Failed to get tunnel config.";
            return null;
        }
        catch (Exception ex)
        {
            LastError = $"Tunnel config error: {ex.Message}";
            SglLogger.Error("GetTunnelConfigAsync: " + ex.Message);
            return null;
        }
    }

    public async Task<bool> SetTunnelIngressAsync(string accountId, string tunnelId,
        string hostname, string serviceUrl, CancellationToken ct = default)
    {
        if (!_isAuthenticated) { LastError = "Not authenticated."; return false; }
        try
        {
            var config = new
            {
                config = new
                {
                    ingress = new object[]
                    {
                        new { hostname, service = serviceUrl, originRequest = new { } },
                        new { service = "http_status:404" }
                    }
                }
            };
            var content = new StringContent(
                JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(
                $"/client/v4/accounts/{accountId}/cfd_tunnel/{tunnelId}/configurations", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                SglLogger.Information("Tunnel ingress set: {Hostname} -> {Service}", hostname, serviceUrl);
                return true;
            }
            try
            {
                var json = JsonSerializer.Deserialize<CfApiResponse<object>>(body);
                LastError = json?.Errors?.FirstOrDefault()?.Message ?? $"HTTP {(int)response.StatusCode}";
            }
            catch { LastError = $"HTTP {(int)response.StatusCode}"; }
            SglLogger.Warning("Failed to set tunnel ingress: {Error}", LastError);
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"Ingress config error: {ex.Message}";
            SglLogger.Error("SetTunnelIngressAsync: " + ex.Message);
            return false;
        }
    }

    public async Task<List<CfTunnelConnection>?> GetTunnelConnectionsAsync(
        string accountId, string tunnelId, CancellationToken ct = default)
    {
        if (!_isAuthenticated) return null;
        try
        {
            var response = await _httpClient.GetAsync(
                $"/client/v4/accounts/{accountId}/cfd_tunnel/{tunnelId}/connections", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<List<CfTunnelConnection>>>(cancellationToken: ct);
            if (json?.Success == true)
                return json.Result ?? new List<CfTunnelConnection>();
            return null;
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to get tunnel connections: {Error}", ex.Message);
            return null;
        }
    }

    public async Task<CfDnsRecord?> CheckDnsRecordAsync(string zoneId, string hostname, CancellationToken ct = default)
    {
        if (!_isAuthenticated) return null;
        try
        {
            // Search for ALL record types (not just CNAME) — an A record at this hostname
            // would block CNAME creation and needs to be found/replaced
            var response = await _httpClient.GetAsync(
                $"/client/v4/zones/{zoneId}/dns_records?name={hostname}", ct);
            var json = await response.Content.ReadFromJsonAsync<CfApiResponse<List<CfDnsRecord>>>(cancellationToken: ct);
            if (json?.Success == true && json.Result?.Count > 0)
                return json.Result[0];
            return null;
        }
        catch (Exception ex)
        {
            SglLogger.Warning("DNS check failed: {Error}", ex.Message);
            return null;
        }
    }

    public async Task<bool> EnsureDnsCnameAsync(string zoneId, string hostname, string tunnelId, CancellationToken ct = default)
    {
        if (!_isAuthenticated) { LastError = "Not authenticated."; return false; }
        var target = $"{tunnelId}.cfargotunnel.com";

        // For zone-apex records, Cloudflare API requires name="@" (not the full hostname).
        // If we pass the full hostname and it equals the zone name, Cloudflare appends the
        // zone name again, creating "hostname.hostname.zone" instead of "hostname.zone".
        var zoneName = Zones.FirstOrDefault(z => z.Id == zoneId)?.Name;
        var dnsName = string.Equals(hostname, zoneName, StringComparison.OrdinalIgnoreCase) ? "@" : hostname;

        try
        {
            var existing = await CheckDnsRecordAsync(zoneId, hostname, ct);
            if (existing != null)
            {
                // If existing record is the correct CNAME, we're done
                if (existing.Type == "CNAME" && existing.Content == target)
                {
                    SglLogger.Information("DNS CNAME correct: {Hostname} -> {Target}", hostname, target);
                    return true;
                }

                // If existing record is NOT a CNAME (e.g. A record), delete it first
                // because Cloudflare won't let us PUT-update an A record into a CNAME
                if (existing.Type != "CNAME")
                {
                    SglLogger.Information("Deleting existing {Type} record for {Hostname} to replace with CNAME",
                        existing.Type, hostname);
                    var delResp = await _httpClient.DeleteAsync(
                        $"/client/v4/zones/{zoneId}/dns_records/{existing.Id}", ct);
                    if (!delResp.IsSuccessStatusCode)
                    {
                        LastError = $"Failed to delete existing {existing.Type} record: HTTP {(int)delResp.StatusCode}";
                        return false;
                    }
                    // Fall through to create new CNAME below
                }
                else
                {
                    // Existing CNAME points to wrong target — update it
                    var updatePayload = new { type = "CNAME", name = dnsName, content = target, proxied = true };
                    var updateContent = new StringContent(
                        JsonSerializer.Serialize(updatePayload), System.Text.Encoding.UTF8, "application/json");
                    var updateResp = await _httpClient.PutAsync(
                        $"/client/v4/zones/{zoneId}/dns_records/{existing.Id}", updateContent, ct);
                    if (updateResp.IsSuccessStatusCode)
                    {
                        SglLogger.Information("DNS CNAME updated: {Hostname} -> {Target}", hostname, target);
                        return true;
                    }
                    LastError = $"Failed to update DNS: HTTP {(int)updateResp.StatusCode}";
                    return false;
                }
            }

            // Create new CNAME record
            var createPayload = new { type = "CNAME", name = dnsName, content = target, proxied = true, ttl = 1 };
            var createContent = new StringContent(
                JsonSerializer.Serialize(createPayload), System.Text.Encoding.UTF8, "application/json");
            var createResp = await _httpClient.PostAsync(
                $"/client/v4/zones/{zoneId}/dns_records", createContent, ct);
            if (createResp.IsSuccessStatusCode)
            {
                SglLogger.Information("DNS CNAME created: {Hostname} -> {Target}", hostname, target);
                return true;
            }
            var body = await createResp.Content.ReadAsStringAsync(ct);
            LastError = $"DNS create failed: {body[..Math.Min(200, body.Length)]}";
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"DNS error: {ex.Message}";
            return false;
        }
    }

    public async Task<TunnelVerificationResult> VerifyAndConfigureTunnelAsync(
        string tunnelId, string hostname, int localPort, string zoneId, CancellationToken ct = default)
    {
        var result = new TunnelVerificationResult();
        if (!_isAuthenticated || string.IsNullOrEmpty(AccountId))
        {
            result.Summary = "Not authenticated.";
            return result;
        }
        var serviceUrl = $"http://localhost:{localPort}";
        result.Steps.Add("Starting tunnel verification...");

        var tunnel = Tunnels.FirstOrDefault(t => t.Id.Equals(tunnelId, StringComparison.OrdinalIgnoreCase));
        if (tunnel != null)
        {
            result.TunnelStatus = tunnel.Status;
            result.Steps.Add($"Tunnel: {tunnel.Name} (Status: {tunnel.Status})");
        }
        else
            result.Steps.Add($"Tunnel {tunnelId[..Math.Min(8, tunnelId.Length)]}... not in account list.");

        result.Steps.Add("Checking ingress rules...");
        var config = await GetTunnelConfigAsync(AccountId, tunnelId, ct);
        bool ingressCorrect = false;
        if (config?.Config?.Ingress != null)
        {
            var rule = config.Config.Ingress.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Hostname) &&
                r.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
            if (rule != null && rule.Service?.Equals(serviceUrl, StringComparison.OrdinalIgnoreCase) == true)
            {
                ingressCorrect = true;
                result.Steps.Add($"Ingress OK: {rule.Hostname} -> {rule.Service}");
            }
            else if (rule != null)
                result.Steps.Add($"Ingress wrong: routes to '{rule.Service}' not '{serviceUrl}'. Updating...");
            else
                result.Steps.Add($"No ingress rule for {hostname}. Creating...");
        }
        else
            result.Steps.Add("No ingress configuration found. Creating...");

        if (!ingressCorrect)
        {
            var ok = await SetTunnelIngressAsync(AccountId, tunnelId, hostname, serviceUrl, ct);
            result.IngressConfigured = ok;
            result.Steps.Add(ok ? "Ingress configured successfully." : $"Ingress FAILED: {LastError}");
        }
        else
            result.IngressConfigured = true;

        // DNS CNAME: Try to verify/create, but don't fail if we can't
        // For dpdns.org and similar managed domains, DNS may be handled automatically
        // by the tunnel config or by the domain provider
        result.Steps.Add("Checking DNS CNAME...");
        if (!string.IsNullOrEmpty(zoneId))
        {
            try
            {
                var dnsOk = await EnsureDnsCnameAsync(zoneId, hostname, tunnelId, ct);
                result.DnsConfigured = dnsOk;
                if (dnsOk)
                    result.Steps.Add("DNS CNAME verified/created via Cloudflare API.");
                else
                    result.Steps.Add($"DNS via API: {LastError} (may be managed externally - checking resolution...)");
            }
            catch (Exception ex)
            {
                result.Steps.Add($"DNS API error: {ex.Message} (checking DNS resolution instead...)");
            }
        }
        else
        {
            result.Steps.Add("No zone ID configured - cannot manage DNS via API.");
        }

        // If DNS API failed, check if DNS already resolves (it may be managed externally)
        if (!result.DnsConfigured)
        {
            try
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(hostname, ct);
                if (addresses.Length > 0)
                {
                    result.DnsConfigured = true;
                    result.Steps.Add($"DNS resolves to {addresses[0]} ({addresses.Length} address(es)) - DNS is working.");
                }
                else
                {
                    result.Steps.Add("DNS does not resolve - hostname not reachable.");
                }
            }
            catch (Exception ex)
            {
                result.Steps.Add($"DNS resolution check failed: {ex.Message}");
            }
        }

        result.Steps.Add("Checking tunnel connections...");
        var connections = await GetTunnelConnectionsAsync(AccountId, tunnelId, ct);
        if (connections != null)
        {
            result.ActiveConnections = connections.Count;
            result.Steps.Add($"Active connections: {connections.Count}");
        }
        else
            result.Steps.Add("Could not retrieve connections.");

        // Success = ingress configured. DNS is secondary (may be managed externally)
        result.Success = result.IngressConfigured;
        result.Summary = result.Success
            ? $"Tunnel configured: {hostname} -> {serviceUrl}. DNS: {(result.DnsConfigured ? "OK" : "external")}. {result.ActiveConnections} connection(s)."
            : $"FAILED. Ingress: {(result.IngressConfigured ? "OK" : "FAILED")}. DNS: {(result.DnsConfigured ? "OK" : "FAILED")}.";
        return result;
    }

    /// <summary>
    /// Logs out and clears all cached credentials and state.
    /// </summary>
    public void Logout()
    {
        _authToken = null;
        _authEmail = null;
        _authKey = null;
        _isAuthenticated = false;
        AccountName = null;
        AccountEmail = null;
        AccountId = null;
        ZoneId = null;
        ZoneName = null;
        ZoneStatus = null;
        AuthMethod = null;
        LastError = null;
        Zones.Clear();
        Tunnels.Clear();
        _httpClient.DefaultRequestHeaders.Clear();
        SglLogger.Information("Cloudflare API client logged out.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Cloudflare API response models ──

    public class CfApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("errors")]
        public List<CfApiError>? Errors { get; set; }

        [JsonPropertyName("messages")]
        public List<CfApiError>? Messages { get; set; }
    }

    public class CfApiError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class CfTokenVerify
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    public class CfUserInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class CfAccountInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}

public class CfZoneInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class CfTunnelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class CfTunnelConfig
{
    [JsonPropertyName("config")] public CfTunnelConfigInner? Config { get; set; }
}

public class CfTunnelConfigInner
{
    [JsonPropertyName("ingress")] public List<CfIngressRule>? Ingress { get; set; }
}

public class CfIngressRule
{
    [JsonPropertyName("hostname")] public string? Hostname { get; set; }
    [JsonPropertyName("service")] public string? Service { get; set; }
}

public class CfTunnelConnection
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("origin_ip")] public string? OriginIp { get; set; }
    [JsonPropertyName("opened_at")] public DateTime? OpenedAt { get; set; }
    [JsonPropertyName("is_pending_reconnect")] public bool IsPendingReconnect { get; set; }
}

public class CfDnsRecord
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("proxied")] public bool Proxied { get; set; }
}

public class TunnelVerificationResult
{
    public bool Success { get; set; }
    public string Summary { get; set; } = "";
    public string? TunnelStatus { get; set; }
    public bool IngressConfigured { get; set; }
    public bool DnsConfigured { get; set; }
    public int ActiveConnections { get; set; }
    public List<string> Steps { get; } = new();
}
