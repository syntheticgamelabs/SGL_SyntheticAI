namespace SGL.JudgeDredd.Shared.Configuration;

/// <summary>
/// Describes a single LLM model available in the system catalog.
/// Used by both server (to serve downloads) and client (to display/manage models).
/// </summary>
public class LlmModelInfo
{
    /// <summary>Short unique identifier, e.g. "qwen3-4b".</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>GGUF filename on disk.</summary>
    public string FileName { get; set; } = "";

    /// <summary>Subfolder name inside the LLM/ directory.</summary>
    public string FolderName { get; set; } = "";

    /// <summary>Approximate file size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Human-readable size string, e.g. "1.8 GB".</summary>
    public string SizeDisplay { get; set; } = "";

    /// <summary>Parameter count in billions, e.g. 4 for a 4B model.</summary>
    public int ParameterCount { get; set; }

    /// <summary>Quantization level, e.g. "Q3_K_S", "Q4_K_M".</summary>
    public string Quantization { get; set; } = "";

    /// <summary>Estimated RAM required in MB to run this model.</summary>
    public int RamRequiredMB { get; set; }

    /// <summary>Short description of the model.</summary>
    public string Description { get; set; } = "";

    /// <summary>What the model is best suited for.</summary>
    public string BestFor { get; set; } = "";

    /// <summary>True if this is an embedding/RAG model (not a chat model).</summary>
    public bool IsEmbeddingModel { get; set; }

    /// <summary>True if this model is the default for new installations.</summary>
    public bool IsDefault { get; set; }

    /// <summary>True if this model is recommended for general use.</summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Returns the full relative path from the LLM root, e.g. "FolderName/FileName".
    /// </summary>
    public string RelativePath => string.IsNullOrEmpty(FolderName) ? FileName : $"{FolderName}/{FileName}";

    /// <summary>
    /// Returns the full catalog of available LLM models (excluding irrelevant ones like gaming/Unreal).
    /// </summary>
    public static List<LlmModelInfo> GetCatalog()
    {
        return
        [
            new LlmModelInfo
            {
                Id = "qwen3-4b",
                DisplayName = "Qwen3-4B (Default)",
                FileName = "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill.q3_k_s.gguf",
                FolderName = "Qwen3-4B-Thinking-2507-Claude-4.5-Opus-High-Reasoning-Distill-GGUF",
                SizeBytes = 1_932_735_488L, // ~1.8 GB
                SizeDisplay = "1.8 GB",
                ParameterCount = 4,
                Quantization = "Q3_K_S",
                RamRequiredMB = 2400,
                Description = "Qwen3 4B with reasoning distillation. Strong thinking and analysis capabilities in a compact package.",
                BestFor = "Reasoning, security analysis, thinking",
                IsDefault = true,
                IsRecommended = true,
            },
            new LlmModelInfo
            {
                Id = "ministral-3b",
                DisplayName = "Ministral-3B",
                FileName = "Ministral-3-3B-Instruct-2512-Q4_K_M.gguf",
                FolderName = "Ministral-3-3B-Instruct-2512-GGUF",
                SizeBytes = 2_147_483_648L, // ~2.0 GB
                SizeDisplay = "2.0 GB",
                ParameterCount = 3,
                Quantization = "Q4_K_M",
                RamRequiredMB = 2600,
                Description = "Ministral 3B instruction-tuned model. Fast inference with good quality output.",
                BestFor = "General chat, fast responses",
            },
            new LlmModelInfo
            {
                Id = "gemma2-2b",
                DisplayName = "Gemma-2-2B",
                FileName = "gemma-2-2b-it-abliterated-Q5_K_S.gguf",
                FolderName = "gemma-2-2b-it-abliterated-GGUF",
                SizeBytes = 1_932_735_488L, // ~1.8 GB
                SizeDisplay = "1.8 GB",
                ParameterCount = 2,
                Quantization = "Q5_K_S",
                RamRequiredMB = 2200,
                Description = "Google Gemma 2 2B abliterated (uncensored). Compact and capable general-purpose model.",
                BestFor = "General chat, uncensored",
            },
            new LlmModelInfo
            {
                Id = "lfm25-1b",
                DisplayName = "LFM2.5-1.2B",
                FileName = "LFM2.5-1.2B-Instruct-Q8_0.gguf",
                FolderName = "LFM2.5-1.2B-Instruct-GGUF",
                SizeBytes = 1_288_490_189L, // ~1.2 GB
                SizeDisplay = "1.2 GB",
                ParameterCount = 1,
                Quantization = "Q8_0",
                RamRequiredMB = 1600,
                Description = "Liquid Foundation Model 2.5 1.2B. Ultra-lightweight with fast inference.",
                BestFor = "Lightweight tasks, fast responses",
            },
            new LlmModelInfo
            {
                Id = "nanbeige4-3b",
                DisplayName = "Nanbeige4-3B",
                FileName = "Nanbeige4-3B-Thinking-2511-Claude-4.5-Opus-High-Reasoning-Distill-V2-heretic.Q3_K_M.gguf",
                FolderName = "Nanbeige4-3B-Thinking-2511-Claude-4.5-Opus-High-Reasoning-Distill-V2-heretic-GGUF",
                SizeBytes = 2_040_109_465L, // ~1.9 GB
                SizeDisplay = "1.9 GB",
                ParameterCount = 3,
                Quantization = "Q3_K_M",
                RamRequiredMB = 2500,
                Description = "Nanbeige4 3B with reasoning distillation. Strong thinking capabilities.",
                BestFor = "Reasoning, thinking, analysis",
            },
            new LlmModelInfo
            {
                Id = "llama33-8b",
                DisplayName = "Llama3.3-8B",
                FileName = "Llama3.3-8B-Instruct-Thinking-Heretic-Uncensored-Claude-4.5-Opus-High-Reasoning.Q4_K_S.gguf",
                FolderName = "Llama3.3-8B-Instruct-Thinking-Heretic-Uncensored-Claude-4.5-Opus-High-Reasoning-GGUF",
                SizeBytes = 4_831_838_208L, // ~4.5 GB
                SizeDisplay = "4.5 GB",
                ParameterCount = 8,
                Quantization = "Q4_K_S",
                RamRequiredMB = 5500,
                Description = "Llama 3.3 8B with heretic uncensoring and reasoning distillation. High quality output.",
                BestFor = "High quality reasoning, uncensored",
                IsRecommended = true,
            },
            new LlmModelInfo
            {
                Id = "neuraldaredevil-8b",
                DisplayName = "NeuralDaredevil-8B",
                FileName = "NeuralDaredevil-8B-abliterated.Q4_K_S.gguf",
                FolderName = "NeuralDaredevil-8B-abliterated-GGUF",
                SizeBytes = 4_831_838_208L, // ~4.5 GB
                SizeDisplay = "4.5 GB",
                ParameterCount = 8,
                Quantization = "Q4_K_S",
                RamRequiredMB = 5500,
                Description = "NeuralDaredevil 8B abliterated (uncensored). Strong general-purpose model.",
                BestFor = "Uncensored, general use",
            },
            new LlmModelInfo
            {
                Id = "nomic-embed",
                DisplayName = "Nomic Embed Text v1.5",
                FileName = "nomic-embed-text-v1.5.Q4_K_M.gguf",
                FolderName = "nomic-embed-text-v1.5-GGUF",
                SizeBytes = 83_886_080L, // ~80 MB
                SizeDisplay = "80 MB",
                ParameterCount = 0,
                Quantization = "Q4_K_M",
                RamRequiredMB = 200,
                Description = "Nomic embedding model for RAG and semantic search. Required for knowledge base features.",
                BestFor = "Embeddings, RAG, semantic search",
                IsEmbeddingModel = true,
            },
        ];
    }

    /// <summary>
    /// Finds a model in the catalog by its ID.
    /// </summary>
    public static LlmModelInfo? FindById(string id)
    {
        return GetCatalog().FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the default chat model from the catalog.
    /// </summary>
    public static LlmModelInfo GetDefault()
    {
        return GetCatalog().First(m => m.IsDefault);
    }
}
