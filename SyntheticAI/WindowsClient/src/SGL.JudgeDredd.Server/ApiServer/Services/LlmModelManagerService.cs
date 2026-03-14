using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services;

/// <summary>
/// Server-side service that manages LLM model files.
/// Lists available models from the LLM folder, serves model downloads to clients
/// via chunked HTTP streaming, and tracks which models are present on disk.
/// </summary>
public class LlmModelManagerService
{
    private readonly string _llmRootPath;

    /// <summary>
    /// Initializes the service with the root path to the LLM models directory.
    /// </summary>
    /// <param name="llmRootPath">
    /// Absolute path to the LLM/ folder that contains model subfolders.
    /// </param>
    public LlmModelManagerService(string llmRootPath)
    {
        _llmRootPath = llmRootPath;
    }

    /// <summary>
    /// Returns the catalog of models with availability flags set based on
    /// whether the GGUF file is actually present on disk.
    /// </summary>
    public List<LlmModelAvailability> GetAvailableModels()
    {
        var catalog = LlmModelInfo.GetCatalog();
        var results = new List<LlmModelAvailability>();

        foreach (var model in catalog)
        {
            var filePath = ResolveModelFilePath(model);
            var exists = filePath != null && File.Exists(filePath);
            long actualSize = 0;

            if (exists)
            {
                try
                {
                    actualSize = new FileInfo(filePath!).Length;
                }
                catch
                {
                    // File may be locked or inaccessible
                }
            }

            results.Add(new LlmModelAvailability
            {
                Model = model,
                IsAvailableOnServer = exists,
                ActualSizeBytes = actualSize,
            });
        }

        return results;
    }

    /// <summary>
    /// Gets the file size of a specific model by its catalog ID.
    /// Returns null if the model is not found or not present on disk.
    /// </summary>
    public long? GetModelFileSize(string modelId)
    {
        var model = LlmModelInfo.FindById(modelId);
        if (model == null) return null;

        var filePath = ResolveModelFilePath(model);
        if (filePath == null || !File.Exists(filePath)) return null;

        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens a read-only file stream for the specified model, suitable for chunked streaming to clients.
    /// Returns null if the model is not found or not present on disk.
    /// </summary>
    public FileStream? OpenModelStream(string modelId)
    {
        var model = LlmModelInfo.FindById(modelId);
        if (model == null)
        {
            SglLogger.Warning("LLM download requested for unknown model ID: {ModelId}", modelId);
            return null;
        }

        var filePath = ResolveModelFilePath(model);
        if (filePath == null || !File.Exists(filePath))
        {
            SglLogger.Warning("LLM model file not found on disk: {ModelId} at {Path}",
                modelId, filePath ?? "null");
            return null;
        }

        SglLogger.Information("Opening model file for download: {ModelId} ({Path})", modelId, filePath);
        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
    }

    /// <summary>
    /// Gets the GGUF filename for a given model ID (for the Content-Disposition header).
    /// </summary>
    public string? GetModelFileName(string modelId)
    {
        var model = LlmModelInfo.FindById(modelId);
        return model?.FileName;
    }

    /// <summary>
    /// Resolves the full file path for a model by checking common folder layouts.
    /// </summary>
    private string? ResolveModelFilePath(LlmModelInfo model)
    {
        // Primary: LLM/{FolderName}/{FileName}
        if (!string.IsNullOrEmpty(model.FolderName))
        {
            var primary = Path.Combine(_llmRootPath, model.FolderName, model.FileName);
            if (File.Exists(primary)) return primary;
        }

        // Fallback: LLM/{FileName} (flat layout)
        var flat = Path.Combine(_llmRootPath, model.FileName);
        if (File.Exists(flat)) return flat;

        // Search: any match by filename in subdirectories
        try
        {
            var found = Directory.GetFiles(_llmRootPath, model.FileName, SearchOption.AllDirectories);
            if (found.Length > 0) return found[0];
        }
        catch
        {
            // Directory may not exist
        }

        return null;
    }
}

/// <summary>
/// Pairs a model catalog entry with its on-disk availability status.
/// Used by the API response to tell clients which models they can download.
/// </summary>
public class LlmModelAvailability
{
    public LlmModelInfo Model { get; set; } = new();
    public bool IsAvailableOnServer { get; set; }
    public long ActualSizeBytes { get; set; }
}
