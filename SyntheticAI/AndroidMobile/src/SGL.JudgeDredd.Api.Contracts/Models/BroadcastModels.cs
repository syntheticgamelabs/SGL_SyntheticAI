using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class BroadcastMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "info";

    [JsonPropertyName("sentBy")]
    public string SentBy { get; set; } = string.Empty;

    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class BroadcastRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "info";
}
