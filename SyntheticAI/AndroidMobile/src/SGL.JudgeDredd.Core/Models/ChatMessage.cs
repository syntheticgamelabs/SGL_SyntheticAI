using System.Text.RegularExpressions;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.Core.Models;

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public string SessionId { get; set; } = "default";
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsStreaming { get; set; }
    public string? ToolCall { get; set; }
    public string? ImagePath { get; set; }
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>Full thinking text extracted from think tags.</summary>
    public string? ThinkingContent { get; set; }

    /// <summary>Whether this message contains thinking content.</summary>
    public bool HasThinking => !string.IsNullOrEmpty(ThinkingContent);

    /// <summary>Collapsed summary of thinking (first sentence or ~80 chars).</summary>
    public string ThinkingSummary
    {
        get
        {
            if (string.IsNullOrEmpty(ThinkingContent)) return string.Empty;
            var text = ThinkingContent.Trim();
            // Try to find the first sentence
            var periodIdx = text.IndexOf(". ", StringComparison.Ordinal);
            if (periodIdx > 0 && periodIdx <= 100)
                return text[..(periodIdx + 1)];
            // Fall back to first 80 chars
            return text.Length > 80 ? text[..80] + "..." : text;
        }
    }

    /// <summary>Toggle for expanding/collapsing the thinking section in the UI.</summary>
    public bool IsThinkingExpanded { get; set; }

    /// <summary>
    /// Compiled regex to strip thinking tags (complete or partial/unclosed) for display.
    /// Handles both <think>...</think> and unclosed <think>... during streaming.
    /// </summary>
    private static readonly Regex ThinkTagStripRegex = new(
        @"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The display text for the message (answer only, without think tags).
    /// During streaming, strips thinking tags in real-time so the user never sees raw tags.
    /// After ParseThinkingTags runs, Content is already stripped so the regex is a no-op.
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (string.IsNullOrEmpty(Content)) return string.Empty;
            return ThinkTagStripRegex.Replace(Content, "").Trim();
        }
    }
}
