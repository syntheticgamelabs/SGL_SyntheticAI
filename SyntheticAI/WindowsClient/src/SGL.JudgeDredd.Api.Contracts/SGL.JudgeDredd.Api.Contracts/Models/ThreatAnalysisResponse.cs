using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Api.Contracts.Models;

public class ThreatAnalysisResponse
{
    [JsonPropertyName("isThreat")]
    public bool IsThreat { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("threatName")]
    public string ThreatName { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    [JsonPropertyName("recommendedAction")]
    public string RecommendedAction { get; set; } = string.Empty;
}
