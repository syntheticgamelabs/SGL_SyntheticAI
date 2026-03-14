using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

/// <summary>
/// Request to send a command to a target device.
/// Sent by a desktop/admin client to the server, which queues it for the target device.
/// </summary>
public class CommandRequest
{
    [JsonPropertyName("targetDeviceId")]
    public Guid TargetDeviceId { get; set; }

    [JsonPropertyName("commandType")]
    public string CommandType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("requestedBy")]
    public Guid RequestedBy { get; set; }
}
