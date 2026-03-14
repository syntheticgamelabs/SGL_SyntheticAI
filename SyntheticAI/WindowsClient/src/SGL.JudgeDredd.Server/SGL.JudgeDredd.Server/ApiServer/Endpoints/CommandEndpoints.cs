using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Command and event endpoints for device management.
/// Provides REST-based command/control alongside the WebSocket gateway.
///
/// Endpoints:
///   POST /api/v1/commands/send         - Desktop client sends command for a device
///   GET  /api/v1/commands/pending/{id} - Device polls for pending commands
///   POST /api/v1/commands/result       - Device posts command execution result
///   POST /api/v1/events                - Device posts an event
///   GET  /api/v1/events/stream/{id}    - SSE real-time event stream for a client
/// </summary>
public static class CommandEndpoints
{
    /// <summary>
    /// Directory for persisting command queue to disk.
    /// </summary>
    private static readonly string _queueDir = Path.Combine(AppContext.BaseDirectory, "data", "command_queue");

    /// <summary>
    /// In-memory queue of pending commands per device. Key = target device ID.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<PendingCommand>> PendingCommands = new();

    /// <summary>
    /// In-memory list of recent events for SSE streaming. Key = device ID.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<DeviceEvent>> RecentEvents = new();

    /// <summary>
    /// Subscribers waiting for SSE events. Key = client ID that is subscribed.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, List<SseSubscriber>> SseSubscribers = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Static constructor - loads any persisted commands from disk on startup.
    /// </summary>
    static CommandEndpoints()
    {
        if (Directory.Exists(_queueDir))
        {
            foreach (var deviceDir in Directory.GetDirectories(_queueDir))
            {
                var deviceIdStr = Path.GetFileName(deviceDir);
                if (!Guid.TryParse(deviceIdStr, out var deviceId))
                    continue;

                var queue = PendingCommands.GetOrAdd(deviceId, _ => new ConcurrentQueue<PendingCommand>());
                foreach (var file in Directory.GetFiles(deviceDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var cmd = JsonSerializer.Deserialize<PendingCommand>(json, JsonOptions);
                        if (cmd != null) queue.Enqueue(cmd);
                    }
                    catch { /* skip corrupt files */ }
                }
            }
        }
    }

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.CommandSend, HandleSendCommand);
        app.MapGet(ApiConstants.CommandPending + "/{deviceId}", HandleGetPendingCommands);
        app.MapPost(ApiConstants.CommandResult, HandleCommandResult);
        app.MapPost(ApiConstants.Events, HandlePostEvent);
        app.MapGet(ApiConstants.EventStream + "/{clientId}", HandleEventStream);
    }

    /// <summary>
    /// POST /api/v1/commands/send
    /// Desktop client sends a command for a target device.
    /// The command is queued for the target device to pick up via polling or WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSendCommand(
        CommandRequest request,
        HttpContext context,
        WebSocketGateway gateway)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (request.TargetDeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "TargetDeviceId is required." });

        if (string.IsNullOrWhiteSpace(request.CommandType))
            return Results.BadRequest(new { error = "CommandType is required." });

        // Set the requester from the authenticated token if not provided
        if (request.RequestedBy == Guid.Empty && context.Items.TryGetValue("ClientId", out var cid))
        {
            request.RequestedBy = cid is Guid g ? g : Guid.Empty;
        }

        var commandId = Guid.NewGuid();

        var pending = new PendingCommand
        {
            CommandId = commandId,
            TargetDeviceId = request.TargetDeviceId,
            CommandType = request.CommandType,
            Payload = request.Payload,
            RequestedBy = request.RequestedBy,
            CreatedAt = DateTime.UtcNow
        };

        // Queue the command for the target device
        var queue = PendingCommands.GetOrAdd(request.TargetDeviceId, _ => new ConcurrentQueue<PendingCommand>());
        queue.Enqueue(pending);

        // Persist to disk for durability across restarts
        PersistQueue(request.TargetDeviceId, pending);

        SglLogger.Information("Command queued: {CommandType} for device {TargetDevice} (CommandId: {CommandId})",
            request.CommandType, request.TargetDeviceId, commandId);

        // Also try to push via WebSocket if the target device is connected
        var wsMessage = new GatewayMessage
        {
            Type = "command",
            Payload = JsonSerializer.SerializeToElement(new
            {
                commandId = commandId,
                commandType = request.CommandType,
                payload = request.Payload,
                requestedBy = request.RequestedBy
            }, JsonOptions)
        };
        var pushed = await gateway.SendToClientAsync(request.TargetDeviceId, wsMessage);

        var response = new CommandResponse
        {
            CommandId = commandId,
            Status = pushed ? "sent" : "queued",
            Result = pushed
                ? "Command delivered to device via WebSocket."
                : "Command queued. Device will receive it on next poll."
        };

        return Results.Ok(response);
    }

    /// <summary>
    /// GET /api/v1/commands/pending/{deviceId}
    /// Device polls for pending commands. Returns all queued commands and clears the queue.
    /// </summary>
    private static IResult HandleGetPendingCommands(Guid deviceId, HttpContext context)
    {
        if (deviceId == Guid.Empty)
            return Results.BadRequest(new { error = "Invalid deviceId." });

        // Verify the requesting client is the device itself (or allow if authenticated)
        var commands = new List<PendingCommand>();

        if (PendingCommands.TryGetValue(deviceId, out var queue))
        {
            while (queue.TryDequeue(out var cmd))
            {
                commands.Add(cmd);
                // Remove persisted file since command has been delivered
                RemovePersistedCommand(deviceId, cmd.CommandId);
            }
        }

        SglLogger.Information("Device {DeviceId} polled for commands. Returning {Count} pending command(s).",
            deviceId, commands.Count);

        var responses = commands.Select(c => new CommandResponse
        {
            CommandId = c.CommandId,
            Status = "pending",
            Result = JsonSerializer.Serialize(new
            {
                commandType = c.CommandType,
                payload = c.Payload,
                requestedBy = c.RequestedBy,
                createdAt = c.CreatedAt
            }, JsonOptions)
        }).ToList();

        return Results.Ok(new { commands = responses });
    }

    /// <summary>
    /// POST /api/v1/commands/result
    /// Device posts the result of executing a command.
    /// </summary>
    private static async Task<IResult> HandleCommandResult(
        CommandResponse result,
        HttpContext context,
        WebSocketGateway gateway)
    {
        if (result.CommandId == Guid.Empty)
            return Results.BadRequest(new { error = "CommandId is required." });

        var deviceId = context.Items.TryGetValue("ClientId", out var cid) && cid is Guid g2 ? g2 : Guid.Empty;

        SglLogger.Information("Command result received: CommandId={CommandId}, Status={Status} from device {DeviceId}",
            result.CommandId, result.Status, deviceId);

        // Push the result via WebSocket to all connected clients (admin/desktop)
        var wsMessage = new GatewayMessage
        {
            Type = "result",
            Payload = JsonSerializer.SerializeToElement(new
            {
                commandId = result.CommandId,
                status = result.Status,
                result = result.Result,
                fromDeviceId = deviceId,
                timestamp = DateTime.UtcNow
            }, JsonOptions)
        };

        await gateway.BroadcastAsync(wsMessage);

        return Results.Ok(new { success = true, message = "Command result recorded." });
    }

    /// <summary>
    /// POST /api/v1/events
    /// Device posts an event (scan complete, threat found, etc.).
    /// The event is stored and pushed to SSE subscribers and WebSocket clients.
    /// </summary>
    private static async Task<IResult> HandlePostEvent(
        DeviceEvent deviceEvent,
        HttpContext context,
        WebSocketGateway gateway)
    {
        if (deviceEvent.DeviceId == Guid.Empty && context.Items.TryGetValue("ClientId", out var cid))
        {
            deviceEvent.DeviceId = cid is Guid g3 ? g3 : Guid.Empty;
        }

        if (string.IsNullOrWhiteSpace(deviceEvent.EventType))
            return Results.BadRequest(new { error = "EventType is required." });

        if (deviceEvent.Timestamp == default)
            deviceEvent.Timestamp = DateTime.UtcNow;

        SglLogger.Information("Event received: {EventType} from device {DeviceId}",
            deviceEvent.EventType, deviceEvent.DeviceId);

        // Store in recent events (keep last 100 per device)
        var eventQueue = RecentEvents.GetOrAdd(deviceEvent.DeviceId, _ => new ConcurrentQueue<DeviceEvent>());
        eventQueue.Enqueue(deviceEvent);
        while (eventQueue.Count > 100)
            eventQueue.TryDequeue(out _);

        // Push to WebSocket clients
        var wsMessage = new GatewayMessage
        {
            Type = "event",
            Payload = JsonSerializer.SerializeToElement(new
            {
                deviceId = deviceEvent.DeviceId,
                eventType = deviceEvent.EventType,
                timestamp = deviceEvent.Timestamp,
                data = deviceEvent.Data
            }, JsonOptions)
        };
        await gateway.BroadcastAsync(wsMessage);

        // Notify SSE subscribers
        await NotifySseSubscribersAsync(deviceEvent);

        return Results.Ok(new { success = true, message = "Event recorded." });
    }

    /// <summary>
    /// GET /api/v1/events/stream/{clientId}
    /// SSE (Server-Sent Events) endpoint for real-time event streaming.
    /// Desktop clients subscribe to receive events for a specific device (or all if clientId is Guid.Empty).
    /// </summary>
    private static async Task HandleEventStream(Guid clientId, HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var subscriber = new SseSubscriber
        {
            SubscriberId = Guid.NewGuid(),
            WatchingClientId = clientId,
            Response = context.Response,
            CancellationToken = context.RequestAborted
        };

        // Register subscriber
        var subscribers = SseSubscribers.GetOrAdd(clientId, _ => new List<SseSubscriber>());
        lock (subscribers)
        {
            subscribers.Add(subscriber);
        }

        SglLogger.Information("SSE subscriber connected for client {ClientId} (SubscriberId: {SubscriberId})",
            clientId, subscriber.SubscriberId);

        try
        {
            // Send initial event
            await WriteSseEventAsync(context.Response, "connected", JsonSerializer.Serialize(new
            {
                message = "SSE stream established.",
                watchingClientId = clientId,
                serverTime = DateTime.UtcNow
            }, JsonOptions));

            // Keep the connection open until the client disconnects
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            // Remove subscriber
            lock (subscribers)
            {
                subscribers.Remove(subscriber);
            }

            SglLogger.Information("SSE subscriber disconnected for client {ClientId} (SubscriberId: {SubscriberId})",
                clientId, subscriber.SubscriberId);
        }
    }

    /// <summary>
    /// Notify all SSE subscribers about a device event.
    /// </summary>
    private static async Task NotifySseSubscribersAsync(DeviceEvent deviceEvent)
    {
        var allSubscribers = new List<SseSubscriber>();

        // Subscribers watching this specific device
        if (SseSubscribers.TryGetValue(deviceEvent.DeviceId, out var deviceSubs))
        {
            lock (deviceSubs)
            {
                allSubscribers.AddRange(deviceSubs);
            }
        }

        // Subscribers watching all devices (Guid.Empty as key)
        if (SseSubscribers.TryGetValue(Guid.Empty, out var globalSubs))
        {
            lock (globalSubs)
            {
                allSubscribers.AddRange(globalSubs);
            }
        }

        var eventData = JsonSerializer.Serialize(new
        {
            deviceId = deviceEvent.DeviceId,
            eventType = deviceEvent.EventType,
            timestamp = deviceEvent.Timestamp,
            data = deviceEvent.Data
        }, JsonOptions);

        foreach (var subscriber in allSubscribers)
        {
            if (subscriber.CancellationToken.IsCancellationRequested)
                continue;

            try
            {
                await WriteSseEventAsync(subscriber.Response, deviceEvent.EventType, eventData);
            }
            catch
            {
                // Subscriber may have disconnected; will be cleaned up on next iteration
            }
        }
    }

    /// <summary>
    /// Write a single SSE event to the response stream.
    /// </summary>
    private static async Task WriteSseEventAsync(HttpResponse response, string eventType, string data)
    {
        await response.WriteAsync($"event: {eventType}\n");
        await response.WriteAsync($"data: {data}\n\n");
        await response.Body.FlushAsync();
    }

    /// <summary>
    /// Persists a command to disk for durability across restarts.
    /// </summary>
    private static void PersistQueue(Guid deviceId, PendingCommand cmd)
    {
        try
        {
            var dir = Path.Combine(_queueDir, deviceId.ToString());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{cmd.CommandId}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(cmd, JsonOptions));
        }
        catch { /* Non-critical - persistence failure doesn't block command flow */ }
    }

    /// <summary>
    /// Removes a persisted command file from disk after it has been dequeued.
    /// </summary>
    private static void RemovePersistedCommand(Guid deviceId, Guid commandId)
    {
        try
        {
            var file = Path.Combine(_queueDir, deviceId.ToString(), $"{commandId}.json");
            if (File.Exists(file)) File.Delete(file);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Represents a command waiting to be picked up by a device.
    /// </summary>
    private class PendingCommand
    {
        public Guid CommandId { get; set; }
        public Guid TargetDeviceId { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public Guid RequestedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents an SSE subscriber waiting for events.
    /// </summary>
    private class SseSubscriber
    {
        public Guid SubscriberId { get; set; }
        public Guid WatchingClientId { get; set; }
        public HttpResponse Response { get; set; } = null!;
        public CancellationToken CancellationToken { get; set; }
    }
}
