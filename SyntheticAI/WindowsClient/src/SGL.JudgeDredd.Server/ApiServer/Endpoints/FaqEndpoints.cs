using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

public static class FaqEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string FaqDir = Path.Combine(AppContext.BaseDirectory, "data", "faq");

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/faq", HandleGetAll);
        app.MapPost(ApiConstants.ApiPrefix + "/faq/ask", HandleAsk);
        app.MapPost(ApiConstants.ApiPrefix + "/faq/{id}/answer", HandleAnswer);
    }

    private static IResult HandleGetAll()
    {
        try
        {
            Directory.CreateDirectory(FaqDir);
            var files = Directory.GetFiles(FaqDir, "*.json");
            var entries = new List<FaqEntry>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var entry = JsonSerializer.Deserialize<FaqEntry>(json);
                    if (entry != null) entries.Add(entry);
                }
                catch { }
            }

            return Results.Ok(entries.OrderByDescending(e => e.AskedAt).ToList());
        }
        catch (Exception ex)
        {
            SglLogger.Error($"FAQ GetAll error: {ex.Message}");
            return Results.Problem("Failed to retrieve FAQ entries.");
        }
    }

    private static async Task<IResult> HandleAsk(FaqAskRequest request, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question is required." });

        try
        {
            Directory.CreateDirectory(FaqDir);
            var username = context.Items.TryGetValue("Username", out var u) ? u?.ToString() ?? "anonymous" : "anonymous";

            var entry = new FaqEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Question = request.Question,
                AskedBy = username,
                AskedAt = DateTime.UtcNow,
                IsAnswered = false
            };

            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(FaqDir, $"{entry.Id}.json"), json);

            SglLogger.Information("FAQ question submitted by {Username}: {Question}", username, request.Question);
            return Results.Ok(entry);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"FAQ Ask error: {ex.Message}");
            return Results.Problem("Failed to submit question.");
        }
    }

    private static async Task<IResult> HandleAnswer(string id, FaqAnswerRequest request, HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(request.Answer))
            return Results.BadRequest(new { error = "Answer is required." });

        try
        {
            var filePath = Path.Combine(FaqDir, $"{id}.json");
            if (!File.Exists(filePath))
                return Results.NotFound(new { error = "FAQ entry not found." });

            var json = await File.ReadAllTextAsync(filePath);
            var entry = JsonSerializer.Deserialize<FaqEntry>(json);
            if (entry == null)
                return Results.NotFound(new { error = "FAQ entry corrupted." });

            entry.Answer = request.Answer;
            entry.AnsweredAt = DateTime.UtcNow;
            entry.IsAnswered = true;

            var updatedJson = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(filePath, updatedJson);

            SglLogger.Information("FAQ question {Id} answered.", id);
            return Results.Ok(entry);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"FAQ Answer error: {ex.Message}");
            return Results.Problem("Failed to save answer.");
        }
    }
}
