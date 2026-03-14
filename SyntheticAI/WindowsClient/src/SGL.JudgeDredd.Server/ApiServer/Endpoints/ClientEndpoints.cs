using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Client registration, login, and heartbeat endpoints.
/// Registration and login now return JWT tokens instead of plain API keys.
/// Integrates with UserAccountStore for persistent account storage.
/// </summary>
public static class ClientEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ClientRegister, HandleRegister);
        app.MapPost(ApiConstants.ClientLogin, HandleLogin);
        app.MapPost(ApiConstants.ClientHeartbeat, HandleHeartbeat);
    }

    private static async Task<IResult> HandleRegister(
        ClientRegistrationRequest request,
        ConnectedClientTracker tracker,
        ClientDataStore dataStore,
        JwtService jwtService,
        UserAccountStore accountStore)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Results.BadRequest(new { error = "Username is required." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Password is required." });

        // Check persistent account store first
        if (accountStore.IsUsernameTaken(request.Username))
            return Results.Conflict(new { error = "Username already exists." });

        if (tracker.UsernameExists(request.Username))
            return Results.Conflict(new { error = "Username already exists." });

        var clientId = tracker.RegisterClient(
            request.Username,
            request.MachineName,
            request.ClientVersion,
            request.Platform,
            request.Password,
            request.DeviceModel,
            request.OsVersion);

        // Generate JWT token for the newly registered client
        var token = jwtService.GenerateToken(clientId, request.Username, request.MachineName);

        // Persist to disk (store token instead of legacy api key)
        await dataStore.SaveRegistrationAsync(clientId, request.Username,
            request.MachineName, request.ClientVersion, request.Platform, token,
            request.DeviceModel, request.OsVersion);

        // Persist password hash for login survival across restarts
        var passwordHash = tracker.GetPasswordHash(request.Username);
        if (passwordHash != null)
        {
            await dataStore.SavePasswordHashAsync(request.Username, passwordHash);
        }

        // Store in persistent user account store
        await accountStore.RegisterAsync(request.Username, request.Password,
            request.MachineName, request.Platform);

        var response = new ClientRegistrationResponse
        {
            ClientId = clientId,
            Token = token,
            ApiKey = token, // Backward compatibility: set ApiKey to the JWT token
            ServerVersion = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion
        };

        SglLogger.Information("Client registered: {Username} from {Machine} ({Platform})",
            request.Username, request.MachineName, request.Platform);

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleLogin(
        ClientLoginRequest request,
        ConnectedClientTracker tracker,
        JwtService jwtService,
        UserAccountStore accountStore)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Username and password are required." });

        // Try persistent account store first
        var (accountSuccess, accountMessage, account) = await accountStore.LoginAsync(request.Username, request.Password);
        if (accountSuccess && account != null)
        {
            // Account found in persistent store -- validate via tracker as well for session tracking
            var result = tracker.ValidateLogin(request.Username, request.Password);
            Guid clientId;
            string machineName;

            if (result != null)
            {
                (clientId, machineName) = result.Value;
            }
            else
            {
                // Account exists in persistent store but not in in-memory tracker (server restarted)
                // Re-register in the tracker with the persistent account info
                clientId = Guid.TryParse(account.ClientId, out var parsed) ? parsed : Guid.NewGuid();
                machineName = account.MachineName;
            }

            var role = account.IsAdmin ? "admin" : "device";
            var token = jwtService.GenerateToken(clientId, request.Username, machineName, role);

            var response = new ClientRegistrationResponse
            {
                ClientId = clientId,
                Token = token,
                ApiKey = token, // Backward compatibility
                ServerVersion = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion
            };

            SglLogger.Information("Client login: {Username} (ID: {ClientId}, Role: {Role})",
                request.Username, clientId, role);

            return Results.Ok(response);
        }

        // Fall back to in-memory tracker validation
        var trackerResult = tracker.ValidateLogin(request.Username, request.Password);
        if (trackerResult == null)
        {
            SglLogger.Warning("Failed login attempt for user: {Username}", request.Username);
            return Results.Json(
                new { error = accountMessage ?? "Invalid username or password." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        {
            var (clientId, machineName) = trackerResult.Value;

            // Tracker-only users (not in persistent store) default to device role
            var token = jwtService.GenerateToken(clientId, request.Username, machineName, "device");

            var response = new ClientRegistrationResponse
            {
                ClientId = clientId,
                Token = token,
                ApiKey = token, // Backward compatibility
                ServerVersion = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion
            };

            SglLogger.Information("Client login: {Username} (ID: {ClientId})",
                request.Username, clientId);

            return Results.Ok(response);
        }
    }

    private static IResult HandleHeartbeat(
        ClientHeartbeatRequest request,
        HttpContext context,
        ConnectedClientTracker tracker)
    {
        // Use the authenticated ClientId from JWT (set by JwtAuthMiddleware)
        // instead of trusting the ClientId from the request body.
        if (!context.Items.TryGetValue("ClientId", out var clientIdObj) || clientIdObj is not Guid authenticatedClientId)
            return Results.Json(new { error = "Unauthorized. No valid client identity." },
                statusCode: StatusCodes.Status401Unauthorized);

        var success = tracker.UpdateHeartbeat(
            authenticatedClientId,
            request.SignatureVersion,
            request.RealTimeProtectionActive,
            request.ThreatsDetected,
            request.FilesScanned,
            request.LlmModelLoaded,
            request.UptimeMinutes);

        if (!success)
            return Results.NotFound(new { error = "Client not registered." });

        var response = new ClientHeartbeatResponse
        {
            LatestSignatureVersion = "2025.02.23.1",
            HasUpdate = false,
            ServerMessage = null
        };

        return Results.Ok(response);
    }
}
