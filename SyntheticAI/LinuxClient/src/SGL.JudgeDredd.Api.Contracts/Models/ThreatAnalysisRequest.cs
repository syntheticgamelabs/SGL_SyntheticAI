using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ThreatAnalysisRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("sha256Hash")]
    public string Sha256Hash { get; set; } = string.Empty;

    [JsonPropertyName("heuristicScore")]
    public double HeuristicScore { get; set; }

    [JsonPropertyName("importedApis")]
    public string[] ImportedApis { get; set; } = [];

    [JsonPropertyName("sectionNames")]
    public string[] SectionNames { get; set; } = [];

    [JsonPropertyName("sectionEntropies")]
    public double[] SectionEntropies { get; set; } = [];

    [JsonPropertyName("primaryIndicator")]
    public string PrimaryIndicator { get; set; } = string.Empty;
}
