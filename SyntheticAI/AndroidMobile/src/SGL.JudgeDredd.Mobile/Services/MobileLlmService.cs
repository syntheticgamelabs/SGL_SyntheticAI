using System.Text.Json;
using SGL.JudgeDredd.Mobile.Models;

namespace SGL.JudgeDredd.Mobile.Services;

public class MobileLlmService
{
    private readonly ApiClient _apiClient;
    private readonly ThreatKnowledgeService _knowledgeService;
    private readonly string _modelDir;
    private string? _currentModelPath;
    private string? _currentModelName;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? DownloadProgress;

    public bool IsModelDownloaded => _currentModelPath != null && File.Exists(_currentModelPath);
    public string? CurrentModelName => _currentModelName;
    public bool IsReady { get; private set; }

    public MobileLlmService(ApiClient apiClient, ThreatKnowledgeService knowledgeService)
    {
        _apiClient = apiClient;
        _knowledgeService = knowledgeService;
        _modelDir = Path.Combine(FileSystem.AppDataDirectory, "llm_models");
        Directory.CreateDirectory(_modelDir);
        DetectExistingModel();
    }

    private void DetectExistingModel()
    {
        var modelFiles = Directory.GetFiles(_modelDir, "*.gguf");
        if (modelFiles.Length > 0)
        {
            _currentModelPath = modelFiles[0];
            _currentModelName = Path.GetFileNameWithoutExtension(_currentModelPath);
            IsReady = true;
            StatusChanged?.Invoke(this, $"Model loaded: {_currentModelName}");
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            return await _apiClient.GetAvailableModelsAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Failed to get models: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<bool> DownloadModelAsync(string modelName, CancellationToken ct = default)
    {
        try
        {
            StatusChanged?.Invoke(this, $"Downloading {modelName}...");

            var modelPath = Path.Combine(_modelDir, $"{modelName}.gguf");
            var stream = await _apiClient.DownloadModelStreamAsync(modelName);
            if (stream == null)
            {
                StatusChanged?.Invoke(this, "Model download failed - no stream from server.");
                return false;
            }

            using var fileStream = File.Create(modelPath);
            var buffer = new byte[65536];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                var mb = totalRead / (1024.0 * 1024.0);
                StatusChanged?.Invoke(this, $"Downloading... {mb:F1} MB");
                DownloadProgress?.Invoke(this, totalRead);
            }

            _currentModelPath = modelPath;
            _currentModelName = modelName;
            IsReady = true;
            StatusChanged?.Invoke(this, $"Model {modelName} downloaded ({totalRead / (1024 * 1024)} MB)");
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(this, "Download cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Download error: {ex.Message}");
            return false;
        }
    }

    public async Task<string> AnalyzeThreatAsync(string filePath, string appName, List<string> permissions)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(filePath, appName, permissions);
            var response = await _apiClient.SendChatMessageAsync(prompt, _currentModelName);

            if (!string.IsNullOrEmpty(response))
            {
                await _knowledgeService.AddThreatEntryAsync(new ThreatKnowledgeEntry
                {
                    ThreatType = "app_analysis",
                    ThreatName = appName,
                    FilePath = filePath,
                    Description = response.Length > 500 ? response[..500] : response,
                    Permissions = permissions,
                    Source = "llm_analysis"
                });
            }

            return response ?? "Analysis unavailable - server not connected.";
        }
        catch
        {
            return PerformLocalAnalysis(filePath, appName, permissions);
        }
    }

    private string BuildAnalysisPrompt(string filePath, string appName, List<string> permissions)
    {
        return $@"Analyze this Android application for security threats:
App: {appName}
Path: {filePath}
Permissions: {string.Join(", ", permissions)}

Known threat patterns from local database:
{string.Join(", ", _knowledgeService.GetDatabase().LearnedPatterns.Take(10).Select(p => $"{p.Pattern} (confidence: {p.Confidence:P0})"))}

Provide:
1. Threat assessment (Safe/Low/Medium/High/Critical)
2. Specific risks from the permissions
3. Recommended actions
4. Whether this matches any known malware patterns";
    }

    private string PerformLocalAnalysis(string filePath, string appName, List<string> permissions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Local Security Analysis: {appName} ===");
        sb.AppendLine("(Offline mode - using local heuristic engine)");
        sb.AppendLine();

        double riskScore = _knowledgeService.GetThreatScore(appName, permissions);
        int dangerousCount = permissions.Count(p =>
            p.Contains("RECORD_AUDIO") || p.Contains("CAMERA") ||
            p.Contains("LOCATION") || p.Contains("READ_SMS") ||
            p.Contains("READ_CONTACTS") || p.Contains("READ_CALL_LOG"));

        riskScore += dangerousCount * 0.15;
        riskScore = Math.Min(1.0, riskScore);

        string level = riskScore switch
        {
            >= 0.8 => "CRITICAL",
            >= 0.6 => "HIGH",
            >= 0.4 => "MEDIUM",
            >= 0.2 => "LOW",
            _ => "SAFE"
        };

        sb.AppendLine($"Threat Level: {level} (Score: {riskScore:P0})");
        sb.AppendLine();
        sb.AppendLine("Permission Analysis:");

        foreach (var p in permissions)
        {
            string risk = p switch
            {
                var x when x.Contains("RECORD_AUDIO") => "HIGH - Can record audio/conversations",
                var x when x.Contains("CAMERA") => "HIGH - Can access camera",
                var x when x.Contains("FINE_LOCATION") => "MEDIUM - Precise location tracking",
                var x when x.Contains("READ_SMS") => "HIGH - Can read text messages",
                var x when x.Contains("READ_CONTACTS") => "MEDIUM - Can read contacts",
                var x when x.Contains("READ_CALL_LOG") => "HIGH - Can read call history",
                var x when x.Contains("PHONE_STATE") => "LOW - Can detect call state",
                _ => "INFO - Standard permission"
            };
            sb.AppendLine($"  {p.Split('.').Last()}: {risk}");
        }

        sb.AppendLine();
        sb.AppendLine("Recommendations:");
        if (dangerousCount >= 3)
            sb.AppendLine("  - Consider uninstalling or restricting this app");
        if (permissions.Any(p => p.Contains("RECORD_AUDIO")))
            sb.AppendLine("  - Revoke microphone permission if not needed");
        if (permissions.Any(p => p.Contains("CAMERA")))
            sb.AppendLine("  - Revoke camera permission if not needed");
        if (permissions.Any(p => p.Contains("LOCATION")))
            sb.AppendLine("  - Switch to approximate location only");

        return sb.ToString();
    }

    public async Task<bool> CheckFirstLaunchAsync()
    {
        if (IsModelDownloaded) return true;

        StatusChanged?.Invoke(this, "No LLM model found. Checking server for available models...");
        var models = await GetAvailableModelsAsync();

        if (models.Count > 0)
        {
            StatusChanged?.Invoke(this, $"Found {models.Count} model(s). Ready to download.");
            return false;
        }

        StatusChanged?.Invoke(this, "No models available on server. Using local heuristic engine.");
        return false;
    }

    public async Task SyncKnowledgeWithServerAsync()
    {
        try
        {
            StatusChanged?.Invoke(this, "Syncing threat knowledge with server...");
            var success = await _knowledgeService.SyncWithServerAsync(_apiClient);
            StatusChanged?.Invoke(this, success ? "Knowledge synced successfully." : "Sync failed - will retry later.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Sync error: {ex.Message}");
        }
    }

    public List<string> GetDownloadedModels()
    {
        if (!Directory.Exists(_modelDir)) return new List<string>();
        return Directory.GetFiles(_modelDir, "*.gguf")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Runs local inference using the downloaded GGUF model.
    /// Falls back to server if local model not available, and to
    /// heuristic analysis if server is also unavailable.
    /// </summary>
    public async Task<string> RunLocalInferenceAsync(string userMessage, string? systemPrompt = null)
    {
        // If we have a downloaded model, try server with that model first
        // (the mobile device typically cannot run GGUF models directly due
        // to RAM constraints, but the server can run them)
        if (IsModelDownloaded && _currentModelName != null)
        {
            try
            {
                var response = await _apiClient.SendChatMessageAsync(
                    userMessage, _currentModelName, systemPrompt);
                if (!string.IsNullOrEmpty(response) &&
                    !response.StartsWith("Connection error") &&
                    !response.StartsWith("Failed to get"))
                    return response;
            }
            catch { /* Fall through to local heuristic */ }
        }

        // Local heuristic response based on knowledge base
        return GenerateLocalResponse(userMessage);
    }

    private string GenerateLocalResponse(string userMessage)
    {
        var lower = userMessage.ToLower();
        var db = _knowledgeService.GetDatabase();

        // Check for threat-related questions
        var threatKeywords = new[] { "malware", "virus", "threat", "hack", "suspicious",
            "safe", "protect", "secure", "scan", "attack" };

        if (threatKeywords.Any(k => lower.Contains(k)))
        {
            var recentThreats = _knowledgeService.GetRecentThreats(5);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Offline Mode - Local Analysis]");
            sb.AppendLine();
            sb.AppendLine($"I've logged {_knowledgeService.TotalThreatsLogged} threats " +
                         $"and learned {_knowledgeService.PatternsLearned} patterns so far.");
            sb.AppendLine();

            if (recentThreats.Count > 0)
            {
                sb.AppendLine("Recent activity:");
                foreach (var t in recentThreats.Take(3))
                    sb.AppendLine($"  - [{t.Severity}] {t.ThreatName}: {t.Description?[..Math.Min(60, t.Description?.Length ?? 0)]}");
            }

            sb.AppendLine();
            sb.AppendLine("For full AI analysis, connect to the SyntheticAI server or download a model in Settings.");
            return sb.ToString();
        }

        // General response
        return "[Offline Mode]\n\n" +
               "I'm running in offline mode without a full AI model. " +
               "I can still monitor your device for threats, but my conversational " +
               "abilities are limited.\n\n" +
               "For full AI chat, either:\n" +
               "  1. Connect to the SyntheticAI server (switch to SERVER mode)\n" +
               "  2. Download a GGUF model in Settings > AI Models";
    }

    public void SwitchModel(string modelName)
    {
        var modelPath = Path.Combine(_modelDir, $"{modelName}.gguf");
        if (File.Exists(modelPath))
        {
            _currentModelPath = modelPath;
            _currentModelName = modelName;
            IsReady = true;
            StatusChanged?.Invoke(this, $"Switched to model: {modelName}");
        }
    }
}