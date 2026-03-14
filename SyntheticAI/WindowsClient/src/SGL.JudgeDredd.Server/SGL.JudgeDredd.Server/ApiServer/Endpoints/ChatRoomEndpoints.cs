using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Real-time chat room endpoints for client-to-client and client-to-admin messaging.
/// Replaces the old broadcast-hacked "[Chat]" prefix approach with a proper chat system.
/// Messages are persisted to disk in data/chat_history/ and support pagination.
/// </summary>
public static class ChatRoomEndpoints
{
    private static readonly string ChatDir = Path.Combine(AppContext.BaseDirectory, "data", "chat_history");
    private static readonly ConcurrentDictionary<string, List<ChatRoomMessage>> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/chatroom/{room}", (Delegate)HandleGetMessages);
        app.MapPost(ApiConstants.ApiPrefix + "/chatroom/{room}", (Delegate)HandleSendMessage);
        app.MapGet(ApiConstants.ApiPrefix + "/chatroom", (Delegate)HandleListRooms);
        app.MapDelete(ApiConstants.ApiPrefix + "/chatroom/{room}", (Delegate)HandleClearRoom);
    }

    private static IResult HandleGetMessages(HttpContext context, string room)
    {
        EnsureLoaded();

        // Pagination
        var skip = 0;
        var take = 50;
        if (context.Request.Query.TryGetValue("skip", out var skipStr) && int.TryParse(skipStr, out var s))
            skip = s;
        if (context.Request.Query.TryGetValue("take", out var takeStr) && int.TryParse(takeStr, out var t))
            take = Math.Min(t, 200);

        if (!_rooms.TryGetValue(room, out var messages))
            return Results.Ok(new { room, messages = Array.Empty<object>(), total = 0 });

        List<ChatRoomMessage> page;
        int total;
        lock (_lock)
        {
            total = messages.Count;
            page = messages.OrderByDescending(m => m.Timestamp).Skip(skip).Take(take).ToList();
        }

        return Results.Ok(new { room, messages = page, total });
    }

    private static async Task<IResult> HandleSendMessage(HttpContext context, string room)
    {
        EnsureLoaded();

        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return Results.BadRequest(new { error = "Empty request body." });

        var request = JsonSerializer.Deserialize<ChatRoomRequest>(body, JsonOptions);
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        // Get username from JWT context or request
        var username = context.Items.TryGetValue("Username", out var u)
            ? u?.ToString() ?? request.Username ?? "Anonymous"
            : request.Username ?? "Anonymous";

        var msg = new ChatRoomMessage
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Username = username,
            Message = request.Message.Trim(),
            Timestamp = DateTime.UtcNow,
            Room = room
        };

        var roomMessages = _rooms.GetOrAdd(room, _ => new List<ChatRoomMessage>());
        lock (_lock)
        {
            roomMessages.Add(msg);
            // Keep last 500 messages per room
            while (roomMessages.Count > 500)
                roomMessages.RemoveAt(0);
        }

        // Persist to disk
        SaveRoom(room, roomMessages);

        // Push to WebSocket clients
        try
        {
            var wsGateway = context.RequestServices.GetService<WebSocketGateway>();
            if (wsGateway != null && wsGateway.ConnectedCount > 0)
            {
                var wsMessage = new GatewayMessage
                {
                    Type = "event",
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        eventType = "chatroom_message",
                        room,
                        message = msg
                    }, JsonOptions)
                };
                await wsGateway.BroadcastAsync(wsMessage);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to push chat to WebSocket clients: {Error}", ex.Message);
        }

        SglLogger.Information("Chat message in room '{Room}' from {User}: {Msg}",
            room, username, msg.Message.Length > 80 ? msg.Message[..80] + "..." : msg.Message);

        return Results.Ok(new { status = "sent", message = msg });
    }

    private static IResult HandleListRooms(HttpContext context)
    {
        EnsureLoaded();

        var rooms = _rooms.Select(r => new
        {
            name = r.Key,
            messageCount = r.Value.Count,
            lastActivity = r.Value.Count > 0 ? r.Value[^1].Timestamp : (DateTime?)null
        }).ToList();

        return Results.Ok(new { rooms });
    }

    private static IResult HandleClearRoom(HttpContext context, string room)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (_rooms.TryRemove(room, out _))
        {
            var filePath = Path.Combine(ChatDir, $"{SanitizeFileName(room)}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        return Results.Ok(new { status = "cleared", room });
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            try
            {
                Directory.CreateDirectory(ChatDir);
                foreach (var file in Directory.GetFiles(ChatDir, "*.json"))
                {
                    var roomName = Path.GetFileNameWithoutExtension(file);
                    var json = File.ReadAllText(file);
                    var messages = JsonSerializer.Deserialize<List<ChatRoomMessage>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (messages != null)
                        _rooms[roomName] = messages;
                }
                SglLogger.Information("Loaded {Count} chat rooms from disk", _rooms.Count);
            }
            catch (Exception ex)
            {
                SglLogger.Warning("Failed to load chat rooms: {Error}", ex.Message);
            }
            _loaded = true;
        }
    }

    private static void SaveRoom(string room, List<ChatRoomMessage> messages)
    {
        try
        {
            Directory.CreateDirectory(ChatDir);
            var filePath = Path.Combine(ChatDir, $"{SanitizeFileName(room)}.json");
            lock (_lock)
            {
                File.WriteAllText(filePath, JsonSerializer.Serialize(messages, JsonOptions));
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to save chat room '{Room}': {Error}", room, ex.Message);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString();
    }

    public class ChatRoomMessage
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Room { get; set; } = "";
    }

    private class ChatRoomRequest
    {
        public string Message { get; set; } = "";
        public string? Username { get; set; }
    }
}
