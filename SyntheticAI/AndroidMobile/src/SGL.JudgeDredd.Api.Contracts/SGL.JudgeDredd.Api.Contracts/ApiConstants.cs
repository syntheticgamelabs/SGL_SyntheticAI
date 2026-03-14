namespace SGL.JudgeDredd.Api.Contracts;

/// <summary>
/// Shared constants for the SyntheticAI client-server API.
/// </summary>
public static class ApiConstants
{
    public const int DefaultPort = 5000;
    public const int LegacyPort = 7743;
    public const string ApiVersion = "v1";
    public const string ApiPrefix = $"/api/{ApiVersion}";

    // Client endpoints
    public const string ClientRegister = $"{ApiPrefix}/clients/register";
    public const string ClientLogin = $"{ApiPrefix}/clients/login";
    public const string ClientHeartbeat = $"{ApiPrefix}/clients/heartbeat";

    // Signature endpoints
    public const string SignatureCheck = $"{ApiPrefix}/signatures/check";
    public const string SignatureDownload = $"{ApiPrefix}/signatures/download";

    // Threat analysis endpoints
    public const string ThreatAnalyze = $"{ApiPrefix}/threats/analyze";

    // Log endpoints
    public const string LogUpload = $"{ApiPrefix}/logs/upload";
    public const string CrashReport = $"{ApiPrefix}/logs/crash";

    // Command & control endpoints
    public const string CommandSend = $"{ApiPrefix}/commands/send";
    public const string CommandPending = $"{ApiPrefix}/commands/pending";  // + /{deviceId}
    public const string CommandResult = $"{ApiPrefix}/commands/result";
    public const string Events = $"{ApiPrefix}/events";
    public const string EventStream = $"{ApiPrefix}/events/stream";       // + /{clientId}

    // WebSocket gateway
    public const string WebSocketGateway = "/ws/gateway";

    // Server status
    public const string ServerStatus = $"{ApiPrefix}/server/status";

    // Auth header
    public const string AuthHeaderName = "Authorization";
    public const string AuthScheme = "Bearer";
}
