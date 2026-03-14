using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

/// <summary>
/// Represents an event emitted by a device (e.g., scan complete, threat found, quarantine action).
/// </summary>
public class DeviceEvent
{
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}
