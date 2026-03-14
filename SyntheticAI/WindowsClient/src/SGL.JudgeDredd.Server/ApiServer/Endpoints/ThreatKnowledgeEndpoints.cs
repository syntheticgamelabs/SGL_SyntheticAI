using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

public static class ThreatKnowledgeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/threats/knowledge", (Delegate)HandleSubmitKnowledge);
        app.MapGet(ApiConstants.ApiPrefix + "/threats/knowledge/aggregated", HandleGetAggregated);
    }

    private static async Task<IResult> HandleSubmitKnowledge(HttpContext context)
    {
        try
        {
            var clientId = context.Items.TryGetValue("ClientId", out var cid) ? cid?.ToString() ?? "anonymous" : "anonymous";
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body." });

            var knowledgeDir = Path.Combine(AppContext.BaseDirectory, "data", "threat_knowledge", clientId);
            Directory.CreateDirectory(knowledgeDir);

            var fileName = $"knowledge_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            await File.WriteAllTextAsync(Path.Combine(knowledgeDir, fileName), body);

            SglLogger.Information("Threat knowledge received from client {ClientId}", clientId);
            return Results.Ok(new { status = "accepted", fileName });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"ThreatKnowledge submit error: {ex.Message}");
            return Results.Problem("Failed to save threat knowledge.");
        }
    }

    private static IResult HandleGetAggregated()
    {
        try
        {
            var baseDir = Path.Combine(AppContext.BaseDirectory, "data", "threat_knowledge");
            if (!Directory.Exists(baseDir))
                return Results.Ok(new { threats = Array.Empty<object>(), totalFiles = 0 });

            var allFiles = Directory.GetFiles(baseDir, "*.json", SearchOption.AllDirectories);
            var threats = new List<JsonElement>();

            foreach (var file in allFiles.Take(1000))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var doc = JsonDocument.Parse(json);
                    threats.Add(doc.RootElement.Clone());
                }
                catch { }
            }

            return Results.Ok(new { threats, totalFiles = allFiles.Length });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"ThreatKnowledge aggregation error: {ex.Message}");
            return Results.Problem("Failed to aggregate threat knowledge.");
        }
    }
}
