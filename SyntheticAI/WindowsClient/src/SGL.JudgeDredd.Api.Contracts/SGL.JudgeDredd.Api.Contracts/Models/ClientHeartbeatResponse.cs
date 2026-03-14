using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ClientHeartbeatResponse
{
    [JsonPropertyName("latestSignatureVersion")]
    public string LatestSignatureVersion { get; set; } = string.Empty;

    [JsonPropertyName("hasUpdate")]
    public bool HasUpdate { get; set; }

    [JsonPropertyName("serverMessage")]
    public string? ServerMessage { get; set; }
}
