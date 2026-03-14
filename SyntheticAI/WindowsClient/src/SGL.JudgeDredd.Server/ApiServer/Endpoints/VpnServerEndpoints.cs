using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Server-side VPN server list management.
/// GET  /api/v1/vpn/servers          - Returns the admin-managed VPN server list (any authenticated user)
/// POST /api/v1/vpn/servers/refresh  - Admin-only: Triggers refresh from online sources (VPN Gate + NordVPN API)
/// POST /api/v1/vpn/servers          - Admin-only: Add a custom VPN server entry
/// DELETE /api/v1/vpn/servers/{id}   - Admin-only: Remove a server entry
/// </summary>
public static class VpnServerEndpoints
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string DataFilePath = Path.Combine(DataDir, "vpn_servers.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object FileLock = new();

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/vpn/servers", (Delegate)HandleGetServers);
        app.MapPost(ApiConstants.ApiPrefix + "/vpn/servers/refresh", (Delegate)HandleRefresh);
        app.MapPost(ApiConstants.ApiPrefix + "/vpn/servers", (Delegate)HandleAddServer);
        app.MapDelete(ApiConstants.ApiPrefix + "/vpn/servers/{id}", (Delegate)HandleDeleteServer);
    }

    // ── GET: Return saved VPN server list ────────────────────────────────

    private static IResult HandleGetServers(HttpContext context)
    {
        try
        {
            var servers = LoadServers();
            return Results.Ok(new
            {
                servers,
                count = servers.Count,
                lastRefreshed = GetLastRefreshedTime(),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"VPN servers GET error: {ex.Message}");
            return Results.Problem("Failed to retrieve VPN server list.");
        }
    }

    // ── POST /refresh: Admin-only, fetch from VPN Gate + NordVPN ─────────

    private static async Task<IResult> HandleRefresh(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            SglLogger.Information("Admin triggered VPN server list refresh");
            var allServers = new List<VpnServerEntry>();

            // 1. Fetch from VPN Gate
            var vpnGateServers = await FetchVpnGateServersAsync();
            allServers.AddRange(vpnGateServers);
            SglLogger.Information("VPN Gate returned {Count} servers", vpnGateServers.Count);

            // 2. Fetch from NordVPN API
            var nordServers = await FetchNordVpnServersAsync();
            allServers.AddRange(nordServers);
            SglLogger.Information("NordVPN API returned {Count} servers", nordServers.Count);

            // 3. Merge with existing custom entries (preserve admin-added servers)
            var existingServers = LoadServers();
            var customServers = existingServers.Where(s => s.Source == "custom").ToList();
            allServers.AddRange(customServers);

            // 4. Save to disk
            SaveServers(allServers);

            return Results.Ok(new
            {
                message = "VPN server list refreshed successfully",
                vpnGateCount = vpnGateServers.Count,
                nordVpnCount = nordServers.Count,
                customCount = customServers.Count,
                totalCount = allServers.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"VPN server refresh error: {ex.Message}");
            return Results.Problem($"Failed to refresh VPN server list: {ex.Message}");
        }
    }

    // ── POST: Admin-only, add a custom server entry ─────────────────────

    private static async Task<IResult> HandleAddServer(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var entry = await context.Request.ReadFromJsonAsync<VpnServerEntry>(JsonOptions);
            if (entry == null || string.IsNullOrWhiteSpace(entry.Hostname))
                return Results.BadRequest(new { error = "Server entry with a hostname is required." });

            // Assign ID and metadata
            entry.Id = Guid.NewGuid().ToString("N");
            entry.Source = "custom";
            entry.AddedAt = DateTime.UtcNow;

            var servers = LoadServers();
            servers.Add(entry);
            SaveServers(servers);

            SglLogger.Information("Admin added custom VPN server: {Hostname} ({Country})", entry.Hostname, entry.Country);
            return Results.Ok(new { message = "Server added", server = entry });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"VPN server add error: {ex.Message}");
            return Results.Problem("Failed to add VPN server.");
        }
    }

    // ── DELETE: Admin-only, remove a server by ID ───────────────────────

    private static IResult HandleDeleteServer(HttpContext context, string id)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest(new { error = "Server ID is required." });

            var servers = LoadServers();
            var removed = servers.RemoveAll(s => s.Id == id);

            if (removed == 0)
                return Results.NotFound(new { error = $"Server with ID '{id}' not found." });

            SaveServers(servers);

            SglLogger.Information("Admin removed VPN server: {Id}", id);
            return Results.Ok(new { message = "Server removed", id });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"VPN server delete error: {ex.Message}");
            return Results.Problem("Failed to delete VPN server.");
        }
    }

    // ── VPN Gate Fetcher ────────────────────────────────────────────────

    private static async Task<List<VpnServerEntry>> FetchVpnGateServersAsync()
    {
        var servers = new List<VpnServerEntry>();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var csv = await http.GetStringAsync("https://www.vpngate.net/api/iphone/");
            var lines = csv.Split('\n').Skip(2); // Skip header lines

            foreach (var line in lines)
            {
                var cols = line.Split(',');
                if (cols.Length < 15) continue;

                var hostname = cols[0];   // HostName
                var ip = cols[1];          // IP
                var score = long.TryParse(cols[2], out var sc) ? sc : 0;       // Score
                var ping = int.TryParse(cols[3], out var p) ? p : -1;          // Ping
                var speed = long.TryParse(cols[4], out var sp) ? sp : 0;       // Speed bps
                var countryShort = cols[5]; // CountryShort
                var countryLong = cols[6];  // CountryLong
                var numSessions = int.TryParse(cols[7], out var ns) ? ns : 0;  // NumVpnSessions
                var openvpnConfig = cols.Length > 14 ? cols[14] : null;         // OpenVPN_ConfigData_Base64

                if (string.IsNullOrWhiteSpace(hostname) || speed <= 0)
                    continue;

                servers.Add(new VpnServerEntry
                {
                    Id = $"vpngate-{ip.Replace('.', '-')}",
                    Hostname = hostname,
                    IpAddress = ip,
                    Country = countryLong,
                    CountryCode = countryShort,
                    Speed = speed,
                    Ping = ping,
                    Score = score,
                    Sessions = numSessions,
                    OpenVpnConfigBase64 = string.IsNullOrWhiteSpace(openvpnConfig) ? null : openvpnConfig.Trim(),
                    Source = "vpngate",
                    Username = "vpn",
                    Password = "vpn",
                    AddedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error($"VPN Gate fetch failed: {ex.Message}");
        }

        return servers;
    }

    // ── NordVPN Fetcher ─────────────────────────────────────────────────

    private static async Task<List<VpnServerEntry>> FetchNordVpnServersAsync()
    {
        var servers = new List<VpnServerEntry>();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await http.GetStringAsync("https://api.nordvpn.com/v1/servers?limit=50");

            using var doc = JsonDocument.Parse(json);
            foreach (var server in doc.RootElement.EnumerateArray())
            {
                var hostname = server.TryGetProperty("hostname", out var h) ? h.GetString() : null;
                var station = server.TryGetProperty("station", out var s) ? s.GetString() : null;
                var load = server.TryGetProperty("load", out var l) ? l.GetInt32() : 0;
                var name = server.TryGetProperty("name", out var n) ? n.GetString() : hostname;

                // Extract country
                var country = "";
                var countryCode = "";
                if (server.TryGetProperty("locations", out var locations) &&
                    locations.GetArrayLength() > 0)
                {
                    var loc = locations[0];
                    if (loc.TryGetProperty("country", out var c))
                    {
                        country = c.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                        countryCode = c.TryGetProperty("code", out var cc) ? cc.GetString() ?? "" : "";
                    }
                }

                // Extract supported protocols
                var protocols = new List<string>();
                if (server.TryGetProperty("technologies", out var techs))
                {
                    foreach (var tech in techs.EnumerateArray())
                    {
                        var techName = tech.TryGetProperty("name", out var tn) ? tn.GetString() : null;
                        if (!string.IsNullOrEmpty(techName))
                            protocols.Add(techName);
                    }
                }

                if (string.IsNullOrWhiteSpace(hostname)) continue;

                servers.Add(new VpnServerEntry
                {
                    Id = $"nord-{hostname?.Replace('.', '-')}",
                    Hostname = hostname!,
                    IpAddress = station ?? "",
                    Country = country,
                    CountryCode = countryCode,
                    Load = load,
                    Protocols = protocols.Count > 0 ? protocols : null,
                    Source = "nordvpn",
                    Name = name ?? hostname!,
                    AddedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error($"NordVPN API fetch failed: {ex.Message}");
        }

        return servers;
    }

    // ── Persistence ─────────────────────────────────────────────────────

    private static List<VpnServerEntry> LoadServers()
    {
        lock (FileLock)
        {
            if (!File.Exists(DataFilePath))
                return new List<VpnServerEntry>();

            try
            {
                var json = File.ReadAllText(DataFilePath);
                return JsonSerializer.Deserialize<List<VpnServerEntry>>(json, JsonOptions)
                       ?? new List<VpnServerEntry>();
            }
            catch (Exception ex)
            {
                SglLogger.Error($"Failed to load VPN server data: {ex.Message}");
                return new List<VpnServerEntry>();
            }
        }
    }

    private static void SaveServers(List<VpnServerEntry> servers)
    {
        lock (FileLock)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var json = JsonSerializer.Serialize(servers, JsonOptions);
                File.WriteAllText(DataFilePath, json);
            }
            catch (Exception ex)
            {
                SglLogger.Error($"Failed to save VPN server data: {ex.Message}");
            }
        }
    }

    private static DateTime? GetLastRefreshedTime()
    {
        try
        {
            if (File.Exists(DataFilePath))
                return File.GetLastWriteTimeUtc(DataFilePath);
        }
        catch { }
        return null;
    }
}

// ── Data Model ──────────────────────────────────────────────────────────

/// <summary>
/// Unified VPN server entry used for storage and API responses.
/// Accommodates VPN Gate, NordVPN, and custom admin-added entries.
/// </summary>
public class VpnServerEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("speed")]
    public long Speed { get; set; }

    [JsonPropertyName("ping")]
    public int Ping { get; set; } = -1;

    [JsonPropertyName("score")]
    public long Score { get; set; }

    [JsonPropertyName("sessions")]
    public int Sessions { get; set; }

    [JsonPropertyName("load")]
    public int Load { get; set; }

    [JsonPropertyName("protocols")]
    public List<string>? Protocols { get; set; }

    [JsonPropertyName("openVpnConfigBase64")]
    public string? OpenVpnConfigBase64 { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty; // "vpngate", "nordvpn", "custom"

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
