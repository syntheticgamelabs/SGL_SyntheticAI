using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Signature database check and download endpoints.
/// </summary>
public static class SignatureEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.SignatureCheck, HandleCheck);
        app.MapGet(ApiConstants.SignatureDownload, HandleDownload);
    }

    private static async Task<IResult> HandleCheck(HttpContext context, IKnowledgeBase? knowledgeBase = null)
    {
        var sigCount = 0;
        var latestVersion = SGL.JudgeDredd.Shared.VersionInfo.DefaultSignatureVersion;
        long downloadSize = 0;

        if (knowledgeBase != null)
        {
            try
            {
                sigCount = await knowledgeBase.GetSignatureCountAsync();

                // Compute real download size from signature database file if it exists
                var sigDbPath = Path.Combine(AppContext.BaseDirectory, "data", "signatures.db");
                if (File.Exists(sigDbPath))
                {
                    downloadSize = new FileInfo(sigDbPath).Length;
                }
                else if (sigCount > 0)
                {
                    // Estimate: ~200 bytes per signature entry in JSON format
                    downloadSize = sigCount * 200L;
                }

                // Build version from latest signature update timestamp
                var signatures = await knowledgeBase.GetAllSignaturesAsync();
                if (signatures.Count > 0)
                {
                    var latestDate = signatures.Max(s => s.LastUpdated);
                    latestVersion = $"{latestDate:yyyy.MM.dd}.{signatures.Count}";
                }
            }
            catch (Exception ex)
            {
                SglLogger.Warning("Failed to get signature count from KnowledgeBase: {Error}", ex.Message);
            }
        }

        var response = new SignatureUpdateResponse
        {
            Version = latestVersion,
            SignatureCount = sigCount,
            Available = sigCount > 0,
            DownloadSize = downloadSize,
            ReleaseDate = DateTime.UtcNow
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleDownload(HttpContext context, IKnowledgeBase? knowledgeBase = null)
    {
        var sigDbPath = Path.Combine(AppContext.BaseDirectory, "data", "signatures.db");

        // If the signature database file doesn't exist, generate one from KnowledgeBase
        if (!File.Exists(sigDbPath) && knowledgeBase != null)
        {
            try
            {
                var signatures = await knowledgeBase.GetAllSignaturesAsync();
                if (signatures.Count > 0)
                {
                    var sigData = signatures.Select(s => new
                    {
                        s.Sha256Hash,
                        s.Md5Hash,
                        s.Name,
                        s.Family,
                        Severity = (int)s.Severity,
                        s.Description,
                        s.Tags,
                        s.FirstSeen,
                        s.LastUpdated
                    });

                    var dir = Path.GetDirectoryName(sigDbPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    var json = JsonSerializer.Serialize(sigData, new JsonSerializerOptions { WriteIndented = false });
                    await File.WriteAllTextAsync(sigDbPath, json);

                    SglLogger.Information("Generated signature database with {Count} signatures at {Path}",
                        signatures.Count, sigDbPath);
                }
            }
            catch (Exception ex)
            {
                SglLogger.Warning("Failed to generate signature database: {Error}", ex.Message);
            }
        }

        if (!File.Exists(sigDbPath))
        {
            return Results.NotFound(new { error = "No signature database available for download." });
        }

        var fileBytes = await File.ReadAllBytesAsync(sigDbPath);
        return Results.File(fileBytes, "application/octet-stream", "signatures.db");
    }
}
