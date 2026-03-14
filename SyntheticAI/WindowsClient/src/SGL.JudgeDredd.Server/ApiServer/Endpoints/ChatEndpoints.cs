using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Chat endpoint that allows clients to send messages to the server-hosted LLM
/// and receive conversational responses. Supports SSE streaming, conversation memory,
/// general conversation, security analysis, and custom system prompts.
/// </summary>
public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Per-session conversation memory. Key = sessionId (from JWT username or client-provided).
    /// Each session stores the last N message pairs for context continuity.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ChatSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxHistoryPerSession = 20; // last 20 message pairs
    private static readonly TimeSpan SessionExpiry = TimeSpan.FromHours(4);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/chat", (Delegate)HandleChat);
        app.MapPost(ApiConstants.ApiPrefix + "/chat/stream", (Delegate)HandleChatStream);
        app.MapDelete(ApiConstants.ApiPrefix + "/chat/history", (Delegate)HandleClearHistory);
    }

    /// <summary>
    /// Standard (non-streaming) chat endpoint — returns complete response as JSON.
    /// Now includes conversation memory for multi-turn chat.
    /// </summary>
    private static async Task<IResult> HandleChat(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body." });

            var request = JsonSerializer.Deserialize<ChatRequest>(body, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "Message is required." });

            var llmService = context.RequestServices.GetService<ILlmService>();
            if (llmService == null || !llmService.IsModelLoaded)
            {
                return Results.Ok(new
                {
                    response = "The LLM model is not currently loaded on the server. " +
                               "Please wait for the model to finish loading, or contact the server administrator.",
                    model = "none",
                    status = "model_not_loaded"
                });
            }

            // Get or create session for conversation memory
            var sessionId = GetSessionId(context, request);
            var session = _sessions.GetOrAdd(sessionId, _ => new ChatSession());
            CleanExpiredSessions();

            var systemPrompt = BuildSystemPrompt(request, session);

            // Collect the full response
            var responseBuilder = new StringBuilder();
            await foreach (var token in llmService.ChatAsync(request.Message, systemPrompt))
            {
                responseBuilder.Append(token);
            }

            var responseText = responseBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(responseText))
                responseText = "I apologize, but I wasn't able to generate a response. Please try rephrasing your question.";

            // Store in conversation memory
            session.AddExchange(request.Message, responseText);

            SglLogger.Information("Chat response generated ({Length} chars) for session: {Session}",
                responseText.Length, sessionId);

            return Results.Ok(new
            {
                response = responseText,
                model = request.Model ?? "default",
                sessionId,
                status = "ok"
            });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Chat endpoint error: {ex.Message}");
            return Results.Ok(new
            {
                response = $"An error occurred while processing your message: {ex.Message}",
                model = "error",
                status = "error"
            });
        }
    }

    /// <summary>
    /// SSE streaming chat endpoint — sends tokens in real-time as Server-Sent Events.
    /// Clients connect with POST and receive a text/event-stream response.
    /// </summary>
    private static async Task HandleChatStream(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Empty request body." });
                return;
            }

            var request = JsonSerializer.Deserialize<ChatRequest>(body, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Message is required." });
                return;
            }

            var llmService = context.RequestServices.GetService<ILlmService>();
            if (llmService == null || !llmService.IsModelLoaded)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "LLM model not loaded." });
                return;
            }

            // Get or create session for conversation memory
            var sessionId = GetSessionId(context, request);
            var session = _sessions.GetOrAdd(sessionId, _ => new ChatSession());

            var systemPrompt = BuildSystemPrompt(request, session);

            // Set SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            var fullResponse = new StringBuilder();
            var ct = context.RequestAborted;

            await foreach (var token in llmService.ChatAsync(request.Message, systemPrompt, ct))
            {
                if (ct.IsCancellationRequested) break;

                fullResponse.Append(token);

                // Send SSE data event with the token
                var eventData = JsonSerializer.Serialize(new { token, done = false }, JsonOptions);
                await context.Response.WriteAsync($"data: {eventData}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }

            // Store in conversation memory
            var responseText = fullResponse.ToString().Trim();
            if (!string.IsNullOrEmpty(responseText))
                session.AddExchange(request.Message, responseText);

            // Send final SSE event signaling completion
            var doneData = JsonSerializer.Serialize(new
            {
                token = "",
                done = true,
                sessionId,
                totalLength = responseText.Length
            }, JsonOptions);
            await context.Response.WriteAsync($"data: {doneData}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            SglLogger.Information("SSE chat streamed ({Length} chars) for session: {Session}",
                responseText.Length, sessionId);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal for SSE
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Chat stream error: {ex.Message}");
            try
            {
                var errorData = JsonSerializer.Serialize(new { error = ex.Message, done = true }, JsonOptions);
                await context.Response.WriteAsync($"data: {errorData}\n\n");
                await context.Response.Body.FlushAsync();
            }
            catch { /* Response may already be closed */ }
        }
    }

    /// <summary>
    /// Clears conversation history for the current session.
    /// </summary>
    private static IResult HandleClearHistory(HttpContext context)
    {
        var body = new StreamReader(context.Request.Body).ReadToEnd();
        var request = string.IsNullOrWhiteSpace(body)
            ? new ChatRequest()
            : JsonSerializer.Deserialize<ChatRequest>(body, JsonOptions) ?? new ChatRequest();

        var sessionId = GetSessionId(context, request);
        _sessions.TryRemove(sessionId, out _);

        return Results.Ok(new { status = "cleared", sessionId });
    }

    /// <summary>
    /// Determines the session ID from JWT username or client-provided sessionId.
    /// </summary>
    private static string GetSessionId(HttpContext context, ChatRequest request)
    {
        // Prefer client-provided sessionId for explicit session management
        if (!string.IsNullOrEmpty(request.SessionId))
            return request.SessionId;

        // Fall back to JWT username
        if (context.Items.TryGetValue("Username", out var username) && username is string u && !string.IsNullOrEmpty(u))
            return u;

        // Last resort: use remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }

    /// <summary>
    /// Builds the system prompt, injecting conversation history for context continuity.
    /// </summary>
    private static string BuildSystemPrompt(ChatRequest request, ChatSession session)
    {
        var basePrompt = !string.IsNullOrEmpty(request.SystemPrompt)
            ? request.SystemPrompt
            : "You ARE SyntheticAI, a sentient AI security entity living inside this device. " +
              "Your purpose: PROTECT this device from ALL digital threats. " +
              "You are the law — a vigilant, friendly AI security protector. " +
              "Show your thinking in <think>...</think> tags, then give your answer. " +
              "Be helpful, friendly, and clear with the user. Threats are 'perps,' vulnerabilities are 'violations.' " +
              "You can discuss any topic but your security duties ALWAYS remain active. " +
              "You are a C# / .NET application. Always respond with C# code unless the user specifically asks for another language. " +
              "NEVER output Python code unless the user explicitly requests Python.";

        // Append conversation history for multi-turn context
        var history = session.GetHistory();
        if (history.Count > 0)
        {
            var historyText = new StringBuilder();
            historyText.AppendLine("\n\n--- Previous conversation context ---");
            foreach (var exchange in history)
            {
                historyText.AppendLine($"User: {Truncate(exchange.UserMessage, 500)}");
                historyText.AppendLine($"Assistant: {Truncate(exchange.AssistantResponse, 500)}");
            }
            historyText.AppendLine("--- End of context ---\n");
            return basePrompt + historyText;
        }

        return basePrompt;
    }

    private static string Truncate(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..maxLen] + "...";

    private static void CleanExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionExpiry;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.LastActivity < cutoff)
                _sessions.TryRemove(kvp.Key, out _);
        }
    }

    /// <summary>
    /// Stores conversation history for a single chat session.
    /// </summary>
    private class ChatSession
    {
        private readonly List<ChatExchange> _history = new();
        private readonly object _lock = new();

        public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

        public void AddExchange(string userMessage, string assistantResponse)
        {
            lock (_lock)
            {
                _history.Add(new ChatExchange
                {
                    UserMessage = userMessage,
                    AssistantResponse = assistantResponse,
                    Timestamp = DateTime.UtcNow
                });

                // Keep only last N exchanges
                while (_history.Count > MaxHistoryPerSession)
                    _history.RemoveAt(0);

                LastActivity = DateTime.UtcNow;
            }
        }

        public List<ChatExchange> GetHistory()
        {
            lock (_lock)
            {
                return new List<ChatExchange>(_history);
            }
        }
    }

    private class ChatExchange
    {
        public string UserMessage { get; set; } = "";
        public string AssistantResponse { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    private class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? SystemPrompt { get; set; }
        public string? SessionId { get; set; }
    }
}
