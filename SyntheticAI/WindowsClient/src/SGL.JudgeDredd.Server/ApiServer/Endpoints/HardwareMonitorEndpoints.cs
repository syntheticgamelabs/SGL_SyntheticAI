using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Admin-only hardware monitoring endpoints.
/// GET  /api/v1/admin/hardware         - Get current hardware snapshot
/// GET  /api/v1/admin/hardware/stream  - SSE stream of hardware data
/// </summary>
public static class HardwareMonitorEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/admin/hardware", (Delegate)HandleGetSnapshot);
        app.MapGet(ApiConstants.ApiPrefix + "/admin/hardware/stream", (Delegate)HandleStream);
    }

    private static IResult HandleGetSnapshot(HttpContext context, HardwareMonitorService monitor)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var snapshot = monitor.GetSnapshot();
            return Results.Json(snapshot, JsonOptions);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Hardware monitor error: {ex.Message}");
            return Results.Problem("Failed to retrieve hardware data.");
        }
    }

    private static async Task HandleStream(HttpContext context, HardwareMonitorService monitor)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("{\"error\":\"Admin access required\"}");
            return;
        }

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                var snapshot = monitor.GetSnapshot();
                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n");
                await context.Response.Body.FlushAsync();
                await Task.Delay(3000, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) { }
    }
}
