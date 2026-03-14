using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Server.ClientManagement;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Server status endpoint (public, no auth required).
/// </summary>
public static class ServerStatusEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ServerStatus, HandleStatus);

        // Lightweight health check endpoint for Cloudflare tunnel health probes
        app.MapGet(ApiConstants.ApiPrefix + "/server/health", () =>
            Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        // APK file download endpoint - serves the mobile APK from data/mobile/ folder
        app.MapGet(ApiConstants.ApiPrefix + "/mobile/download", (HttpContext context) =>
        {
            var mobileDir = Path.Combine(AppContext.BaseDirectory, "data", "mobile");
            if (!Directory.Exists(mobileDir))
                return Results.NotFound(new { error = "Mobile APK not available on this server." });

            var apkFiles = Directory.GetFiles(mobileDir, "*.apk");
            if (apkFiles.Length == 0)
                return Results.NotFound(new { error = "No APK file found in data/mobile/ directory." });

            var apkPath = apkFiles[0];
            var fileName = Path.GetFileName(apkPath);
            return Results.File(apkPath, "application/vnd.android.package-archive", fileName);
        }).AllowAnonymous();

        // APK info endpoint - returns file size
        app.MapGet(ApiConstants.ApiPrefix + "/mobile/download/info", () =>
        {
            var mobileDir = Path.Combine(AppContext.BaseDirectory, "data", "mobile");
            if (!Directory.Exists(mobileDir))
                return Results.Ok(new { available = false, size = "N/A" });

            var apkFiles = Directory.GetFiles(mobileDir, "*.apk");
            if (apkFiles.Length == 0)
                return Results.Ok(new { available = false, size = "N/A" });

            var fi = new FileInfo(apkFiles[0]);
            var sizeMb = fi.Length / (1024.0 * 1024.0);
            var sizeGb = fi.Length / (1024.0 * 1024.0 * 1024.0);
            var sizeDisplay = sizeGb >= 1 ? $"{sizeGb:F2} GB" : $"{sizeMb:F1} MB";

            return Results.Ok(new { available = true, size = sizeDisplay, bytes = fi.Length, fileName = fi.Name });
        }).AllowAnonymous();

        // Desktop client download endpoint
        app.MapGet(ApiConstants.ApiPrefix + "/client/download", (HttpContext context) =>
        {
            var clientDir = Path.Combine(AppContext.BaseDirectory, "data", "client");
            if (!Directory.Exists(clientDir))
                return Results.NotFound(new { error = "Desktop client installer not available." });

            var exeFiles = Directory.GetFiles(clientDir, "*.exe");
            if (exeFiles.Length == 0)
                return Results.NotFound(new { error = "No installer found in data/client/ directory." });

            var exePath = exeFiles[0];
            var fileName = Path.GetFileName(exePath);
            return Results.File(exePath, "application/octet-stream", fileName);
        }).AllowAnonymous();

        app.MapGet(ApiConstants.ApiPrefix + "/client/download/info", () =>
        {
            var clientDir = Path.Combine(AppContext.BaseDirectory, "data", "client");
            if (!Directory.Exists(clientDir))
                return Results.Ok(new { available = false, size = "N/A" });

            var exeFiles = Directory.GetFiles(clientDir, "*.exe");
            if (exeFiles.Length == 0)
                return Results.Ok(new { available = false, size = "N/A" });

            var fi = new FileInfo(exeFiles[0]);
            var sizeMb = fi.Length / (1024.0 * 1024.0);
            return Results.Ok(new { available = true, size = $"{sizeMb:F1} MB", bytes = fi.Length, fileName = fi.Name });
        }).AllowAnonymous();

        // Linux client download endpoint
        app.MapGet(ApiConstants.ApiPrefix + "/linux-client/download", (HttpContext context) =>
        {
            var linuxDir = Path.Combine(AppContext.BaseDirectory, "data", "linux-client");
            if (!Directory.Exists(linuxDir))
                return Results.NotFound(new { error = "Linux client not available." });

            var tarFiles = Directory.GetFiles(linuxDir, "*.tar.gz");
            if (tarFiles.Length == 0)
                tarFiles = Directory.GetFiles(linuxDir, "*");
            if (tarFiles.Length == 0)
                return Results.NotFound(new { error = "No Linux client package found." });

            var filePath = tarFiles[0];
            var fileName = Path.GetFileName(filePath);
            return Results.File(filePath, "application/gzip", fileName);
        }).AllowAnonymous();

        app.MapGet(ApiConstants.ApiPrefix + "/linux-client/download/info", () =>
        {
            var linuxDir = Path.Combine(AppContext.BaseDirectory, "data", "linux-client");
            if (!Directory.Exists(linuxDir))
                return Results.Ok(new { available = false, size = "N/A" });

            var tarFiles = Directory.GetFiles(linuxDir, "*.tar.gz");
            if (tarFiles.Length == 0)
                tarFiles = Directory.GetFiles(linuxDir, "*");
            if (tarFiles.Length == 0)
                return Results.Ok(new { available = false, size = "N/A" });

            var fi = new FileInfo(tarFiles[0]);
            var sizeMb = fi.Length / (1024.0 * 1024.0);
            return Results.Ok(new { available = true, size = $"{sizeMb:F1} MB", bytes = fi.Length, fileName = fi.Name });
        }).AllowAnonymous();
    }

    private static async Task<IResult> HandleStatus(
        JudgeDreddApiServer server,
        ConnectedClientTracker tracker,
        ILlmService? llmService = null,
        IKnowledgeBase? knowledgeBase = null)
    {
        // Dynamic signature count from knowledge base
        var sigCount = 0;
        if (knowledgeBase != null)
        {
            try { sigCount = await knowledgeBase.GetSignatureCountAsync(); }
            catch { /* graceful degradation */ }
        }

        // Dynamic LLM model name from service
        var llmModelName = "Not loaded";
        if (llmService?.IsModelLoaded == true)
        {
            try { llmModelName = llmService.ModelName ?? "Unknown Model"; }
            catch { llmModelName = "Loaded"; }
        }

        var response = new ServerStatusResponse
        {
            ServerVersion = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion,
            Uptime = DateTime.UtcNow - server.StartedAt,
            OnlineClients = tracker.OnlineCount,
            TotalRegisteredClients = tracker.TotalCount,
            SignatureVersion = SGL.JudgeDredd.Shared.VersionInfo.DefaultSignatureVersion,
            SignatureCount = sigCount,
            LlmModelLoaded = llmService?.IsModelLoaded ?? false,
            LlmModelName = llmModelName
        };

        return Results.Ok(response);
    }
}
