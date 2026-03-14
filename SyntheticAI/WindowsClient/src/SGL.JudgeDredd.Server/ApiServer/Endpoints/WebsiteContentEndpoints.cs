using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Website content management endpoints for the admin editor.
/// GET  /api/v1/website/content  - Public: returns the current website content JSON
/// PUT  /api/v1/website/content  - Admin only: saves updated website content JSON
/// </summary>
public static class WebsiteContentEndpoints
{
    private const string ContentRoute = "/website/content";
    private static readonly string ContentFilePath = Path.Combine(AppContext.BaseDirectory, "data", "website_content.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + ContentRoute, (Delegate)HandleGetContent);
        app.MapPut(ApiConstants.ApiPrefix + ContentRoute, (Delegate)HandlePutContent);
    }

    /// <summary>
    /// GET /api/v1/website/content — Public, no auth required.
    /// Returns the current website content JSON, or a default template if none exists.
    /// </summary>
    private static IResult HandleGetContent()
    {
        try
        {
            if (File.Exists(ContentFilePath))
            {
                var json = File.ReadAllText(ContentFilePath);
                var content = JsonSerializer.Deserialize<WebsiteContent>(json, JsonOptions);
                return Results.Json(content ?? CreateDefaultContent(), JsonOptions);
            }

            return Results.Json(CreateDefaultContent(), JsonOptions);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"WebsiteContent GET error: {ex.Message}");
            return Results.Json(CreateDefaultContent(), JsonOptions);
        }
    }

    /// <summary>
    /// PUT /api/v1/website/content — Admin only.
    /// Saves the provided website content JSON to disk.
    /// </summary>
    private static async Task<IResult> HandlePutContent(HttpContext context)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var content = JsonSerializer.Deserialize<WebsiteContent>(body, JsonOptions);

            if (content == null)
                return Results.Json(new { error = "Invalid content JSON" }, statusCode: 400);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(ContentFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(content, JsonOptions);
            await File.WriteAllTextAsync(ContentFilePath, json);

            SglLogger.Information("Website content updated by admin.");
            return Results.Json(new { success = true, message = "Website content saved." });
        }
        catch (JsonException ex)
        {
            SglLogger.Error($"WebsiteContent PUT parse error: {ex.Message}");
            return Results.Json(new { error = "Invalid JSON format: " + ex.Message }, statusCode: 400);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"WebsiteContent PUT error: {ex.Message}");
            return Results.Problem("Failed to save website content.");
        }
    }

    private static WebsiteContent CreateDefaultContent() => new()
    {
        HeroTitle = "SyntheticAI Antivirus",
        HeroSubtitle = "Next-generation AI-powered protection for your devices.",
        AnnouncementBanner = "",
        AnnouncementVisible = false,
        VideoUrls = new Dictionary<string, string>(),
        SupportEmail = "support@syntheticai.com",
        SupportLinks = []
    };
}

/// <summary>
/// Data model for editable website content.
/// </summary>
public class WebsiteContent
{
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroSubtitle { get; set; } = string.Empty;
    public string AnnouncementBanner { get; set; } = string.Empty;
    public bool AnnouncementVisible { get; set; }
    public Dictionary<string, string> VideoUrls { get; set; } = new();
    public string SupportEmail { get; set; } = string.Empty;
    public List<SupportLink> SupportLinks { get; set; } = [];
}

/// <summary>
/// A labeled URL link for the support section.
/// </summary>
public class SupportLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
