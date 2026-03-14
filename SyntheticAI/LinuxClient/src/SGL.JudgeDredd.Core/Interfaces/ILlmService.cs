namespace SGL.JudgeDredd.Core.Interfaces;

using SGL.JudgeDredd.Core.Models;

public interface ILlmService
{
    IAsyncEnumerable<string> ChatAsync(string userMessage, string? systemPrompt = null, CancellationToken ct = default);
    Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default);
    Task<ThreatAnalysisResult> AnalyzeThreatAsync(ThreatInfo threat, CancellationToken ct = default);
    IAsyncEnumerable<string> GenerateCodeAsync(string language, string description, CancellationToken ct = default);
    bool IsModelLoaded { get; }
    string? ModelName { get; }
    Task LoadModelAsync(IProgress<double>? progress = null, CancellationToken ct = default);
}
