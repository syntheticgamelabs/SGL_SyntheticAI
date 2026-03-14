using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SGL.JudgeDredd.Api.Contracts;

namespace SGL.JudgeDredd.Server.ApiServer.Middleware;

/// <summary>
/// Middleware that validates JWT tokens in the Authorization header for protected endpoints.
/// Bypasses authentication for registration, login, and server status endpoints.
/// On successful validation, sets ClientId and Username on HttpContext.Items for downstream use.
/// </summary>
public class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Endpoints that do not require authentication.
    /// </summary>
    private static readonly HashSet<string> PublicEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/health",
        ApiConstants.ClientRegister,
        ApiConstants.ClientLogin,
        ApiConstants.ServerStatus,
    };

    /// <summary>
    /// Path prefixes that are public (WebSocket gateway handles its own auth via query string).
    /// </summary>
    private static readonly string[] PublicPrefixes = new[]
    {
        "/ws/",
        "/api/v1/server/health",
        "/api/v1/mobile/download",
        "/api/v1/client/download",
        "/api/v1/linux-client/download",
        "/api/v1/faq",
        "/api/v1/metrics/public",
        "/api/v1/llm/models",
        "/api/v1/copilot/",
        "/api/v1/developer/login",
        "/api/v1/broadcast-notification",
    };

    /// <summary>
    /// Paths that are only public for GET requests (e.g., website content is readable but not writable without auth).
    /// </summary>
    private static readonly string[] PublicGetOnlyPrefixes = new[]
    {
        "/api/v1/website/content",
        "/api/v1/admin/broadcast-notification",
    };

    public JwtAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, JwtService jwtService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Allow public endpoints without auth
        if (PublicEndpoints.Contains(path))
        {
            await _next(context);
            return;
        }

        // Allow WebSocket paths (they authenticate via query string token)
        foreach (var prefix in PublicPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // Allow GET-only public paths (e.g., website content is readable without auth)
        if (HttpMethods.IsGet(context.Request.Method))
        {
            foreach (var prefix in PublicGetOnlyPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }
            }
        }

        // Check for API paths only (don't block non-API requests)
        if (!path.StartsWith(ApiConstants.ApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Allow localhost admin access without JWT (server-mode admin panel)
        var remoteIp = context.Connection.RemoteIpAddress;
        var isLocalhost = remoteIp != null && (System.Net.IPAddress.IsLoopback(remoteIp) ||
                          remoteIp.Equals(System.Net.IPAddress.Parse("::1")));

        // Extract Bearer token from Authorization header
        var authHeader = context.Request.Headers[ApiConstants.AuthHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith($"{ApiConstants.AuthScheme} ", StringComparison.OrdinalIgnoreCase))
        {
            // If localhost and no token, grant admin access (server-mode admin panel)
            if (isLocalhost)
            {
                context.Items["Username"] = "LocalAdmin";
                context.Items["Role"] = "admin";
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid authorization header. Expected: Bearer <jwt_token>" });
            return;
        }

        var token = authHeader[$"{ApiConstants.AuthScheme} ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Authorization token is empty." });
            return;
        }

        // Validate the JWT token
        var principal = jwtService.ValidateToken(token);
        if (principal == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired JWT token." });
            return;
        }

        // Extract claims and store on HttpContext.Items for downstream endpoints
        var clientId = JwtService.GetClientIdFromPrincipal(principal);
        var username = JwtService.GetUsernameFromPrincipal(principal);

        if (clientId.HasValue)
        {
            context.Items["ClientId"] = clientId.Value;
        }

        if (!string.IsNullOrEmpty(username))
        {
            context.Items["Username"] = username;
        }

        context.Items["ClaimsPrincipal"] = principal;

        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        if (roleClaim != null)
        {
            context.Items["Role"] = roleClaim.Value;
        }

        await _next(context);
    }
}
