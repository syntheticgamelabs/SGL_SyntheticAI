using System.Collections.Concurrent;
using LLama;
using LLama.Common;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// Manages LLaMA ChatSession instances keyed by session ID.
/// Each session wraps an InteractiveExecutor over the shared LLamaContext
/// and maintains its own chat history with an optional system prompt.
/// </summary>
public class ChatSessionManager
{
    private readonly ConcurrentDictionary<string, ManagedSession> _sessions = new();

    /// <summary>
    /// Gets or creates a chat session for the default session ID.
    /// If the session already exists, it is returned as-is (the system prompt is NOT changed).
    /// </summary>
    public ChatSession GetOrCreateSession(LLamaContext context, string? systemPrompt = null)
    {
        return GetOrCreateSession("default", context, systemPrompt);
    }

    /// <summary>
    /// Gets or creates a chat session for the specified session ID.
    /// If the session already exists, it is returned. A new session is created with
    /// an InteractiveExecutor and optionally primed with the system prompt via ChatHistory.
    /// </summary>
    public ChatSession GetOrCreateSession(string sessionId, LLamaContext context, string? systemPrompt = null)
    {
        var managed = _sessions.GetOrAdd(sessionId, _ => CreateSession(context, systemPrompt));
        return managed.Session;
    }

    /// <summary>
    /// Resets a specific session, removing its chat history. The next call to
    /// GetOrCreateSession will generate a fresh session.
    /// </summary>
    public void ResetSession(string sessionId = "default")
    {
        _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Resets all managed sessions.
    /// </summary>
    public void ResetAllSessions()
    {
        _sessions.Clear();
    }

    /// <summary>
    /// Checks whether a session with the given ID currently exists.
    /// </summary>
    public bool HasSession(string sessionId = "default")
    {
        return _sessions.ContainsKey(sessionId);
    }

    /// <summary>
    /// Returns the IDs of all active sessions.
    /// </summary>
    public IReadOnlyCollection<string> GetActiveSessionIds()
    {
        return _sessions.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the chat history for a given session, or null if the session does not exist.
    /// </summary>
    public ChatHistory? GetHistory(string sessionId = "default")
    {
        return _sessions.TryGetValue(sessionId, out var managed) ? managed.History : null;
    }

    private static ManagedSession CreateSession(LLamaContext context, string? systemPrompt)
    {
        var executor = new InteractiveExecutor(context);
        var history = new ChatHistory();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            history.AddMessage(AuthorRole.System, systemPrompt);
        }

        var session = new ChatSession(executor, history);
        return new ManagedSession(session, history);
    }

    /// <summary>
    /// Internal record to track a ChatSession alongside its ChatHistory.
    /// </summary>
    private sealed record ManagedSession(ChatSession Session, ChatHistory History);
}
