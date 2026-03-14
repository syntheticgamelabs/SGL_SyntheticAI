using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Mobile.Models;

public class ThreatKnowledgeDb
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public List<ThreatKnowledgeEntry> Entries { get; set; } = new();

    [JsonPropertyName("learnedPatterns")]
    public List<LearnedPattern> LearnedPatterns { get; set; } = new();
}

public class ThreatKnowledgeEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("threatType")]
    public string ThreatType { get; set; } = string.Empty;

    [JsonPropertyName("threatName")]
    public string ThreatName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("fileHash")]
    public string FileHash { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Low";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string ActionTaken { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    [JsonPropertyName("networkActivity")]
    public string NetworkActivity { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "local_scan";
}

public class LearnedPattern
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("patternType")]
    public string PatternType { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("occurrences")]
    public int Occurrences { get; set; }

    [JsonPropertyName("firstSeen")]
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSeen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
