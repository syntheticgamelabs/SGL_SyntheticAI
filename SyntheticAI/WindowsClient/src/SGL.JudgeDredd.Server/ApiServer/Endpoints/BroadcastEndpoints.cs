using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

public static class BroadcastEndpoints
{
    private static readonly string BroadcastFile =
        Path.Combine(AppContext.BaseDirectory, "data", "broadcast.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/broadcast", HandleGetBroadcast);
        app.MapPost(ApiConstants.ApiPrefix + "/broadcast", (Delegate)HandlePostBroadcast);
        app.MapDelete(ApiConstants.ApiPrefix + "/broadcast", HandleDeleteBroadcast);
    }

    private static IResult HandleGetBroadcast()
    {
        try
        {
            if (!File.Exists(BroadcastFile))
                return Results.Ok(new { message = (string?)null });

            var json = File.ReadAllText(BroadcastFile);
            var broadcast = JsonSerializer.Deserialize<BroadcastMessage>(json, JsonOptions);
            return Results.Ok(broadcast);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Broadcast GET error: {ex.Message}");
            return Results.Ok(new { message = (string?)null });
        }
    }

    private static async Task<IResult> HandlePostBroadcast(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var username = context.Items.TryGetValue("Username", out var u) ? u?.ToString() ?? "admin" : "admin";

            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body." });

            var request = JsonSerializer.Deserialize<BroadcastRequest>(body, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "Message is required." });

            var broadcast = new BroadcastMessage
            {
                Message = request.Message.Trim(),
                Priority = request.Priority ?? "info",
                SentBy = username,
                SentAt = DateTime.UtcNow
            };

            Directory.CreateDirectory(Path.GetDirectoryName(BroadcastFile)!);
            var json = JsonSerializer.Serialize(broadcast, JsonOptions);
            await File.WriteAllTextAsync(BroadcastFile, json);

            // Push broadcast to all connected WebSocket clients in real-time
            var wsGateway = context.RequestServices.GetService<WebSocketGateway>();
            if (wsGateway != null && wsGateway.ConnectedCount > 0)
            {
                var wsMessage = new GatewayMessage
                {
                    Type = "event",
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        eventType = "broadcast",
                        message = broadcast.Message,
                        priority = broadcast.Priority,
                        sentBy = broadcast.SentBy,
                        sentAt = broadcast.SentAt
                    }, JsonOptions)
                };
                await wsGateway.BroadcastAsync(wsMessage);
                SglLogger.Information("Broadcast pushed to {Count} WebSocket clients", wsGateway.ConnectedCount);
            }

            SglLogger.Information("Broadcast message set by {User}: {Message}", username, broadcast.Message);
            return Results.Ok(new { status = "sent", broadcast, wsClientsPushed = wsGateway?.ConnectedCount ?? 0 });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Broadcast POST error: {ex.Message}");
            return Results.Problem("Failed to save broadcast message.");
        }
    }

    private static IResult HandleDeleteBroadcast(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var username = context.Items.TryGetValue("Username", out var u) ? u?.ToString() ?? "admin" : "admin";

            if (File.Exists(BroadcastFile))
            {
                File.Delete(BroadcastFile);
                SglLogger.Information("Broadcast message cleared by {User}", username);
            }

            return Results.Ok(new { status = "cleared" });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Broadcast DELETE error: {ex.Message}");
            return Results.Problem("Failed to clear broadcast message.");
        }
    }
}
