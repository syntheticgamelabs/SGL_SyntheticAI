namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Represents an LLM model available on the server for download.
/// Maps to the response from GET /api/v1/llm/models.
/// </summary>
public class LlmModelListItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeDisplay { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public string Quantization { get; set; } = string.Empty;
    public int RamRequiredMB { get; set; }
    public string Description { get; set; } = string.Empty;
    public string BestFor { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsAvailableOnServer { get; set; }
    public long ActualSizeBytes { get; set; }
    public bool IsEmbeddingModel { get; set; }

    /// <summary>
    /// Display label for Picker controls.
    /// </summary>
    public string PickerDisplay =>
        IsAvailableOnServer
            ? $"{DisplayName} ({SizeDisplay})"
            : $"{DisplayName} (not on server)";

    public override string ToString() => PickerDisplay;
}
