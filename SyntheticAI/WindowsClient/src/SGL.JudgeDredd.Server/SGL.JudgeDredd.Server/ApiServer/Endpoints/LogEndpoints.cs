using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Log upload and crash report endpoints.
/// </summary>
public static class LogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.LogUpload, HandleLogUpload);
        app.MapPost(ApiConstants.CrashReport, HandleCrashReport);
    }

    private static async Task<IResult> HandleLogUpload(
        LogUploadRequest request,
        ClientDataStore dataStore)
    {
        if (request.ClientId == Guid.Empty)
            return Results.BadRequest(new { error = "ClientId is required." });

        await dataStore.SaveLogAsync(request.ClientId, request.LogType, request.Content);

        SglLogger.Information("Log uploaded from client {ClientId}: {LogType} ({Length} chars)",
            request.ClientId, request.LogType, request.Content.Length);

        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> HandleCrashReport(
        LogUploadRequest request,
        ClientDataStore dataStore)
    {
        if (request.ClientId == Guid.Empty)
            return Results.BadRequest(new { error = "ClientId is required." });

        await dataStore.SaveCrashLogAsync(request.ClientId, request.Content);

        SglLogger.Information("Crash report received from client {ClientId} ({Machine})",
            request.ClientId, request.MachineName);

        return Results.Ok(new { success = true });
    }
}
