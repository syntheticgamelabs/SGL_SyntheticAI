using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Admin-only LLM knowledge backup endpoint.
/// POST /api/v1/admin/llm/backup  - Backs up all LLM learned data (chat sessions, knowledge base)
/// GET  /api/v1/admin/llm/backups - Lists existing backups
/// </summary>
public static class LlmBackupEndpoints
{
    private static readonly string BackupDir = Path.Combine(AppContext.BaseDirectory, "data", "llm_backups");
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/admin/llm/backup", (Delegate)HandleBackup);
        app.MapGet(ApiConstants.ApiPrefix + "/admin/llm/backups", (Delegate)HandleListBackups);
    }

    private static async Task<IResult> HandleBackup(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var username = context.Items["Username"]?.ToString() ?? "admin";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupName = $"llm_backup_{timestamp}";
            var backupPath = Path.Combine(BackupDir, backupName);

            Directory.CreateDirectory(backupPath);

            var files = new List<string>();

            // Backup knowledge base data
            var kbDir = Path.Combine(DataDir, "knowledge_base");
            if (Directory.Exists(kbDir))
            {
                CopyDirectory(kbDir, Path.Combine(backupPath, "knowledge_base"));
                files.Add("knowledge_base/");
            }

            // Backup threat knowledge
            var threatDir = Path.Combine(DataDir, "threat_knowledge");
            if (Directory.Exists(threatDir))
            {
                CopyDirectory(threatDir, Path.Combine(backupPath, "threat_knowledge"));
                files.Add("threat_knowledge/");
            }

            // Backup FAQ data (LLM-related Q&A)
            var faqDir = Path.Combine(DataDir, "faq");
            if (Directory.Exists(faqDir))
            {
                CopyDirectory(faqDir, Path.Combine(backupPath, "faq"));
                files.Add("faq/");
            }

            // Backup signatures
            var sigDir = Path.Combine(DataDir, "signatures");
            if (Directory.Exists(sigDir))
            {
                CopyDirectory(sigDir, Path.Combine(backupPath, "signatures"));
                files.Add("signatures/");
            }

            // Backup broadcast messages
            var broadcastFile = Path.Combine(DataDir, "broadcast.json");
            if (File.Exists(broadcastFile))
            {
                File.Copy(broadcastFile, Path.Combine(backupPath, "broadcast.json"));
                files.Add("broadcast.json");
            }

            // Backup VPN server data
            var vpnFile = Path.Combine(DataDir, "vpn_servers.json");
            if (File.Exists(vpnFile))
            {
                File.Copy(vpnFile, Path.Combine(backupPath, "vpn_servers.json"));
                files.Add("vpn_servers.json");
            }

            // Create a zip archive
            var zipPath = backupPath + ".zip";
            if (Directory.Exists(backupPath))
            {
                ZipFile.CreateFromDirectory(backupPath, zipPath);
                Directory.Delete(backupPath, recursive: true);
            }

            var fileInfo = new FileInfo(zipPath);

            SglLogger.Information("LLM knowledge backup created by {User}: {Path} ({Size} bytes)",
                username, zipPath, fileInfo.Length);

            return Results.Ok(new
            {
                message = "LLM knowledge backup created successfully",
                backupName,
                fileName = backupName + ".zip",
                sizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2),
                filesIncluded = files,
                createdBy = username,
                createdAt = DateTime.UtcNow,
                path = zipPath
            });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM backup error: {ex.Message}");
            return Results.Problem($"Failed to create backup: {ex.Message}");
        }
    }

    private static IResult HandleListBackups(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            Directory.CreateDirectory(BackupDir);
            var backups = Directory.GetFiles(BackupDir, "*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Select(f => new
                {
                    name = Path.GetFileNameWithoutExtension(f.Name),
                    fileName = f.Name,
                    sizeMB = Math.Round(f.Length / (1024.0 * 1024.0), 2),
                    createdAt = f.CreationTimeUtc
                })
                .ToList();

            return Results.Ok(new { backups, count = backups.Count });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM backup list error: {ex.Message}");
            return Results.Problem("Failed to list backups.");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
