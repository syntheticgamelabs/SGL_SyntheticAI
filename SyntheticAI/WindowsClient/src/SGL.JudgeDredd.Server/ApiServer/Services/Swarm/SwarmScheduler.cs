using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Swarm Scheduler that assigns tasks to workers using a scoring algorithm.
/// Considers GPU power, trust score, task specialization, latency, and current load.
/// Wires into real services: ILlmService for LLM ops, KnowledgeGraphService for
/// vector search, and ImageGenerationService for image pipeline operations.
/// Supports remote worker dispatch via HTTP when workers provide an endpoint URL.
/// Persists job history to disk.
/// </summary>
public class SwarmScheduler
{
    private readonly ConcurrentDictionary<string, WorkerInfo> _workers = new();
    private readonly ConcurrentDictionary<string, TaskGraph> _jobs = new();
    private readonly TaskGraphBuilder _graphBuilder = new();
    private readonly string _jobsFilePath;
    private readonly Timer _persistTimer;
    private readonly HttpClient _workerHttpClient;

    /// <summary>
    /// Optional LLM service for real inference operations.
    /// When null, operations return descriptive status messages instead of fake data.
    /// </summary>
    private ILlmService? _llmService;

    /// <summary>
    /// Knowledge graph service for vector similarity search operations.
    /// </summary>
    private KnowledgeGraphService? _knowledgeGraph;

    /// <summary>
    /// Image generation service for SD WebUI integration.
    /// </summary>
    private LLM.ImageGenerationService? _imageGenService;

    public int WorkerCount => _workers.Count;
    public int ActiveJobs => _jobs.Values.Count(j => j.Status == TaskGraphStatus.Running);

    public SwarmScheduler()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _jobsFilePath = Path.Combine(dataDir, "swarm_jobs.json");
        _workerHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        LoadJobsFromDisk();
        _persistTimer = new Timer(_ => SaveJobsToDisk(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Inject the LLM service for real inference operations.
    /// Called after DI setup when an ILlmService is available.
    /// </summary>
    public void SetLlmService(ILlmService? llmService)
    {
        _llmService = llmService;
        SglLogger.Information("SwarmScheduler LLM service {Status}",
            llmService != null ? "connected" : "not available");
    }

    /// <summary>
    /// Inject the knowledge graph service for vector search operations.
    /// </summary>
    public void SetKnowledgeGraph(KnowledgeGraphService? kg)
    {
        _knowledgeGraph = kg;
        SglLogger.Information("SwarmScheduler KnowledgeGraph {Status}",
            kg != null ? "connected" : "not available");
    }

    /// <summary>
    /// Inject the image generation service for SD WebUI pipeline operations.
    /// </summary>
    public void SetImageGenService(LLM.ImageGenerationService? imageGen)
    {
        _imageGenService = imageGen;
        SglLogger.Information("SwarmScheduler ImageGen service {Status}",
            imageGen != null ? "connected" : "not available");
    }

    /// <summary>
    /// Register a worker node with the scheduler.
    /// </summary>
    public void RegisterWorker(WorkerInfo worker)
    {
        _workers[worker.WorkerId] = worker;
        worker.Status = WorkerStatus.Idle;
        SglLogger.Information("Worker {WorkerId} registered with capabilities: [{Caps}]",
            worker.WorkerId, string.Join(", ", worker.Capabilities));
    }

    /// <summary>
    /// Update a worker's heartbeat to keep it alive.
    /// </summary>
    public bool Heartbeat(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.LastHeartbeat = DateTime.UtcNow;
            if (worker.Status == WorkerStatus.Offline)
                worker.Status = WorkerStatus.Idle;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove stale workers (no heartbeat in 5 minutes).
    /// </summary>
    public void PruneStaleWorkers()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        foreach (var kv in _workers.Where(w => w.Value.LastHeartbeat < threshold))
        {
            kv.Value.Status = WorkerStatus.Offline;
        }
    }

    /// <summary>
    /// Submit a new job request. Builds a task graph and begins scheduling.
    /// </summary>
    public async Task<TaskGraph> SubmitJobAsync(string prompt, string requesterId)
    {
        var taskType = _graphBuilder.DetectTaskType(prompt);
        var graph = _graphBuilder.BuildGraph(prompt, taskType, requesterId);

        _jobs[graph.JobId] = graph;
        graph.Status = TaskGraphStatus.Running;

        // Execute the graph
        await ExecuteGraphAsync(graph);

        return graph;
    }

    /// <summary>
    /// Execute a task graph by scheduling nodes topologically.
    /// </summary>
    private async Task ExecuteGraphAsync(TaskGraph graph)
    {
        try
        {
            while (graph.Nodes.Any(n => n.Status == TaskNodeStatus.Pending))
            {
                // Find executable nodes (all dependencies completed)
                var executable = graph.Nodes
                    .Where(n => n.Status == TaskNodeStatus.Pending &&
                                n.Dependencies.All(depId =>
                                    graph.Nodes.Any(d => d.NodeId == depId && d.Status == TaskNodeStatus.Completed)))
                    .ToList();

                if (!executable.Any())
                {
                    // Check for failed dependencies
                    if (graph.Nodes.Any(n => n.Status == TaskNodeStatus.Failed))
                    {
                        graph.Status = TaskGraphStatus.Failed;
                        return;
                    }
                    break;
                }

                // Execute all ready nodes in parallel
                var tasks = executable.Select(node => ExecuteNodeAsync(node, graph));
                await Task.WhenAll(tasks);
            }

            if (graph.Nodes.All(n => n.Status == TaskNodeStatus.Completed))
            {
                graph.Status = TaskGraphStatus.Completed;
                graph.CompletedAt = DateTime.UtcNow;
                // Final result is the output of the last node
                graph.FinalResult = graph.Nodes.LastOrDefault()?.OutputData;
            }
        }
        catch (Exception ex)
        {
            graph.Status = TaskGraphStatus.Failed;
            SglLogger.Error("DAG execution failed for job {JobId}", ex, graph.JobId);
        }
    }

    /// <summary>
    /// Execute a single task node by assigning it to a worker.
    /// If the assigned worker has an endpoint URL, dispatches the task remotely via HTTP.
    /// Otherwise executes locally using available services.
    /// </summary>
    private async Task ExecuteNodeAsync(TaskNode node, TaskGraph graph)
    {
        node.Status = TaskNodeStatus.Running;
        node.StartedAt = DateTime.UtcNow;

        // Select the best worker for this operation
        var worker = SelectWorker(node.Operation);
        if (worker != null)
        {
            node.AssignedWorker = worker.WorkerId;
            worker.Status = WorkerStatus.Busy;
            worker.CurrentLoad += 0.25f;
        }

        try
        {
            // Get input from dependency outputs
            if (node.Dependencies.Any())
            {
                var depOutputs = graph.Nodes
                    .Where(n => node.Dependencies.Contains(n.NodeId) && n.OutputData != null)
                    .Select(n => n.OutputData!)
                    .ToList();
                node.InputData = string.Join("\n", depOutputs);
            }

            // Try remote dispatch to worker if it has an endpoint
            if (worker?.EndpointUrl != null)
            {
                node.OutputData = await DispatchToRemoteWorkerAsync(worker, node);
            }
            else
            {
                // Execute locally using available services
                node.OutputData = await ExecuteOperationAsync(node.Operation, node.InputData);
            }

            node.Status = TaskNodeStatus.Completed;
            node.CompletedAt = DateTime.UtcNow;

            if (worker != null)
            {
                worker.CompletedTasks++;
                worker.Status = WorkerStatus.Idle;
                worker.CurrentLoad = Math.Max(0, worker.CurrentLoad - 0.25f);
            }
        }
        catch (Exception ex)
        {
            node.Status = TaskNodeStatus.Failed;
            if (worker != null)
            {
                worker.FailedTasks++;
                worker.Status = WorkerStatus.Idle;
                worker.CurrentLoad = Math.Max(0, worker.CurrentLoad - 0.25f);
            }

            // Retry if possible
            if (node.RetryCount < node.MaxRetries)
            {
                node.RetryCount++;
                node.Status = TaskNodeStatus.Pending;
                SglLogger.Warning("Retrying node {NodeId} (attempt {Retry}/{Max})",
                    node.NodeId, node.RetryCount, node.MaxRetries);
            }
            else
            {
                SglLogger.Error("Node {NodeId} failed after {Max} retries", ex, node.NodeId, node.MaxRetries);
            }
        }
    }

    /// <summary>
    /// Dispatch a task to a remote worker via HTTP POST.
    /// Workers expose a /task/execute endpoint that accepts operation + input and returns output.
    /// </summary>
    private async Task<string> DispatchToRemoteWorkerAsync(WorkerInfo worker, TaskNode node)
    {
        var payload = new
        {
            nodeId = node.NodeId,
            jobId = node.JobId,
            operation = node.Operation,
            input = node.InputData
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        SglLogger.Information("Dispatching {Operation} to remote worker {WorkerId} at {Url}",
            node.Operation, worker.WorkerId, worker.EndpointUrl);

        var response = await _workerHttpClient.PostAsync(
            $"{worker.EndpointUrl}/task/execute", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Remote worker {worker.WorkerId} returned {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }

    /// <summary>
    /// Select the best worker for a given operation using the scoring algorithm.
    /// </summary>
    public WorkerInfo? SelectWorker(string operation)
    {
        PruneStaleWorkers();

        var candidates = _workers.Values
            .Where(w => w.Status != WorkerStatus.Offline && w.Status != WorkerStatus.Maintenance)
            .Where(w => w.Capabilities.Count == 0 || w.Capabilities.Contains(operation, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!candidates.Any())
            return null;

        // Score each worker
        return candidates
            .OrderByDescending(w =>
                (w.GpuPower * 0.35f) +
                (w.TrustScore * 0.25f) +
                (w.Capabilities.Contains(operation, StringComparer.OrdinalIgnoreCase) ? 0.20f : 0f) -
                (w.Latency * 0.10f) -
                (w.CurrentLoad * 0.10f))
            .First();
    }

    /// <summary>
    /// Execute an operation using real services when available.
    /// LLM operations use ILlmService, vector search uses KnowledgeGraphService,
    /// image operations use ImageGenerationService (SD WebUI).
    /// When a required service is unavailable, returns an honest [PENDING] status.
    /// </summary>
    private async Task<string> ExecuteOperationAsync(string operation, string input)
    {
        switch (operation)
        {
            case "EncodePrompt":
                // Prompt encoding: if LLM is available, summarize/refine the prompt
                // Otherwise pass through the raw prompt text
                if (_llmService != null && _llmService.IsModelLoaded)
                {
                    try
                    {
                        var refined = await _llmService.AnalyzeAsync(
                            $"Refine and clarify this task prompt into a clear, concise instruction:\n\n{input}");
                        return !string.IsNullOrWhiteSpace(refined) ? refined : input;
                    }
                    catch { return input; }
                }
                return input;

            case "LLMInference":
                if (_llmService != null && _llmService.IsModelLoaded)
                {
                    try
                    {
                        var result = await _llmService.AnalyzeAsync(input);
                        return !string.IsNullOrWhiteSpace(result) ? result : "[NO_OUTPUT] LLM returned empty response";
                    }
                    catch (Exception ex)
                    {
                        SglLogger.Warning("LLM inference failed in swarm task: {Error}", ex.Message);
                        return $"[LLM_ERROR] Inference failed: {ex.Message}";
                    }
                }
                return "[PENDING] No LLM model loaded - mount a model to enable real inference";

            case "AnalyzeCode":
                if (_llmService != null && _llmService.IsModelLoaded)
                {
                    try
                    {
                        var prompt = $"Analyze the following code for security issues, bugs, and improvements:\n\n{input}";
                        var result = await _llmService.AnalyzeAsync(prompt);
                        return !string.IsNullOrWhiteSpace(result) ? result : "[NO_OUTPUT] Analysis returned empty";
                    }
                    catch (Exception ex)
                    {
                        return $"[ANALYSIS_ERROR] {ex.Message}";
                    }
                }
                return "[PENDING] No LLM model loaded - mount a model to enable code analysis";

            case "SecurityScan":
                if (_llmService != null && _llmService.IsModelLoaded)
                {
                    try
                    {
                        var prompt = $"Perform a security analysis on the following:\n\n{input}";
                        var result = await _llmService.AnalyzeAsync(prompt);
                        return !string.IsNullOrWhiteSpace(result) ? result : "[NO_OUTPUT] Scan returned empty";
                    }
                    catch (Exception ex)
                    {
                        return $"[SCAN_ERROR] {ex.Message}";
                    }
                }
                return "[PENDING] No LLM model loaded - mount a model to enable security scanning";

            case "VectorSearch":
                if (_knowledgeGraph != null)
                {
                    // Generate a simple hash-based embedding from the input text for similarity lookup
                    var queryEmbedding = GenerateSimpleEmbedding(input);
                    var results = _knowledgeGraph.FindSimilarSeeds(queryEmbedding, 10);

                    if (results.Count > 0)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"[VECTOR_SEARCH] Found {results.Count} similar seeds:");
                        foreach (var (seedId, similarity) in results)
                            sb.AppendLine($"  - {seedId} (similarity: {similarity:F4})");
                        return sb.ToString();
                    }
                    return $"[VECTOR_SEARCH] No similar seeds found for query ({input.Length} chars)";
                }
                return "[PENDING] Knowledge graph service not available";

            case "GenerateLatent":
                if (_imageGenService != null)
                {
                    try
                    {
                        var imgRequest = new LLM.ImageGenRequest
                        {
                            Prompt = input,
                            Width = 512,
                            Height = 512,
                            Steps = 30
                        };
                        var result = await _imageGenService.GenerateImageAsync(imgRequest);
                        if (result != null && result.ImageData.Length > 0)
                        {
                            // Store image bytes as base64 for downstream nodes
                            var base64 = Convert.ToBase64String(result.ImageData);
                            return $"[IMAGE_DATA:{result.Source}] {base64}";
                        }
                        return "[IMAGE_ERROR] Generation returned empty result";
                    }
                    catch (Exception ex)
                    {
                        return $"[IMAGE_ERROR] {ex.Message}";
                    }
                }
                return "[PENDING] Image generation service not available - install Stable Diffusion WebUI";

            case "RefineImage":
                // If input contains base64 image data from GenerateLatent, pass it through
                // Refinement via img2img would require SD WebUI img2img endpoint
                if (input.StartsWith("[IMAGE_DATA:"))
                    return input; // Image already generated, pass through
                return "[PENDING] Image refinement requires an existing generated image";

            case "Upscale":
                // Pass through the image data - upscaling requires ESRGAN model in SD WebUI
                if (input.StartsWith("[IMAGE_DATA:"))
                    return input; // Pass through with note that upscaling was skipped
                return "[PENDING] Upscaling requires Stable Diffusion WebUI with ESRGAN model";

            case "Aggregate":
                // Combine multiple node outputs into a structured result
                if (string.IsNullOrWhiteSpace(input))
                    return "[AGGREGATE] No inputs to aggregate";

                var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length <= 1)
                    return input; // Single input, pass through

                var aggregated = new StringBuilder();
                aggregated.AppendLine($"[AGGREGATED] Combined {lines.Length} results:");
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        aggregated.AppendLine($"  {trimmed}");
                }
                return aggregated.ToString();

            default:
                SglLogger.Warning("Unknown swarm operation: {Operation}", operation);
                return $"[UNSUPPORTED] Operation '{operation}' is not recognized";
        }
    }

    /// <summary>
    /// Generate a simple deterministic embedding from text for vector similarity search.
    /// Uses character-level n-gram hashing to produce a fixed-dimension float vector.
    /// This is a lightweight fallback - production systems should use a proper embedding model.
    /// </summary>
    private static float[] GenerateSimpleEmbedding(string text, int dimensions = 128)
    {
        var embedding = new float[dimensions];
        if (string.IsNullOrWhiteSpace(text)) return embedding;

        var lower = text.ToLowerInvariant();
        // Character trigram hashing
        for (int i = 0; i < lower.Length - 2; i++)
        {
            int hash = (lower[i] * 31 + lower[i + 1]) * 31 + lower[i + 2];
            int idx = Math.Abs(hash) % dimensions;
            embedding[idx] += 1.0f;
        }

        // Word-level hashing for semantic content
        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            int hash = word.GetHashCode();
            int idx = Math.Abs(hash) % dimensions;
            embedding[idx] += 2.0f; // Words get higher weight than character n-grams
        }

        // L2 normalize
        float norm = 0;
        for (int i = 0; i < dimensions; i++)
            norm += embedding[i] * embedding[i];
        norm = (float)Math.Sqrt(norm);
        if (norm > 0)
            for (int i = 0; i < dimensions; i++)
                embedding[i] /= norm;

        return embedding;
    }

    /// <summary>
    /// Get all registered workers.
    /// </summary>
    public List<WorkerInfo> GetWorkers() => _workers.Values.ToList();

    /// <summary>
    /// Get a specific job's status and graph.
    /// </summary>
    public TaskGraph? GetJob(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <summary>
    /// Get all jobs with status.
    /// </summary>
    public List<TaskGraph> GetJobs(int count = 50) =>
        _jobs.Values.OrderByDescending(j => j.CreatedAt).Take(count).ToList();

    private void SaveJobsToDisk()
    {
        try
        {
            var data = _jobs.Values
                .Where(j => j.Status == TaskGraphStatus.Completed || j.Status == TaskGraphStatus.Failed)
                .OrderByDescending(j => j.CreatedAt)
                .Take(200)
                .ToList();

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_jobsFilePath, json);
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to save swarm jobs: {Error}", ex, ex.Message);
        }
    }

    private void LoadJobsFromDisk()
    {
        try
        {
            if (!File.Exists(_jobsFilePath)) return;
            var json = File.ReadAllText(_jobsFilePath);
            var jobs = JsonSerializer.Deserialize<List<TaskGraph>>(json);
            if (jobs == null) return;

            foreach (var job in jobs)
                _jobs.TryAdd(job.JobId, job);

            SglLogger.Information("Loaded {Count} swarm job records from disk", jobs.Count);
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to load swarm jobs: {Error}", ex, ex.Message);
        }
    }
}

/// <summary>
/// Builds task DAGs from requests.
/// </summary>
public class TaskGraphBuilder
{
    /// <summary>
    /// Detect the type of task from a prompt.
    /// </summary>
    public string DetectTaskType(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        if (lower.Contains("image") || lower.Contains("picture") || lower.Contains("draw"))
            return "IMAGE_GEN";
        if (lower.Contains("analyze") || lower.Contains("scan") || lower.Contains("threat"))
            return "LLM_ANALYSIS";
        if (lower.Contains("code") || lower.Contains("program") || lower.Contains("function"))
            return "CODE_GEN";
        if (lower.Contains("embed") || lower.Contains("vector") || lower.Contains("search"))
            return "VECTOR_SEARCH";

        return "LLM_GENERAL";
    }

    /// <summary>
    /// Build a task graph for a given request.
    /// </summary>
    public TaskGraph BuildGraph(string prompt, string taskType, string requesterId)
    {
        var graph = new TaskGraph
        {
            RequesterId = requesterId,
            Description = prompt
        };

        switch (taskType)
        {
            case "IMAGE_GEN":
                graph.Nodes = BuildImageGraph(prompt, graph.JobId);
                break;
            case "LLM_ANALYSIS":
                graph.Nodes = BuildAnalysisGraph(prompt, graph.JobId);
                break;
            case "CODE_GEN":
                graph.Nodes = BuildCodeGenGraph(prompt, graph.JobId);
                break;
            default:
                graph.Nodes = BuildGeneralGraph(prompt, graph.JobId);
                break;
        }

        return graph;
    }

    private List<TaskNode> BuildImageGraph(string prompt, string jobId)
    {
        var encode = new TaskNode { JobId = jobId, Operation = "EncodePrompt", InputData = prompt };
        var generate = new TaskNode { JobId = jobId, Operation = "GenerateLatent", Dependencies = { encode.NodeId } };
        var refine = new TaskNode { JobId = jobId, Operation = "RefineImage", Dependencies = { generate.NodeId } };
        var upscale = new TaskNode { JobId = jobId, Operation = "Upscale", Dependencies = { refine.NodeId } };

        return new List<TaskNode> { encode, generate, refine, upscale };
    }

    private List<TaskNode> BuildAnalysisGraph(string prompt, string jobId)
    {
        var encode = new TaskNode { JobId = jobId, Operation = "EncodePrompt", InputData = prompt };
        var vectorSearch = new TaskNode { JobId = jobId, Operation = "VectorSearch", Dependencies = { encode.NodeId } };
        var analyze = new TaskNode { JobId = jobId, Operation = "LLMInference", Dependencies = { encode.NodeId, vectorSearch.NodeId } };
        var aggregate = new TaskNode { JobId = jobId, Operation = "Aggregate", Dependencies = { analyze.NodeId } };

        return new List<TaskNode> { encode, vectorSearch, analyze, aggregate };
    }

    private List<TaskNode> BuildCodeGenGraph(string prompt, string jobId)
    {
        var encode = new TaskNode { JobId = jobId, Operation = "EncodePrompt", InputData = prompt };
        var inference = new TaskNode { JobId = jobId, Operation = "LLMInference", Dependencies = { encode.NodeId } };
        var codeAnalysis = new TaskNode { JobId = jobId, Operation = "AnalyzeCode", Dependencies = { inference.NodeId } };

        return new List<TaskNode> { encode, inference, codeAnalysis };
    }

    private List<TaskNode> BuildGeneralGraph(string prompt, string jobId)
    {
        var encode = new TaskNode { JobId = jobId, Operation = "EncodePrompt", InputData = prompt };
        var inference = new TaskNode { JobId = jobId, Operation = "LLMInference", Dependencies = { encode.NodeId } };

        return new List<TaskNode> { encode, inference };
    }
}
