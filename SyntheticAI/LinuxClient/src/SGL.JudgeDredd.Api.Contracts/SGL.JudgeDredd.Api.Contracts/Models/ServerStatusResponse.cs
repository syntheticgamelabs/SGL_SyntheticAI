using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ServerStatusResponse
{
    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = string.Empty;

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    [JsonPropertyName("onlineClients")]
    public int OnlineClients { get; set; }

    [JsonPropertyName("totalRegisteredClients")]
    public int TotalRegisteredClients { get; set; }

    [JsonPropertyName("signatureVersion")]
    public string SignatureVersion { get; set; } = string.Empty;

    [JsonPropertyName("signatureCount")]
    public int SignatureCount { get; set; }

    [JsonPropertyName("llmModelLoaded")]
    public bool LlmModelLoaded { get; set; }

    [JsonPropertyName("llmModelName")]
    public string LlmModelName { get; set; } = string.Empty;
}
