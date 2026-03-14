using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

/// <summary>
/// Response returned when a command is acknowledged, or when a device reports a command result.
/// </summary>
public class CommandResponse
{
    [JsonPropertyName("commandId")]
    public Guid CommandId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
}
