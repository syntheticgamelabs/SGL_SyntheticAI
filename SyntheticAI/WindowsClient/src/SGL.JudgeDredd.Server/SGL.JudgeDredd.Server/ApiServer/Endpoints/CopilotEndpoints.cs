using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// AIBlue Copilot download endpoints.
/// Serves the Copilot VS Code plugin and the LM Studio + LLM bundle from data/copilot/.
/// </summary>
public static class CopilotEndpoints
{
    private const string PluginFileName = "AIBlueCopilotVN02.zip";
    private const string BundleFileName = "LMSTUDIO+LLM.zip";

    public static void Map(IEndpointRouteBuilder app)
    {
        // Copilot VS Code plugin download
        app.MapGet(ApiConstants.ApiPrefix + "/copilot/plugin/download", HandlePluginDownload)
            .AllowAnonymous();

        // Copilot plugin info endpoint
        app.MapGet(ApiConstants.ApiPrefix + "/copilot/plugin/download/info", HandlePluginInfo)
            .AllowAnonymous();

        // LM Studio + LLM bundle download
        app.MapGet(ApiConstants.ApiPrefix + "/copilot/bundle/download", HandleBundleDownload)
            .AllowAnonymous();

        // LM Studio + LLM bundle info endpoint
        app.MapGet(ApiConstants.ApiPrefix + "/copilot/bundle/download/info", HandleBundleInfo)
            .AllowAnonymous();
    }

    private static IResult HandlePluginDownload(HttpContext context)
    {
        var copilotDir = Path.Combine(AppContext.BaseDirectory, "data", "copilot");
        var filePath = Path.Combine(copilotDir, PluginFileName);

        if (!File.Exists(filePath))
        {
            SglLogger.Warning("Copilot plugin download requested but file not found: {Path}", filePath);
            return Results.NotFound(new { error = "AIBlue Copilot plugin not available on this server." });
        }

        SglLogger.Information("Serving Copilot plugin download: {FileName}", PluginFileName);
        return Results.File(filePath, "application/zip", PluginFileName);
    }

    private static IResult HandlePluginInfo()
    {
        var copilotDir = Path.Combine(AppContext.BaseDirectory, "data", "copilot");
        var filePath = Path.Combine(copilotDir, PluginFileName);

        if (!File.Exists(filePath))
            return Results.Ok(new { available = false, size = "N/A" });

        var fi = new FileInfo(filePath);
        var sizeMb = fi.Length / (1024.0 * 1024.0);
        var sizeGb = fi.Length / (1024.0 * 1024.0 * 1024.0);
        var sizeDisplay = sizeGb >= 1 ? $"{sizeGb:F2} GB" : $"{sizeMb:F1} MB";

        return Results.Ok(new { available = true, size = sizeDisplay, bytes = fi.Length, fileName = fi.Name });
    }

    private static IResult HandleBundleDownload(HttpContext context)
    {
        var copilotDir = Path.Combine(AppContext.BaseDirectory, "data", "copilot");
        var filePath = Path.Combine(copilotDir, BundleFileName);

        if (!File.Exists(filePath))
        {
            SglLogger.Warning("Copilot bundle download requested but file not found: {Path}", filePath);
            return Results.NotFound(new { error = "LM Studio + LLM bundle not available on this server." });
        }

        SglLogger.Information("Serving Copilot bundle download: {FileName}", BundleFileName);
        return Results.File(filePath, "application/zip", BundleFileName);
    }

    private static IResult HandleBundleInfo()
    {
        var copilotDir = Path.Combine(AppContext.BaseDirectory, "data", "copilot");
        var filePath = Path.Combine(copilotDir, BundleFileName);

        if (!File.Exists(filePath))
            return Results.Ok(new { available = false, size = "N/A" });

        var fi = new FileInfo(filePath);
        var sizeMb = fi.Length / (1024.0 * 1024.0);
        var sizeGb = fi.Length / (1024.0 * 1024.0 * 1024.0);
        var sizeDisplay = sizeGb >= 1 ? $"{sizeGb:F2} GB" : $"{sizeMb:F1} MB";

        return Results.Ok(new { available = true, size = sizeDisplay, bytes = fi.Length, fileName = fi.Name });
    }
}
