using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ClientRegistrationResponse
{
    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    /// <summary>
    /// JWT authentication token. Use this as the Bearer token for all subsequent API calls.
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Kept for backward compatibility. Contains the same value as Token.
    /// New clients should use the Token field instead.
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = string.Empty;
}
