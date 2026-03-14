using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Threat analysis endpoint that uses the server's LLM for deep file analysis.
/// Clients send PE metadata; server uses its loaded LLM model to analyze.
/// </summary>
public static class ThreatAnalysisEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ThreatAnalyze, HandleAnalyze);
    }

    private static async Task<IResult> HandleAnalyze(
        ThreatAnalysisRequest request,
        HttpContext context,
        ClientDataStore dataStore,
        ILlmService? llmService = null)
    {
        var clientId = context.Items.TryGetValue("ClientId", out var cid) && cid is Guid g ? g : Guid.Empty;

        SglLogger.Information("Threat analysis request from client {ClientId}: {FileName} (score: {Score})",
            clientId, request.FileName, request.HeuristicScore);

        if (llmService == null || !llmService.IsModelLoaded)
        {
            return Results.Ok(new ThreatAnalysisResponse
            {
                IsThreat = false,
                Confidence = 0,
                ThreatName = string.Empty,
                Severity = "Unknown",
                Analysis = "LLM model not available on server.",
                RecommendedAction = "Use local heuristic analysis."
            });
        }

        try
        {
            // Build threat info for the LLM
            var threatInfo = new ThreatInfo
            {
                Name = $"Remote Analysis: {request.FileName}",
                Description = BuildAnalysisDescription(request)
            };

            var result = await llmService.AnalyzeThreatAsync(threatInfo);

            var response = new ThreatAnalysisResponse
            {
                IsThreat = result.Confidence > 70 &&
                           result.Verdict.Contains("malicious", StringComparison.OrdinalIgnoreCase),
                Confidence = result.Confidence,
                ThreatName = result.ThreatType,
                Severity = result.Confidence > 90 ? "High" :
                           result.Confidence > 70 ? "Medium" : "Low",
                Analysis = result.Reasoning,
                RecommendedAction = result.Recommendation
            };

            // Save threat report if it's a threat
            if (response.IsThreat && clientId != Guid.Empty)
            {
                await dataStore.SaveThreatReportAsync(clientId, request.FileName,
                    request.Sha256Hash, response.ThreatName, response.Confidence);
            }

            SglLogger.Information("Threat analysis complete for {FileName}: {Verdict} (confidence: {Confidence}%)",
                request.FileName, response.IsThreat ? "THREAT" : "CLEAN", response.Confidence);

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            SglLogger.Error("Threat analysis failed for " + request.FileName, ex);
            return Results.Ok(new ThreatAnalysisResponse
            {
                IsThreat = false,
                Confidence = 0,
                Analysis = "Analysis failed: " + ex.Message,
                RecommendedAction = "Rely on local heuristic analysis."
            });
        }
    }

    private static string BuildAnalysisDescription(ThreatAnalysisRequest request)
    {
        var parts = new List<string>
        {
            $"File: {request.FileName}",
            $"Size: {request.FileSize} bytes",
            $"SHA256: {request.Sha256Hash}",
            $"Heuristic Score: {request.HeuristicScore}/100",
            $"Primary Indicator: {request.PrimaryIndicator}"
        };

        if (request.ImportedApis.Length > 0)
            parts.Add($"Suspicious API imports: {string.Join(", ", request.ImportedApis.Take(20))}");

        if (request.SectionNames.Length > 0)
            parts.Add($"PE sections: {string.Join(", ", request.SectionNames)}");

        if (request.SectionEntropies.Length > 0)
        {
            var highEntropy = request.SectionEntropies.Where(e => e > 7.0).ToArray();
            if (highEntropy.Length > 0)
                parts.Add($"High-entropy sections detected ({highEntropy.Length}), max entropy: {highEntropy.Max():F2}");
        }

        return string.Join(". ", parts);
    }
}
