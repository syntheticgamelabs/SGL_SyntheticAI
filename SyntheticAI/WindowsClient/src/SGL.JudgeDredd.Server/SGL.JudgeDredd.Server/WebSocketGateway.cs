using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// WebSocket gateway for real-time bidirectional communication between the server and clients.
/// Clients connect to /ws/gateway?token=JWT_TOKEN and can send/receive JSON messages.
///
/// Message format:
/// {
///   "type": "command" | "event" | "result",
///   "payload": { ... }
/// }
///
/// Features:
/// - JWT authentication via query string token parameter
/// - Tracks connected WebSocket clients by device ID
/// - Routes commands from desktop clients to target devices
/// - Pushes events from devices to subscribed desktop clients
/// - Ping/pong keepalive at 30-second intervals
/// </summary>
public sealed class WebSocketGateway
{
    private readonly JwtService _jwtService;
    private readonly ConcurrentDictionary<Guid, WebSocketClient> _connectedClients = new();

    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebSocketGateway(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    /// <summary>
    /// Number of currently connected WebSocket clients.
    /// </summary>
    public int ConnectedCount => _connectedClients.Count;

    /// <summary>
    /// Get all connected client IDs.
    /// </summary>
    public IReadOnlyList<Guid> GetConnectedClientIds()
    {
        return _connectedClients.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Handle an incoming WebSocket connection request.
    /// Called from the WebSocket middleware pipeline.
    /// </summary>
    public async Task HandleConnectionAsync(HttpContext context)
    {
        // Validate JWT token from query string
        var token = context.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing token query parameter." });
            return;
        }

        var principal = _jwtService.ValidateToken(token);
        if (principal == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired JWT token." });
            return;
        }

        var clientId = JwtService.GetClientIdFromPrincipal(principal);
        var username = JwtService.GetUsernameFromPrincipal(principal);

        if (!clientId.HasValue)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Token does not contain a valid device_id claim." });
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "WebSocket connection expected." });
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        var wsClient = new WebSocketClient
        {
            ClientId = clientId.Value,
            Username = username ?? "unknown",
            Socket = webSocket,
            ConnectedAt = DateTime.UtcNow
        };

        // Remove any existing connection for this client (reconnect scenario)
        if (_connectedClients.TryRemove(clientId.Value, out var existingClient))
        {
            SglLogger.Information("WebSocket: Replacing existing connection for client {ClientId}", clientId.Value);
            await SafeCloseAsync(existingClient.Socket, "Replaced by new connection");
        }

        _connectedClients[clientId.Value] = wsClient;

        SglLogger.Information("WebSocket: Client connected - {Username} (ID: {ClientId}). Total: {Count}",
            username ?? "unknown", clientId.Value, _connectedClients.Count);

        try
        {
            // Send a welcome message
            var welcome = new GatewayMessage
            {
                Type = "event",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    eventType = "connected",
                    message = $"Welcome, {username}. Connection established.",
                    serverTime = DateTime.UtcNow
                }, JsonOptions)
            };
            await SendMessageAsync(webSocket, welcome);

            // Start the receive loop with ping/pong
            await RunClientLoopAsync(wsClient);
        }
        catch (WebSocketException ex)
        {
            SglLogger.Warning("WebSocket error for client {ClientId}: {Message}", clientId.Value, ex.Message);
        }
        catch (Exception ex)
        {
            SglLogger.Error("WebSocket unexpected error for client " + clientId.Value, ex);
        }
        finally
        {
            _connectedClients.TryRemove(clientId.Value, out _);
            await SafeCloseAsync(webSocket, "Connection ended");

        SglLogger.Information("WebSocket: Client disconnected - {Username} (ID: {ClientId}). Total: {Count}",
                username ?? "unknown", clientId.Value, _connectedClients.Count);
        }
    }

    /// <summary>
    /// Send a message to a specific connected client by device ID.
    /// Returns true if the message was sent successfully.
    /// </summary>
    public async Task<bool> SendToClientAsync(Guid clientId, GatewayMessage message)
    {
        if (!_connectedClients.TryGetValue(clientId, out var client))
            return false;

        if (client.Socket.State != WebSocketState.Open)
        {
            _connectedClients.TryRemove(clientId, out _);
            return false;
        }

        try
        {
            await SendMessageAsync(client.Socket, message);
            return true;
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to send WebSocket message to client {ClientId}: {Message}", clientId, ex.Message);
            _connectedClients.TryRemove(clientId, out _);
            return false;
        }
    }

    /// <summary>
    /// Broadcast a message to all connected WebSocket clients.
    /// </summary>
    public async Task BroadcastAsync(GatewayMessage message)
    {
        var tasks = new List<Task>();
        foreach (var kvp in _connectedClients)
        {
            tasks.Add(SendToClientAsync(kvp.Key, message));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Main receive loop for a connected WebSocket client.
    /// Handles incoming messages and sends periodic pings.
    /// </summary>
    private async Task RunClientLoopAsync(WebSocketClient client)
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        using var pingCts = new CancellationTokenSource();
        var pingTask = RunPingLoopAsync(client, pingCts.Token);

        try
        {
            while (client.Socket.State == WebSocketState.Open)
            {
                using var receiveCts = new CancellationTokenSource(ReceiveTimeout);

                WebSocketReceiveResult result;
                try
                {
                    result = await client.Socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), receiveCts.Token);
                }
                catch (OperationCanceledException)
                {
                    SglLogger.Warning("WebSocket: Receive timeout for client {ClientId}", client.ClientId);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    SglLogger.Information("WebSocket: Client {ClientId} sent close frame", client.ClientId);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.Clear();

                        await ProcessIncomingMessageAsync(client, json);
                    }
                }
                // Binary messages (pong responses) are handled automatically by the WebSocket protocol
            }
        }
        finally
        {
            pingCts.Cancel();
            try { await pingTask; } catch { /* ignore cancellation */ }
        }
    }

    /// <summary>
    /// Send periodic ping frames to keep the connection alive.
    /// </summary>
    private static async Task RunPingLoopAsync(WebSocketClient client, CancellationToken cancellationToken)
    {
        var pingPayload = Encoding.UTF8.GetBytes("ping");

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Socket.State == WebSocketState.Open)
            {
                await Task.Delay(PingInterval, cancellationToken);

                if (client.Socket.State == WebSocketState.Open)
                {
                    // Send a text-based ping message (application-level keepalive)
                    var pingMessage = new GatewayMessage
                    {
                        Type = "ping",
                        Payload = JsonSerializer.SerializeToElement(new
                        {
                            timestamp = DateTime.UtcNow
                        })
                    };

                    var json = JsonSerializer.Serialize(pingMessage, JsonOptions);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    await client.Socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken);

                    client.LastPingAt = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the connection is closing
        }
        catch (WebSocketException)
        {
            // Connection was lost
        }
    }

    /// <summary>
    /// Process an incoming JSON message from a WebSocket client.
    /// </summary>
    private async Task ProcessIncomingMessageAsync(WebSocketClient sender, string json)
    {
        GatewayMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<GatewayMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            SglLogger.Warning("WebSocket: Invalid JSON from client {ClientId}: {Error}", sender.ClientId, ex.Message);
            var errorMsg = new GatewayMessage
            {
                Type = "error",
                Payload = JsonSerializer.SerializeToElement(new { error = "Invalid JSON message format." }, JsonOptions)
            };
            await SendMessageAsync(sender.Socket, errorMsg);
            return;
        }

        if (message == null)
            return;

        SglLogger.Information("WebSocket: Received {Type} from {ClientId}", message.Type, sender.ClientId);

        switch (message.Type?.ToLowerInvariant())
        {
            case "command":
                await HandleCommandMessage(sender, message);
                break;

            case "event":
                await HandleEventMessage(sender, message);
                break;

            case "result":
                await HandleResultMessage(sender, message);
                break;

            case "pong":
                sender.LastPongAt = DateTime.UtcNow;
                break;

            default:
                var unknownMsg = new GatewayMessage
                {
                    Type = "error",
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        error = $"Unknown message type: {message.Type}"
                    }, JsonOptions)
                };
                await SendMessageAsync(sender.Socket, unknownMsg);
                break;
        }
    }

    /// <summary>
    /// Handle a "command" message: route a command from the sender to a target device.
    /// Expected payload: { targetDeviceId: "guid", commandType: "...", data: {...} }
    /// </summary>
    private async Task HandleCommandMessage(WebSocketClient sender, GatewayMessage message)
    {
        try
        {
            var payload = message.Payload;

            Guid targetDeviceId = Guid.Empty;
            if (payload.TryGetProperty("targetDeviceId", out var targetProp))
            {
                Guid.TryParse(targetProp.GetString(), out targetDeviceId);
            }

            if (targetDeviceId == Guid.Empty)
            {
                var errorMsg = new GatewayMessage
                {
                    Type = "error",
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        error = "Command requires a valid targetDeviceId in payload."
                    }, JsonOptions)
                };
                await SendMessageAsync(sender.Socket, errorMsg);
                return;
            }

            // Route the command to the target device
            var routedMessage = new GatewayMessage
            {
                Type = "command",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    fromClientId = sender.ClientId,
                    fromUsername = sender.Username,
                    originalPayload = payload
                }, JsonOptions)
            };

            var sent = await SendToClientAsync(targetDeviceId, routedMessage);

            // Send acknowledgment back to the sender
            var ackMsg = new GatewayMessage
            {
                Type = "result",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    commandId = Guid.NewGuid(),
                    status = sent ? "routed" : "target_offline",
                    targetDeviceId = targetDeviceId,
                    message = sent
                        ? "Command routed to target device."
                        : "Target device is not connected via WebSocket."
                }, JsonOptions)
            };
            await SendMessageAsync(sender.Socket, ackMsg);
        }
        catch (Exception ex)
        {
            SglLogger.Warning("WebSocket: Error handling command from {ClientId}: {Error}", sender.ClientId, ex.Message);
        }
    }

    /// <summary>
    /// Handle an "event" message: broadcast device event to all other connected clients.
    /// </summary>
    private async Task HandleEventMessage(WebSocketClient sender, GatewayMessage message)
    {
        // Re-wrap the event with the sender's identity
        var broadcastMessage = new GatewayMessage
        {
            Type = "event",
            Payload = JsonSerializer.SerializeToElement(new
            {
                sourceDeviceId = sender.ClientId,
                sourceUsername = sender.Username,
                timestamp = DateTime.UtcNow,
                originalPayload = message.Payload
            }, JsonOptions)
        };

        // Send to all clients except the sender
        var tasks = new List<Task>();
        foreach (var kvp in _connectedClients)
        {
            if (kvp.Key != sender.ClientId)
            {
                tasks.Add(SendToClientAsync(kvp.Key, broadcastMessage));
            }
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Handle a "result" message: forward a command result to the original requester.
    /// Expected payload: { targetClientId: "guid", commandId: "guid", status: "...", result: "..." }
    /// </summary>
    private async Task HandleResultMessage(WebSocketClient sender, GatewayMessage message)
    {
        try
        {
            var payload = message.Payload;

            Guid targetClientId = Guid.Empty;
            if (payload.TryGetProperty("targetClientId", out var targetProp))
            {
                Guid.TryParse(targetProp.GetString(), out targetClientId);
            }

            if (targetClientId == Guid.Empty)
            {
                // If no target specified, broadcast the result to all other clients
                var broadcastMsg = new GatewayMessage
                {
                    Type = "result",
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        fromDeviceId = sender.ClientId,
                        originalPayload = payload
                    }, JsonOptions)
                };

                foreach (var kvp in _connectedClients)
                {
                    if (kvp.Key != sender.ClientId)
                        await SendToClientAsync(kvp.Key, broadcastMsg);
                }
                return;
            }

            // Route the result to the specific requesting client
            var routedResult = new GatewayMessage
            {
                Type = "result",
                Payload = JsonSerializer.SerializeToElement(new
                {
                    fromDeviceId = sender.ClientId,
                    originalPayload = payload
                }, JsonOptions)
            };
            await SendToClientAsync(targetClientId, routedResult);
        }
        catch (Exception ex)
        {
            SglLogger.Warning("WebSocket: Error handling result from {ClientId}: {Error}", sender.ClientId, ex.Message);
        }
    }

    /// <summary>
    /// Send a GatewayMessage as JSON to a WebSocket.
    /// </summary>
    private static async Task SendMessageAsync(WebSocket socket, GatewayMessage message)
    {
        if (socket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }

    /// <summary>
    /// Safely close a WebSocket connection.
    /// </summary>
    private static async Task SafeCloseAsync(WebSocket socket, string reason)
    {
        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cts.Token);
            }
        }
        catch
        {
            // Ignore errors during close
        }
    }
}

/// <summary>
/// Represents a connected WebSocket client.
/// </summary>
public class WebSocketClient
{
    public Guid ClientId { get; set; }
    public string Username { get; set; } = string.Empty;
    public WebSocket Socket { get; set; } = null!;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastPingAt { get; set; }
    public DateTime LastPongAt { get; set; }
}

/// <summary>
/// Standard message format for WebSocket communication.
/// </summary>
public class GatewayMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
