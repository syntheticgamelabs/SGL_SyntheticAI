using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class SignatureUpdateResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("signatureCount")]
    public int SignatureCount { get; set; }

    [JsonPropertyName("downloadSize")]
    public long DownloadSize { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }
}
