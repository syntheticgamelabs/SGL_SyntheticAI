using LLama;
using LLama.Common;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// Manages the lifecycle of the LLaMA model: loading weights, creating contexts,
/// and disposing of native resources. Thread-safe for concurrent access checks.
/// </summary>
public class LlmModelManager : IDisposable
{
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private readonly string _modelDirectory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile bool _isLoaded;
    private volatile bool _loadFailed;
    private string? _lastError;
    private bool _disposed;
    private int _gpuLayerCount;
    private uint _contextSize;

    /// <summary>
    /// Whether the model has been successfully loaded and is ready for inference.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// The file name of the currently loaded model (without path), or null if not loaded.
    /// </summary>
    public string? LoadedModelName { get; private set; }

    /// <summary>
    /// The active LLaMA context. Throws if the model has not been loaded yet.
    /// </summary>
    public LLamaContext Context => _context ?? throw new InvalidOperationException(
        "Model not loaded. Call LoadModelAsync before accessing the context.");

    /// <summary>
    /// Raised during model loading to report progress messages.
    /// </summary>
    public event EventHandler<string>? LoadProgressChanged;

    public LlmModelManager(string modelDirectory, int gpuLayerCount = 0, uint contextSize = 2048)
    {
        _modelDirectory = modelDirectory ?? throw new ArgumentNullException(nameof(modelDirectory));
        _gpuLayerCount = gpuLayerCount;
        _contextSize = contextSize > 0 ? contextSize : 2048;
    }

    /// <summary>
    /// Loads the GGUF model file from the configured model directory.
    /// Reports progress through the optional IProgress parameter and the LoadProgressChanged event.
    /// This method is safe to call multiple times; subsequent calls are no-ops if already loaded.
    /// After a failure, retries are blocked until ResetFailure() is called.
    /// </summary>
    public async Task LoadModelAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (_isLoaded) return;

        // If a previous load failed, don't retry automatically
        if (_loadFailed)
        {
            throw new InvalidOperationException(
                $"Model load previously failed: {_lastError ?? "unknown error"}. " +
                "Please check the model file and restart the application to retry.");
        }

        // Non-blocking acquire: if another call is already loading, skip
        if (!await _loadLock.WaitAsync(0, ct)) return;

        try
        {
            // Double-check after acquiring the lock
            if (_isLoaded) return;

            var modelPath = FindModelFile();
            LoadProgressChanged?.Invoke(this, $"Model path resolved to: {modelPath}");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException(
                    $"Model file not found: {modelPath}. " +
                    $"Searched in directory: {_modelDirectory}. " +
                    $"Ensure a GGUF model file exists (Qwen3-4B-Thinking recommended).");

            progress?.Report(0.0);
            LoadProgressChanged?.Invoke(this, "Initializing model parameters...");

            var modelParams = new ModelParams(modelPath)
            {
                ContextSize = _contextSize,
                GpuLayerCount = _gpuLayerCount,
            };

            progress?.Report(0.1);
            LoadProgressChanged?.Invoke(this, "Loading model weights (this may take a while)...");

            // Load on a background thread to keep the UI responsive
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                _model = LLamaWeights.LoadFromFile(modelParams);

                progress?.Report(0.8);
                LoadProgressChanged?.Invoke(this, "Creating inference context...");

                ct.ThrowIfCancellationRequested();
                _context = _model.CreateContext(modelParams);
            }, ct);

            _isLoaded = true;
            _loadFailed = false;
            _lastError = null;
            LoadedModelName = Path.GetFileNameWithoutExtension(modelPath);
            progress?.Report(1.0);
            LoadProgressChanged?.Invoke(this, "Model loaded successfully.");
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            var fullError = ex.InnerException != null
                ? $"{ex.Message} -> {ex.InnerException.Message}"
                : ex.Message;
            _lastError = fullError;
            LoadProgressChanged?.Invoke(this, $"MODEL LOAD FAILED: {fullError}");
            LoadProgressChanged?.Invoke(this, $"MODEL LOAD STACK TRACE: {ex.StackTrace}");
            if (ex.InnerException != null)
                LoadProgressChanged?.Invoke(this, $"INNER EXCEPTION STACK: {ex.InnerException.StackTrace}");
            throw; // Re-throw so ChatViewModel gets the error
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Resets the failure state so model loading can be retried.
    /// Call this before retrying LoadModelAsync after a failure.
    /// </summary>
    public void ResetFailure()
    {
        _loadFailed = false;
        _lastError = null;
    }

    /// <summary>
    /// Sets the GPU layer count for the next model load.
    /// Does not take effect until the model is reloaded.
    /// </summary>
    public void SetGpuLayerCount(int layers)
    {
        _gpuLayerCount = Math.Max(0, layers);
    }

    /// <summary>
    /// Unloads the current model and context, freeing native resources.
    /// The model can be reloaded afterwards by calling LoadModelAsync again.
    /// </summary>
    public void Unload()
    {
        _isLoaded = false;
        _loadFailed = false;
        _lastError = null;
        _context?.Dispose();
        _context = null;
        _model?.Dispose();
        _model = null;
    }

    /// <summary>
    /// Searches the model directory for a .gguf model file.
    /// Prefers Qwen3-4B-Thinking (lightweight, strong reasoning), then falls back to any .gguf found.
    /// </summary>
    private string FindModelFile()
    {
        // Prefer Qwen3-4B-Thinking model (1.89GB - much lighter than GLM 8GB)
        var qwenDirect = Path.Combine(_modelDirectory, "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill.q3_k_s.gguf");
        if (File.Exists(qwenDirect))
            return qwenDirect;

        // Check in the Qwen3 subfolder
        var qwenSubfolder = Path.Combine(_modelDirectory, "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF",
            "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill.q3_k_s.gguf");
        if (File.Exists(qwenSubfolder))
            return qwenSubfolder;

        // Check in the LLM subfolder (common layout)
        var qwenLlmFolder = Path.Combine(_modelDirectory, "LLM",
            "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF",
            "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill.q3_k_s.gguf");
        if (File.Exists(qwenLlmFolder))
            return qwenLlmFolder;

        // Legacy: check for GLM model (backwards compatibility)
        var glmDirect = Path.Combine(_modelDirectory, "GLM-4.6V-Flash-Q4_K_M.gguf");
        if (File.Exists(glmDirect))
            return glmDirect;

        var glmSubfolder = Path.Combine(_modelDirectory, "GLM-4.6V-Flash-GGUF", "GLM-4.6V-Flash-Q4_K_M.gguf");
        if (File.Exists(glmSubfolder))
            return glmSubfolder;

        // Fallback: search recursively for any .gguf file (excluding mmproj vision projector files)
        if (Directory.Exists(_modelDirectory))
        {
            var ggufFiles = Directory.GetFiles(_modelDirectory, "*.gguf", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (ggufFiles.Length > 0)
                return ggufFiles[0];
        }

        // Return the preferred path so the caller gets a meaningful FileNotFoundException
        return qwenDirect;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
        GC.SuppressFinalize(this);
    }
}
