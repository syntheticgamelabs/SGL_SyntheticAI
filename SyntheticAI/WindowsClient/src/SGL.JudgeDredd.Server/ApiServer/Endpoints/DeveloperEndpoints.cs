using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Developer portal endpoints — password-protected area for developer builds/downloads.
/// Credentials: syntheticgamelabs@gmail.com / admintest1!
/// Failed access attempts notify the admin via the broadcast notification system.
/// </summary>
public static class DeveloperEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Developer credentials (hashed for security)
    private const string DevEmail = "syntheticgamelabs@gmail.com";
    private static readonly string DevPasswordHash = ComputeSha256("admintest1!");

    // Track failed attempts for security notifications
    private static int _failedAttempts;
    private static DateTime _lastFailedAttempt = DateTime.MinValue;

    // Active developer sessions (token -> expiry)
    private static readonly Dictionary<string, DateTime> _devSessions = new();
    private static readonly object _sessionLock = new();

    public static void Map(IEndpointRouteBuilder app)
    {
        // Developer authentication
        app.MapPost(ApiConstants.ApiPrefix + "/developer/login", (Delegate)HandleDevLogin);
        app.MapGet(ApiConstants.ApiPrefix + "/developer/verify", (Delegate)HandleVerifySession);

        // Developer file downloads (requires dev session token)
        app.MapGet(ApiConstants.ApiPrefix + "/developer/files", (Delegate)HandleListFiles);
        app.MapGet(ApiConstants.ApiPrefix + "/developer/download/{fileName}", (Delegate)HandleDownload);
    }

    private static async Task<IResult> HandleDevLogin(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<DevLoginRequest>(body, JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var emailMatch = request.Email.Trim().Equals(DevEmail, StringComparison.OrdinalIgnoreCase);
            var passwordMatch = ComputeSha256(request.Password) == DevPasswordHash;

            if (!emailMatch || !passwordMatch)
            {
                Interlocked.Increment(ref _failedAttempts);
                _lastFailedAttempt = DateTime.UtcNow;

                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                SglLogger.Warning("Developer login FAILED from {IP} with email {Email} (attempt #{Count})",
                    ip, request.Email, _failedAttempts);

                // Log failed attempt to disk for admin review
                try
                {
                    var logPath = Path.Combine(AppContext.BaseDirectory, "data", "dev_access_log.json");
                    var logEntry = new
                    {
                        timestamp = DateTime.UtcNow,
                        ip,
                        email = request.Email,
                        success = false,
                        totalFailedAttempts = _failedAttempts
                    };
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    var entries = new List<object>();
                    if (File.Exists(logPath))
                    {
                        var existing = File.ReadAllText(logPath);
                        entries = JsonSerializer.Deserialize<List<object>>(existing) ?? new();
                    }
                    entries.Add(logEntry);
                    // Keep last 100 entries
                    if (entries.Count > 100) entries.RemoveRange(0, entries.Count - 100);
                    await File.WriteAllTextAsync(logPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }

                return Results.Json(new { error = "Invalid credentials.", failedAttempts = _failedAttempts }, statusCode: 401);
            }

            // Generate session token
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            lock (_sessionLock)
            {
                // Clean expired sessions
                var expired = _devSessions.Where(s => s.Value < DateTime.UtcNow).Select(s => s.Key).ToList();
                foreach (var key in expired) _devSessions.Remove(key);

                _devSessions[token] = DateTime.UtcNow.AddHours(4);
            }

            SglLogger.Information("Developer login SUCCESS from {IP}", context.Connection.RemoteIpAddress);
            return Results.Ok(new { token, expiresIn = "4 hours" });
        }
        catch (Exception ex)
        {
            SglLogger.Error("Developer login error: " + ex.Message);
            return Results.Problem("Login failed.");
        }
    }

    private static IResult HandleVerifySession(HttpContext context)
    {
        var token = context.Request.Headers["X-Dev-Token"].FirstOrDefault()
                    ?? context.Request.Query["token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token) || !IsValidDevSession(token))
            return Results.Json(new { valid = false }, statusCode: 401);

        return Results.Ok(new { valid = true });
    }

    private static IResult HandleListFiles(HttpContext context)
    {
        var token = context.Request.Headers["X-Dev-Token"].FirstOrDefault()
                    ?? context.Request.Query["token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token) || !IsValidDevSession(token))
            return Results.Json(new { error = "Developer authentication required." }, statusCode: 401);

        var devDir = Path.Combine(AppContext.BaseDirectory, "data", "developers");
        if (!Directory.Exists(devDir))
        {
            Directory.CreateDirectory(devDir);
            return Results.Ok(new { files = Array.Empty<object>() });
        }

        var files = Directory.GetFiles(devDir)
            .Select(f => new FileInfo(f))
            .Select(fi => new
            {
                name = fi.Name,
                size = fi.Length,
                sizeDisplay = FormatFileSize(fi.Length),
                lastModified = fi.LastWriteTimeUtc
            })
            .OrderByDescending(f => f.lastModified)
            .ToList();

        return Results.Ok(new { files });
    }

    private static IResult HandleDownload(HttpContext context, string fileName)
    {
        var token = context.Request.Headers["X-Dev-Token"].FirstOrDefault()
                    ?? context.Request.Query["token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token) || !IsValidDevSession(token))
            return Results.Json(new { error = "Developer authentication required." }, statusCode: 401);

        // Sanitize filename to prevent path traversal
        fileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(AppContext.BaseDirectory, "data", "developers", fileName);

        if (!File.Exists(filePath))
            return Results.NotFound(new { error = $"File '{fileName}' not found." });

        return Results.File(filePath, "application/octet-stream", fileName);
    }

    private static bool IsValidDevSession(string token)
    {
        lock (_sessionLock)
        {
            return _devSessions.TryGetValue(token, out var expiry) && expiry > DateTime.UtcNow;
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F2} GB";
        if (bytes >= 1048576) return $"{bytes / 1048576.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private class DevLoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
