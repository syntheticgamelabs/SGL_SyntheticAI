using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.LLM;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// AI Image Generation endpoints.
/// POST /api/v1/image/generate  - Generate an image from a text prompt (JWT auth required)
/// GET  /api/v1/image/status    - Check if the image generation server is available (public)
/// GET  /api/v1/image/models    - List available image generation models (JWT auth required)
/// </summary>
public static class ImageGenEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/image/generate", (Delegate)HandleGenerate);
        app.MapGet(ApiConstants.ApiPrefix + "/image/status", HandleStatus);
        app.MapGet(ApiConstants.ApiPrefix + "/image/models", (Delegate)HandleGetModels);
    }

    /// <summary>
    /// POST /api/v1/image/generate
    /// Accepts a JSON body with image generation parameters and returns the generated
    /// image as PNG bytes. Requires JWT authentication.
    ///
    /// Uses Stable Diffusion WebUI when available, otherwise falls back to a
    /// programmatic badge generator that creates a styled card image.
    ///
    /// Request body:
    /// {
    ///   "prompt": "a cyberpunk cityscape",
    ///   "negativePrompt": "blurry, low quality",
    ///   "width": 1024,
    ///   "height": 1024,
    ///   "steps": 30,
    ///   "cfgScale": 7.5,
    ///   "seed": -1,
    ///   "sampler": "Euler"
    /// }
    ///
    /// Returns:
    ///   200 + image/png bytes on success (from SD WebUI or fallback)
    ///   400 for bad requests
    ///   401 if not authenticated (handled by middleware)
    ///   503 if the service is not configured
    ///
    /// Response headers:
    ///   X-Image-Source: "sd_webui" or "fallback_badge"
    /// </summary>
    private static async Task<IResult> HandleGenerate(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body. Provide a JSON object with at least a 'prompt' field." });

            var clientRequest = JsonSerializer.Deserialize<ImageGenClientRequest>(body, JsonOptions);
            if (clientRequest == null || string.IsNullOrWhiteSpace(clientRequest.Prompt))
                return Results.BadRequest(new { error = "A 'prompt' field is required." });

            var imageGenService = context.RequestServices.GetService<ImageGenerationService>();
            if (imageGenService == null)
            {
                return Results.Json(new
                {
                    status = "unavailable",
                    message = "Image generation service is not configured on this server.",
                    help = "Ensure the server is started with image generation enabled."
                }, statusCode: 503);
            }

            // Map from the client-facing request model to the internal ImageGenRequest
            var request = new ImageGenRequest
            {
                Prompt = clientRequest.Prompt,
                NegativePrompt = clientRequest.NegativePrompt ?? string.Empty,
                Width = clientRequest.Width ?? 1024,
                Height = clientRequest.Height ?? 1024,
                Steps = clientRequest.Steps ?? 30,
                CfgScale = clientRequest.CfgScale ?? 7.5,
                Seed = clientRequest.Seed ?? -1,
                Sampler = clientRequest.Sampler ?? "Euler"
            };

            var username = context.Items["Username"]?.ToString() ?? "anonymous";
            SglLogger.Information("Image generation requested by {User}: \"{Prompt}\" ({W}x{H}, {Steps} steps)",
                username, request.Prompt.Length > 60 ? request.Prompt[..60] + "..." : request.Prompt,
                request.Width, request.Height, request.Steps);

            var result = await imageGenService.GenerateImageAsync(request);
            if (result == null || !result.Success)
            {
                return Results.Json(new
                {
                    status = "error",
                    message = "Image generation returned empty result. The prompt may be empty or invalid.",
                    sdWebUiAvailable = imageGenService.IsAvailable,
                    fallbackAvailable = imageGenService.IsFallbackAvailable,
                    help = !imageGenService.IsAvailable
                        ? "Stable Diffusion WebUI is not running. To enable full AI image generation, " +
                          "install and start SD WebUI (Automatic1111 or Forge) on http://localhost:7860. " +
                          "Visit https://github.com/AUTOMATIC1111/stable-diffusion-webui for installation instructions. " +
                          "The fallback badge generator is active but requires a valid prompt."
                        : "The SD WebUI server is running but returned no image. Check the server logs for errors."
                }, statusCode: 422);
            }

            // Set response header indicating the image source
            context.Response.Headers["X-Image-Source"] = result.Source;

            if (result.UsedFallback)
            {
                context.Response.Headers["X-Fallback-Notice"] =
                    "Generated using fallback badge generator. Install SD WebUI on localhost:7860 for AI image generation.";

                // Try to get LLM to generate a text description of what the image would depict
                var llmService = context.RequestServices.GetService<ILlmService>();
                if (llmService != null && llmService.IsModelLoaded)
                {
                    try
                    {
                        var descPrompt = $"Describe in 2-3 sentences what an image with this prompt would look like: \"{clientRequest.Prompt}\". Be vivid and specific.";
                        var description = await llmService.AnalyzeAsync(descPrompt);
                        if (!string.IsNullOrWhiteSpace(description))
                            context.Response.Headers["X-Image-Description"] = description.Replace("\n", " ").Trim();
                    }
                    catch { /* Non-critical - description is optional */ }
                }

                SglLogger.Information("Image generated via fallback badge generator for user {User}", username);
            }
            else
            {
                SglLogger.Information("Image generated via SD WebUI for user {User}", username);
            }

            // Return the image as PNG file download
            return Results.File(
                result.ImageData,
                "image/png",
                $"generated_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
        }
        catch (TaskCanceledException)
        {
            return Results.Json(new
            {
                status = "timeout",
                message = "Image generation timed out. Try reducing the image dimensions or step count."
            }, statusCode: 408);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Image generation endpoint error: {ex.Message}");
            return Results.Json(new
            {
                status = "error",
                message = $"Image generation failed: {ex.Message}",
                help = "If this error persists, check the server logs for more details."
            }, statusCode: 500);
        }
    }

    /// <summary>
    /// GET /api/v1/image/status
    /// Returns the current availability status of the image generation backend,
    /// including whether SD WebUI is online and whether the fallback generator is available.
    /// This endpoint does not require authentication.
    /// </summary>
    private static IResult HandleStatus(HttpContext context)
    {
        var imageGenService = context.RequestServices.GetService<ImageGenerationService>();
        if (imageGenService == null)
        {
            return Results.Ok(new
            {
                available = false,
                sdWebUiOnline = false,
                fallbackAvailable = false,
                activeGenerator = "none",
                message = "Image generation service is not configured on this server.",
                help = "Ensure the server is started with image generation enabled."
            });
        }

        var sdOnline = imageGenService.IsAvailable;
        var fallbackAvailable = imageGenService.IsFallbackAvailable;

        string activeGenerator;
        string message;

        if (sdOnline)
        {
            activeGenerator = "sd_webui";
            message = "Stable Diffusion WebUI is online and ready for AI image generation.";
        }
        else if (fallbackAvailable)
        {
            activeGenerator = "fallback_badge";
            message = "SD WebUI is offline. The fallback badge generator is active and will create " +
                      "styled card images with your prompt text. For full AI image generation, " +
                      "install and start SD WebUI (Automatic1111 or Forge) on http://localhost:7860.";
        }
        else
        {
            activeGenerator = "none";
            message = "No image generation backend is available.";
        }

        return Results.Ok(new
        {
            available = sdOnline || fallbackAvailable,
            sdWebUiOnline = sdOnline,
            fallbackAvailable,
            activeGenerator,
            message,
            sdWebUiUrl = "http://localhost:7860",
            installGuide = !sdOnline
                ? "To enable full AI image generation: " +
                  "(1) Install Python 3.10+ if not already installed. " +
                  "(2) Clone https://github.com/AUTOMATIC1111/stable-diffusion-webui. " +
                  "(3) Run webui-user.bat (Windows) or webui.sh (Linux/Mac). " +
                  "(4) Ensure it starts on port 7860 with --api flag enabled."
                : (string?)null
        });
    }

    /// <summary>
    /// GET /api/v1/image/models
    /// Returns a list of available SD model checkpoints. Requires JWT auth.
    /// </summary>
    private static async Task<IResult> HandleGetModels(HttpContext context)
    {
        var imageGenService = context.RequestServices.GetService<ImageGenerationService>();
        if (imageGenService == null)
        {
            return Results.Ok(new { models = Array.Empty<string>(), available = false });
        }

        var models = await imageGenService.GetAvailableModelsAsync();
        return Results.Ok(new
        {
            models,
            sdWebUiOnline = imageGenService.IsAvailable,
            fallbackAvailable = imageGenService.IsFallbackAvailable,
            count = models.Count
        });
    }

    /// <summary>
    /// Client-facing request DTO with camelCase JSON naming.
    /// </summary>
    private class ImageGenClientRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? NegativePrompt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Steps { get; set; }
        public double? CfgScale { get; set; }
        public long? Seed { get; set; }
        public string? Sampler { get; set; }
    }
}
