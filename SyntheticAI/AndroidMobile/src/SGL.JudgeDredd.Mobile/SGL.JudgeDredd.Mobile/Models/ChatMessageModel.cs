using System.Text.RegularExpressions;

namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Represents a chat message in the AI assistant conversation.
/// </summary>
public class ChatMessageModel
{
    private static readonly Regex ThinkRegex = new(
        @"<think>(.*?)</think>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";

    /// <summary>Full thinking text extracted from think tags.</summary>
    public string? ThinkingContent { get; set; }

    /// <summary>Whether this message has thinking content.</summary>
    public bool HasThinking => !string.IsNullOrEmpty(ThinkingContent);

    /// <summary>Collapsed summary of thinking (first sentence or ~80 chars).</summary>
    public string ThinkingSummary
    {
        get
        {
            if (string.IsNullOrEmpty(ThinkingContent)) return string.Empty;
            var text = ThinkingContent.Trim();
            var periodIdx = text.IndexOf(". ", StringComparison.Ordinal);
            if (periodIdx > 0 && periodIdx <= 100)
                return text[..(periodIdx + 1)];
            return text.Length > 80 ? text[..80] + "..." : text;
        }
    }

    /// <summary>Toggle state for expanding/collapsing the thinking section.</summary>
    public bool IsThinkingExpanded { get; set; }

    /// <summary>The answer text with think tags stripped out.</summary>
    public string DisplayText => Content;

    /// <summary>
    /// Parses think tags from Content, setting ThinkingContent and stripping tags from Content.
    /// </summary>
    public void ParseThinkingTags()
    {
        if (string.IsNullOrEmpty(Content)) return;

        var match = ThinkRegex.Match(Content);
        if (!match.Success) return;

        ThinkingContent = match.Groups[1].Value.Trim();
        Content = ThinkRegex.Replace(Content, "").Trim();
    }
}
