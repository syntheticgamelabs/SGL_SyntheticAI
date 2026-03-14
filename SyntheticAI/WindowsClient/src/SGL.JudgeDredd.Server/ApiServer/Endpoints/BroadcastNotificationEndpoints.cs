using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Admin broadcast notification with countdown timer.
/// Stored in a static field so all clients can poll it.
/// Separate from the existing BroadcastEndpoints (simple message broadcast).
/// </summary>
public static class BroadcastNotificationEndpoints
{
    private static BroadcastNotification? _activeNotification;
    private static readonly object _lock = new();

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/broadcast-notification", (Delegate)HandlePost);
        app.MapGet("/api/v1/admin/broadcast-notification", (Delegate)HandleGet);
        app.MapDelete("/api/v1/admin/broadcast-notification", (Delegate)HandleDelete);
    }

    private static async Task<IResult> HandlePost(HttpContext context)
    {
        try
        {
            var role = context.Items["Role"]?.ToString();
            if (role != "admin")
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);

            var username = context.Items.TryGetValue("Username", out var u)
                ? u?.ToString() ?? "admin"
                : "admin";

            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body." });

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var request = JsonSerializer.Deserialize<BroadcastNotificationRequest>(body, options);

            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "Message is required." });

            var now = DateTime.UtcNow;
            var countdownSpan = new TimeSpan(
                request.CountdownDays,
                request.CountdownHours,
                request.CountdownMinutes,
                request.CountdownSeconds);

            var notification = new BroadcastNotification
            {
                Message = request.Message.Trim(),
                AdminUsername = username,
                PostedAt = now,
                ExpiresAt = now + countdownSpan,
                CountdownDays = request.CountdownDays,
                CountdownHours = request.CountdownHours,
                CountdownMinutes = request.CountdownMinutes,
                CountdownSeconds = request.CountdownSeconds
            };

            lock (_lock)
            {
                _activeNotification = notification;
            }

            SglLogger.Information(
                "Broadcast notification set by {User}: \"{Message}\" (expires in {Days}d {Hours}h {Minutes}m {Seconds}s)",
                username, notification.Message,
                request.CountdownDays, request.CountdownHours,
                request.CountdownMinutes, request.CountdownSeconds);

            return Results.Ok(new
            {
                status = "sent",
                notification
            });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"BroadcastNotification POST error: {ex.Message}");
            return Results.Problem("Failed to save broadcast notification.");
        }
    }

    private static IResult HandleGet()
    {
        try
        {
            BroadcastNotification? current;
            lock (_lock)
            {
                current = _activeNotification;
            }

            // If expired, clear it
            if (current != null && DateTime.UtcNow >= current.ExpiresAt)
            {
                lock (_lock)
                {
                    _activeNotification = null;
                }
                return Results.Ok(new { notification = (BroadcastNotification?)null });
            }

            return Results.Ok(new { notification = current });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"BroadcastNotification GET error: {ex.Message}");
            return Results.Ok(new { notification = (BroadcastNotification?)null });
        }
    }

    private static IResult HandleDelete(HttpContext context)
    {
        try
        {
            var role = context.Items["Role"]?.ToString();
            if (role != "admin")
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);

            var username = context.Items.TryGetValue("Username", out var u)
                ? u?.ToString() ?? "admin"
                : "admin";

            lock (_lock)
            {
                _activeNotification = null;
            }

            SglLogger.Information("Broadcast notification cleared by {User}", username);
            return Results.Ok(new { status = "cleared" });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"BroadcastNotification DELETE error: {ex.Message}");
            return Results.Problem("Failed to clear broadcast notification.");
        }
    }
}

/// <summary>
/// The request body for posting a broadcast notification with countdown.
/// </summary>
public class BroadcastNotificationRequest
{
    public string Message { get; set; } = "";
    public int CountdownDays { get; set; }
    public int CountdownHours { get; set; }
    public int CountdownMinutes { get; set; }
    public int CountdownSeconds { get; set; }
}

/// <summary>
/// The stored broadcast notification with admin info, timestamps, and countdown values.
/// </summary>
public class BroadcastNotification
{
    public string Message { get; set; } = "";
    public string AdminUsername { get; set; } = "";
    public DateTime PostedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int CountdownDays { get; set; }
    public int CountdownHours { get; set; }
    public int CountdownMinutes { get; set; }
    public int CountdownSeconds { get; set; }
}
