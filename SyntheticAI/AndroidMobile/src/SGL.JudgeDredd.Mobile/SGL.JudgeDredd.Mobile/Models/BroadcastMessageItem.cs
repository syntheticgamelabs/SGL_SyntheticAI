namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Represents a single message on the broadcast chat board.
/// </summary>
public class BroadcastMessageItem
{
    public string Username { get; set; } = "Anonymous";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Display string for the timestamp, e.g. "14:32".
    /// </summary>
    public string TimeDisplay => Timestamp.ToString("HH:mm");

    /// <summary>
    /// Display string combining username and time.
    /// </summary>
    public string Header => $"{Username}  {TimeDisplay}";
}
