using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// TTS (Text-to-Speech) endpoint that synthesizes speech audio for mobile clients.
/// Uses the server-side TTS engine (Qwen3-TTS or Windows SAPI fallback).
/// </summary>
public static class TtsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiConstants.ApiPrefix + "/tts/speak", (Delegate)HandleSpeak);
        app.MapGet(ApiConstants.ApiPrefix + "/tts/voices", (Delegate)HandleGetVoices);
        app.MapGet(ApiConstants.ApiPrefix + "/tts/status", HandleStatus);
    }

    private static async Task<IResult> HandleSpeak(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Empty request body." });

            var request = JsonSerializer.Deserialize<TtsRequest>(body, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return Results.BadRequest(new { error = "Text is required." });

            var ttsService = context.RequestServices.GetService<ITtsService>();
            if (ttsService == null || !ttsService.IsAvailable)
            {
                return Results.Ok(new { error = "TTS service not available on this server.", status = "unavailable" });
            }

            var voice = string.IsNullOrEmpty(request.Voice) ? "Chelsie" : request.Voice;
            var text = request.Text.Length > 2000 ? request.Text[..2000] : request.Text;

            var audioData = await ttsService.SynthesizeSpeechAsync(text, voice);
            if (audioData.Length == 0)
            {
                return Results.Ok(new { error = "TTS synthesis returned empty audio.", status = "empty" });
            }

            SglLogger.Information("TTS synthesized {Bytes} bytes for voice '{Voice}'", audioData.Length, voice);
            return Results.File(audioData, "audio/wav", "tts_response.wav");
        }
        catch (Exception ex)
        {
            SglLogger.Error($"TTS endpoint error: {ex.Message}");
            return Results.Ok(new { error = $"TTS failed: {ex.Message}", status = "error" });
        }
    }

    private static async Task<IResult> HandleGetVoices(HttpContext context)
    {
        var ttsService = context.RequestServices.GetService<ITtsService>();
        if (ttsService == null)
            return Results.Ok(new { voices = Array.Empty<string>(), available = false });

        var voices = await ttsService.GetAvailableVoicesAsync();
        return Results.Ok(new { voices, available = ttsService.IsAvailable });
    }

    private static IResult HandleStatus(HttpContext context)
    {
        var ttsService = context.RequestServices.GetService<ITtsService>();
        return Results.Ok(new
        {
            available = ttsService?.IsAvailable ?? false,
            speaking = ttsService?.IsSpeaking ?? false
        });
    }

    private class TtsRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Voice { get; set; }
    }
}
