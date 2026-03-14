using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Server.ApiServer.Services;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Admin-only metrics and user management endpoints for the website admin tab.
/// </summary>
public static class AdminMetricsEndpoints
{
    private static long _totalApiRequests;

    // Persistent DM store — persists to data/dm_store.json on every write
    private static readonly Dictionary<string, List<DirectMessage>> _dmStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _dmLock = new();
    private static readonly string DmStorePath = Path.Combine(AppContext.BaseDirectory, "data", "dm_store.json");
    private static bool _dmStoreLoaded;

    /// <summary>
    /// Increment the global API request counter. Can be called from middleware.
    /// </summary>
    public static void IncrementRequestCount() => Interlocked.Increment(ref _totalApiRequests);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/admin/metrics", HandleGetMetrics);
        app.MapGet(ApiConstants.ApiPrefix + "/admin/users", HandleGetUsers);
        app.MapPost(ApiConstants.ApiPrefix + "/admin/users/{username}/ban", HandleBanUser);
        app.MapPost(ApiConstants.ApiPrefix + "/admin/users/{username}/unban", HandleUnbanUser);
        app.MapPost(ApiConstants.ApiPrefix + "/admin/users/{username}/promote", (Delegate)HandlePromoteUser);
        app.MapPost(ApiConstants.ApiPrefix + "/admin/users/{username}/demote", (Delegate)HandleDemoteUser);
        app.MapDelete(ApiConstants.ApiPrefix + "/admin/users/{username}", HandleDeleteUser);
        app.MapPost(ApiConstants.ApiPrefix + "/admin/dm", (Delegate)HandleSendDm);
        app.MapGet(ApiConstants.ApiPrefix + "/admin/dm/{username}", (Delegate)HandleGetDm);
        app.MapGet(ApiConstants.ApiPrefix + "/admin/metrics/website", (Delegate)HandleGetWebsiteMetrics);
        app.MapGet(ApiConstants.ApiPrefix + "/metrics/public", (Delegate)HandleGetPublicMetrics);
    }

    private static IResult HandleGetMetrics(
        HttpContext context,
        JudgeDreddApiServer server,
        ConnectedClientTracker tracker,
        UserAccountStore accountStore,
        ILlmService? llmService = null)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        var accounts = accountStore.GetAllAccounts();

        var metrics = new
        {
            totalRegisteredUsers = accounts.Count,
            activeClients = tracker.OnlineCount,
            serverUptime = DateTime.UtcNow - server.StartedAt,
            totalApiRequests = Interlocked.Read(ref _totalApiRequests),
            serverVersion = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion,
            llmModelLoaded = llmService?.IsModelLoaded ?? false,
            timestamp = DateTime.UtcNow
        };

        return Results.Ok(metrics);
    }

    private static IResult HandleGetUsers(HttpContext context, UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        var accounts = accountStore.GetAllAccounts();

        var users = accounts.Select(a => new
        {
            username = a.Username,
            registeredAt = a.RegisteredAt,
            lastLoginAt = a.LastLoginAt,
            isBanned = a.IsBanned,
            isAdmin = a.IsAdmin
        }).ToList();

        return Results.Ok(users);
    }

    private static async Task<IResult> HandleBanUser(
        HttpContext context,
        string username,
        UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(username))
            return Results.BadRequest(new { error = "Username is required." });

        var success = await accountStore.BanUserAsync(username);
        if (!success)
            return Results.NotFound(new { error = $"User '{username}' not found." });

        SglLogger.Information("Admin banned user: {Username}", username);
        return Results.Ok(new { message = $"User '{username}' has been banned.", username });
    }

    private static async Task<IResult> HandleUnbanUser(
        HttpContext context,
        string username,
        UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(username))
            return Results.BadRequest(new { error = "Username is required." });

        var success = await accountStore.UnbanUserAsync(username);
        if (!success)
            return Results.NotFound(new { error = $"User '{username}' not found." });

        SglLogger.Information("Admin unbanned user: {Username}", username);
        return Results.Ok(new { message = $"User '{username}' has been unbanned.", username });
    }

    private static async Task<IResult> HandleDeleteUser(
        HttpContext context,
        string username,
        UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(username))
            return Results.BadRequest(new { error = "Username is required." });

        var success = await accountStore.RemoveUserAsync(username);
        if (!success)
            return Results.NotFound(new { error = $"User '{username}' not found." });

        SglLogger.Information("Admin deleted user: {Username}", username);
        return Results.Ok(new { message = $"User '{username}' has been deleted.", username });
    }

    private static async Task<IResult> HandlePromoteUser(
        HttpContext context,
        string username,
        UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(username))
            return Results.BadRequest(new { error = "Username is required." });

        var success = await accountStore.PromoteUserAsync(username);
        if (!success)
            return Results.NotFound(new { error = $"User '{username}' not found." });

        SglLogger.Information("Admin promoted user: {Username}", username);
        return Results.Ok(new { message = $"User '{username}' has been promoted to admin.", username });
    }

    private static async Task<IResult> HandleDemoteUser(
        HttpContext context,
        string username,
        UserAccountStore accountStore)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(username))
            return Results.BadRequest(new { error = "Username is required." });

        var success = await accountStore.DemoteUserAsync(username);
        if (!success)
            return Results.NotFound(new { error = $"User '{username}' not found." });

        SglLogger.Information("Admin demoted user: {Username}", username);
        return Results.Ok(new { message = $"User '{username}' has been demoted from admin.", username });
    }

    /// <summary>
    /// Load DMs from disk on first access.
    /// </summary>
    private static void EnsureDmStoreLoaded()
    {
        if (_dmStoreLoaded) return;
        lock (_dmLock)
        {
            if (_dmStoreLoaded) return;
            try
            {
                if (File.Exists(DmStorePath))
                {
                    var json = File.ReadAllText(DmStorePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, List<DirectMessage>>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                            _dmStore[kvp.Key] = kvp.Value;
                    }
                    SglLogger.Information("Loaded {Count} DM mailboxes from disk", _dmStore.Count);
                }
            }
            catch (Exception ex)
            {
                SglLogger.Warning("Failed to load DM store from disk: {Error}", ex.Message);
            }
            _dmStoreLoaded = true;
        }
    }

    private static void SaveDmStoreToDisk()
    {
        try
        {
            var dir = Path.GetDirectoryName(DmStorePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_dmStore, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DmStorePath, json);
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to persist DM store: {Error}", ex.Message);
        }
    }

    private static async Task<IResult> HandleSendDm(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        var adminName = context.Items.TryGetValue("Username", out var u)
            ? u?.ToString() ?? "admin"
            : "admin";

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return Results.BadRequest(new { error = "Empty request body." });

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<DmRequest>(body, options);
        if (request == null || string.IsNullOrWhiteSpace(request.TargetUsername) || string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "targetUsername and message are required." });

        var dm = new DirectMessage
        {
            FromAdmin = adminName,
            Message = request.Message.Trim(),
            SentAt = DateTime.UtcNow
        };

        EnsureDmStoreLoaded();
        lock (_dmLock)
        {
            if (!_dmStore.ContainsKey(request.TargetUsername))
                _dmStore[request.TargetUsername] = new List<DirectMessage>();
            _dmStore[request.TargetUsername].Add(dm);
            SaveDmStoreToDisk();
        }

        SglLogger.Information("Admin {Admin} sent DM to {User}: {Message}", adminName, request.TargetUsername, dm.Message);
        return Results.Ok(new { status = "sent", dm });
    }

    private static IResult HandleGetDm(HttpContext context, string username)
    {
        EnsureDmStoreLoaded();
        List<DirectMessage>? messages;
        lock (_dmLock)
        {
            _dmStore.TryGetValue(username, out messages);
            if (messages != null)
            {
                _dmStore.Remove(username);
                SaveDmStoreToDisk();
            }
        }

        return Results.Ok(new { messages = messages ?? new List<DirectMessage>() });
    }

    private static IResult HandleGetWebsiteMetrics(HttpContext context, WebsiteMetricsService metricsService)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        var snapshot = metricsService.GetSnapshot();

        return Results.Ok(new
        {
            totalVisitors = snapshot.TotalVisitors,
            activeVisitors = snapshot.ActiveVisitors,
            avgSessionDuration = snapshot.AvgSessionDuration,
            totalDownloads = snapshot.TotalDownloads,
            totalPageViews = snapshot.TotalPageViews,
            downloadsByPlatform = snapshot.DownloadsByPlatform,
            topCountries = snapshot.TopCountries,
            topPages = snapshot.TopPages,
            timestamp = snapshot.Timestamp
        });
    }

    private static IResult HandleGetPublicMetrics(WebsiteMetricsService metricsService)
    {
        return Results.Ok(metricsService.GetPublicSnapshot());
    }
}

public class DmRequest
{
    public string TargetUsername { get; set; } = "";
    public string Message { get; set; } = "";
    public string FromAdmin { get; set; } = "";
}

public class DirectMessage
{
    public string FromAdmin { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; }
}
