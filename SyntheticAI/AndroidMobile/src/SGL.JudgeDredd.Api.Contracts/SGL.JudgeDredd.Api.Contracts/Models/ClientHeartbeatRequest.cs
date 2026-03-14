using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ClientHeartbeatRequest
{
    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    [JsonPropertyName("signatureVersion")]
    public string SignatureVersion { get; set; } = string.Empty;

    [JsonPropertyName("realTimeProtectionActive")]
    public bool RealTimeProtectionActive { get; set; }

    [JsonPropertyName("threatsDetected")]
    public int ThreatsDetected { get; set; }

    [JsonPropertyName("filesScanned")]
    public int FilesScanned { get; set; }

    [JsonPropertyName("llmModelLoaded")]
    public bool LlmModelLoaded { get; set; }

    [JsonPropertyName("uptimeMinutes")]
    public double UptimeMinutes { get; set; }
}
