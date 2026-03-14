using Microsoft.AspNetCore.Http;
using SGL.JudgeDredd.Server.ApiServer.Services;

namespace SGL.JudgeDredd.Server.ApiServer.Middleware;

/// <summary>
/// Middleware that records every incoming HTTP request into the CloudflareService
/// for DDoS detection and request rate monitoring, and tracks website visitors
/// via WebsiteMetricsService.
/// Must be registered early in the pipeline (before auth) so all requests are tracked.
/// </summary>
public class RequestTrackingMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Download endpoint paths mapped to their platform names for metrics tracking.
    /// </summary>
    private static readonly Dictionary<string, string> DownloadPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        { "/api/v1/client/download", "windows" },
        { "/api/v1/mobile/download", "android" },
        { "/api/v1/linux-client/download", "linux" },
        { "/api/v1/copilot/plugin/download", "copilot-plugin" },
        { "/api/v1/copilot/bundle/download", "copilot-bundle" },
    };

    public RequestTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CloudflareService cloudflareService, WebsiteMetricsService websiteMetrics)
    {
        // Record the request before processing
        cloudflareService.RecordRequest(isError: false);

        var path = context.Request.Path.Value ?? string.Empty;

        // Extract the real client IP (handles Cloudflare, proxies, and direct connections)
        var realIp = GetRealIp(context);

        // Get country code from CDN/proxy headers
        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault()
                   ?? context.Request.Headers["X-Country-Code"].FirstOrDefault();

        // Track ALL requests as unique visitors (including API calls).
        // This ensures visitors whose browsers load the SPA (which makes API calls)
        // are counted even if the initial page load was served by a CDN cache.
        websiteMetrics.RecordVisitor(realIp, country);

        // Track download requests
        if (DownloadPaths.TryGetValue(path, out var platform))
        {
            websiteMetrics.RecordDownload(platform);
        }
        // Track website page views (non-API, non-WebSocket paths)
        else if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            websiteMetrics.RecordPageView(path);
        }

        await _next(context);

        // If the response is an error (4xx/5xx), record it as an error for DDoS ratio tracking
        if (context.Response.StatusCode >= 400)
        {
            cloudflareService.RecordRequest(isError: true);
        }
    }

    /// <summary>
    /// Extracts the real client IP from the request, checking CDN/proxy headers first.
    /// Priority: CF-Connecting-IP > X-Real-IP > X-Forwarded-For (first entry) > RemoteIpAddress.
    /// </summary>
    private static string? GetRealIp(HttpContext context)
    {
        // Cloudflare sets this to the original client IP
        var cfIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp))
            return cfIp.Trim();

        // Nginx/other proxies often set X-Real-IP
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        // Standard proxy header — take the first (leftmost) IP which is the original client
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
                return firstIp;
        }

        // Direct connection — use the TCP connection IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}
