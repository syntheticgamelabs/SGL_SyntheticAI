namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Represents a single chat message in the broadcast page chat section.
/// </summary>
public class ChatMessageItem
{
    public string Sender { get; set; } = "Anonymous";
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Display string for the timestamp, e.g. "14:32".
    /// </summary>
    public string TimeDisplay => Timestamp.ToString("HH:mm");
}
