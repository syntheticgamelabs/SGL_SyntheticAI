using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Server.ApiServer.Services;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// API endpoints for LLM model management.
/// Allows clients to list available models, query file sizes, and download model files
/// via chunked HTTP streaming.
/// </summary>
public static class LlmModelEndpoints
{
    private const string BasePath = ApiConstants.ApiPrefix + "/llm/models";

    public static void Map(IEndpointRouteBuilder app)
    {
        // GET /api/v1/llm/models - List all available models with metadata
        app.MapGet(BasePath, HandleListModels).AllowAnonymous();

        // GET /api/v1/llm/models/{modelId}/size - Get file size before download
        app.MapGet(BasePath + "/{modelId}/size", HandleGetSize).AllowAnonymous();

        // GET /api/v1/llm/models/{modelId}/download - Download a model file (chunked streaming)
        app.MapGet(BasePath + "/{modelId}/download", HandleDownload).AllowAnonymous();
    }

    /// <summary>
    /// Returns the full model catalog with availability flags indicating which
    /// models are present on the server and ready for download.
    /// </summary>
    private static IResult HandleListModels(LlmModelManagerService service)
    {
        var models = service.GetAvailableModels();

        var response = models.Select(m => new
        {
            m.Model.Id,
            m.Model.DisplayName,
            m.Model.FileName,
            m.Model.FolderName,
            m.Model.SizeBytes,
            m.Model.SizeDisplay,
            m.Model.ParameterCount,
            m.Model.Quantization,
            m.Model.RamRequiredMB,
            m.Model.Description,
            m.Model.BestFor,
            m.Model.IsEmbeddingModel,
            m.Model.IsDefault,
            m.Model.IsRecommended,
            m.IsAvailableOnServer,
            m.ActualSizeBytes,
        });

        return Results.Ok(response);
    }

    /// <summary>
    /// Returns the exact file size of a model so the client can show download progress.
    /// </summary>
    private static IResult HandleGetSize(string modelId, LlmModelManagerService service)
    {
        var size = service.GetModelFileSize(modelId);
        if (size == null)
        {
            return Results.NotFound(new { error = $"Model '{modelId}' not found or not available on this server." });
        }

        var sizeGb = size.Value / (1024.0 * 1024.0 * 1024.0);
        var sizeMb = size.Value / (1024.0 * 1024.0);
        var sizeDisplay = sizeGb >= 1 ? $"{sizeGb:F2} GB" : $"{sizeMb:F1} MB";

        return Results.Ok(new
        {
            modelId,
            bytes = size.Value,
            sizeDisplay,
            fileName = service.GetModelFileName(modelId),
        });
    }

    /// <summary>
    /// Streams the model GGUF file to the client as an octet-stream download.
    /// Supports HTTP range requests and uses chunked streaming for large files.
    /// </summary>
    private static async Task HandleDownload(string modelId, HttpContext context, LlmModelManagerService service)
    {
        var fileName = service.GetModelFileName(modelId);
        if (fileName == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Model '{modelId}' not found." });
            return;
        }

        await using var stream = service.OpenModelStream(modelId);
        if (stream == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = $"Model '{modelId}' file not available on this server." });
            return;
        }

        var fileLength = stream.Length;

        context.Response.ContentType = "application/octet-stream";
        context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        context.Response.ContentLength = fileLength;

        SglLogger.Information("Starting LLM model download: {ModelId} ({Size:F1} MB)",
            modelId, fileLength / (1024.0 * 1024.0));

        try
        {
            // Stream in 64KB chunks to avoid loading entire file into memory
            const int bufferSize = 65536;
            var buffer = new byte[bufferSize];
            long totalSent = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                await context.Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), context.RequestAborted);
                totalSent += bytesRead;
            }

            SglLogger.Information("LLM model download complete: {ModelId} ({Sent:F1} MB sent)",
                modelId, totalSent / (1024.0 * 1024.0));
        }
        catch (OperationCanceledException)
        {
            SglLogger.Warning("LLM model download cancelled by client: {ModelId}", modelId);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM model download failed: {modelId} - {ex.Message}", ex);
        }
    }
}
