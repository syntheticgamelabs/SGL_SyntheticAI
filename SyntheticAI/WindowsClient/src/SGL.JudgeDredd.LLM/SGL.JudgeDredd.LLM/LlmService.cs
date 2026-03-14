using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;
using SGL.JudgeDredd.LLM.Prompts;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// Implementation of <see cref="ILlmService"/> backed by a local LLaMA model via LLamaSharp.
/// Provides streaming chat, threat analysis, and code generation capabilities
/// using the SyntheticAI persona and specialized system prompts.
/// </summary>
public class LlmService : ILlmService
{
    private readonly LlmModelManager _modelManager;
    private readonly ChatSessionManager _sessionManager;
    private float _temperature = 0.7f;

    /// <inheritdoc />
    public bool IsModelLoaded => _modelManager.IsLoaded;

    /// <inheritdoc />
    public string? ModelName => _modelManager.LoadedModelName;

    /// <summary>
    /// Gets or sets the sampling temperature for inference.
    /// Higher values produce more creative output; lower values are more deterministic.
    /// </summary>
    public float Temperature
    {
        get => _temperature;
        set => _temperature = Math.Clamp(value, 0.0f, 2.0f);
    }

    public LlmService(LlmModelManager modelManager, ChatSessionManager sessionManager)
    {
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    /// <inheritdoc />
    public async Task LoadModelAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await _modelManager.LoadModelAsync(progress, ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatAsync(
        string userMessage,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_modelManager.IsLoaded)
        {
            yield return "I'm not quite ready yet — the model is still loading. Please stand by!";
            yield break;
        }

        // Use the SyntheticAI persona prompt if no specific system prompt is provided
        var effectivePrompt = systemPrompt ?? SystemPrompts.JudgeDredd;

        var session = _sessionManager.GetOrCreateSession(_modelManager.Context, effectivePrompt);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 1024,
            AntiPrompts = new List<string>
            {
                "<|user|>", "<|endoftext|>", "<|end|>",
                "<|im_end|>", "<|eot_id|>", "</s>"
            },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _temperature,
                TopK = 40,
                TopP = 0.85f,
                RepeatPenalty = 1.3f,
                FrequencyPenalty = 0.4f,
                PresencePenalty = 0.3f,
            },
        };

        var message = new ChatHistory.Message(AuthorRole.User, userMessage);

        await foreach (var token in session.ChatAsync(message, inferenceParams).WithCancellation(ct))
        {
            yield return token;
        }
    }

    /// <inheritdoc />
    public async Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        await foreach (var token in ChatAsync(prompt, SystemPrompts.ThreatAnalysis, ct))
        {
            sb.Append(token);
        }
        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<ThreatAnalysisResult> AnalyzeThreatAsync(ThreatInfo threat, CancellationToken ct = default)
    {
        var prompt = $"""
            Analyze this potential threat:
            File: {threat.Name}
            Family: {threat.Family}
            Severity: {threat.Severity}
            Description: {threat.Description}
            Tags: {string.Join(", ", threat.Tags)}

            Provide your analysis in structured format:
            VERDICT: [CLEAN|SUSPICIOUS|MALICIOUS]
            CONFIDENCE: [0-100]
            THREAT_TYPE: [type or NONE]
            REASONING: [your analysis]
            RECOMMENDATION: [action to take]
            """;

        var response = await AnalyzeAsync(prompt, ct);
        return ParseThreatAnalysis(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateCodeAsync(
        string language,
        string description,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = $"Generate {language} code for the following: {description}\n\nProvide only the code with comments.";
        await foreach (var token in ChatAsync(prompt, SystemPrompts.CodeGeneration, ct))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Parses the structured threat analysis response from the LLM into a <see cref="ThreatAnalysisResult"/>.
    /// Expects lines starting with VERDICT:, CONFIDENCE:, THREAT_TYPE:, REASONING:, RECOMMENDATION:.
    /// </summary>
    private static ThreatAnalysisResult ParseThreatAnalysis(string response)
    {
        var result = new ThreatAnalysisResult();

        foreach (var line in response.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("VERDICT:", StringComparison.OrdinalIgnoreCase))
            {
                result.Verdict = line["VERDICT:".Length..].Trim();
            }
            else if (line.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line["CONFIDENCE:".Length..].Trim(), out var confidence))
                {
                    result.Confidence = confidence;
                }
            }
            else if (line.StartsWith("THREAT_TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                result.ThreatType = line["THREAT_TYPE:".Length..].Trim();
            }
            else if (line.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                result.Reasoning = line["REASONING:".Length..].Trim();
            }
            else if (line.StartsWith("RECOMMENDATION:", StringComparison.OrdinalIgnoreCase))
            {
                result.Recommendation = line["RECOMMENDATION:".Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(result.Verdict))
        {
            result.Verdict = "UNKNOWN";
        }

        return result;
    }
}
