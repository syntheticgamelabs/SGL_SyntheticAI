using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Dark web / data breach monitoring endpoints.
/// Allows clients to check emails, domains, and passwords against known breach databases.
/// </summary>
public static class BreachDetectionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/breach/check-email", (Delegate)HandleCheckEmail);
        app.MapPost(ApiConstants.ApiPrefix + "/breach/check-domain", (Delegate)HandleCheckDomain);
        app.MapPost(ApiConstants.ApiPrefix + "/breach/check-password", (Delegate)HandleCheckPassword);
        app.MapGet(ApiConstants.ApiPrefix + "/breach/watchlist", (Delegate)HandleGetWatchlist);
        app.MapPost(ApiConstants.ApiPrefix + "/breach/watchlist", (Delegate)HandleAddToWatchlist);
        app.MapDelete(ApiConstants.ApiPrefix + "/breach/watchlist/{value}", (Delegate)HandleRemoveFromWatchlist);
    }

    private static async Task<IResult> HandleCheckEmail(HttpContext context, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { error = "Breach detection service not available." });

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<BreachCheckRequest>(body, JsonOptions);

        if (request == null || string.IsNullOrWhiteSpace(request.Query))
            return Results.BadRequest(new { error = "Email address is required." });

        var result = await breachService.CheckEmailAsync(request.Query, context.RequestAborted);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCheckDomain(HttpContext context, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { error = "Breach detection service not available." });

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<BreachCheckRequest>(body, JsonOptions);

        if (request == null || string.IsNullOrWhiteSpace(request.Query))
            return Results.BadRequest(new { error = "Domain is required." });

        var result = await breachService.CheckDomainAsync(request.Query, context.RequestAborted);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCheckPassword(HttpContext context, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { error = "Breach detection service not available." });

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<BreachCheckRequest>(body, JsonOptions);

        if (request == null || string.IsNullOrWhiteSpace(request.Query))
            return Results.BadRequest(new { error = "Password is required." });

        var result = await breachService.CheckPasswordAsync(request.Query, context.RequestAborted);
        return Results.Ok(result);
    }

    private static IResult HandleGetWatchlist(HttpContext context, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { items = Array.Empty<object>() });

        return Results.Ok(new { items = breachService.GetMonitoredItems() });
    }

    private static async Task<IResult> HandleAddToWatchlist(HttpContext context, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { error = "Breach detection service not available." });

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<WatchlistRequest>(body, JsonOptions);

        if (request == null || string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { error = "Value is required." });

        breachService.AddMonitoredItem(request.Value, request.Type ?? "email");
        return Results.Ok(new { status = "added", value = request.Value });
    }

    private static IResult HandleRemoveFromWatchlist(HttpContext context, string value, IBreachDetectionService? breachService = null)
    {
        if (breachService == null)
            return Results.Ok(new { error = "Breach detection service not available." });

        breachService.RemoveMonitoredItem(value);
        return Results.Ok(new { status = "removed", value });
    }

    private class BreachCheckRequest
    {
        public string Query { get; set; } = "";
    }

    private class WatchlistRequest
    {
        public string Value { get; set; } = "";
        public string? Type { get; set; }
    }
}
