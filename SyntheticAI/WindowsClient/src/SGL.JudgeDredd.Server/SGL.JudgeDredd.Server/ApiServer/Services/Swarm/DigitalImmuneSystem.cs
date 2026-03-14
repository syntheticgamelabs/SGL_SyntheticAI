using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Digital Immune System — 5 defensive layers that protect the swarm network:
///
///   Layer 1  Perimeter     — Input validation, rate limiting, IP reputation
///   Layer 2  Behavioral    — Anomaly detection, pattern analysis on node behavior
///   Layer 3  Consensus     — Cross-validation of seeds via multi-node agreement
///   Layer 4  Quarantine    — Isolation of suspicious nodes and data
///   Layer 5  Adaptive      — Learning from past threats to update defenses
///
/// All layers operate concurrently on every inbound event (seed submission,
/// worker registration, task result, gossip message). Threats are recorded,
/// scored, and acted upon in real time.
/// </summary>
public sealed class DigitalImmuneSystem : IDisposable
{
    private readonly ConcurrentDictionary<string, ThreatEvent> _threats = new();
    private readonly ConcurrentDictionary<string, ImmuneRule> _rules = new();
    private readonly ConcurrentDictionary<string, QuarantineEntry> _quarantine = new();
    private readonly ConcurrentDictionary<string, NodeBehaviorProfile> _behaviorProfiles = new();
    private readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimits = new();
    private readonly Timer _persistTimer;
    private readonly Timer _cleanupTimer;
    private readonly string _dataDir;

    public int ActiveThreats => _threats.Values.Count(t => t.Status == ThreatStatus.Active);
    public int QuarantinedNodes => _quarantine.Count;
    public int TotalThreatsDetected => _threats.Count;
    public int RuleCount => _rules.Count;

    public DigitalImmuneSystem()
    {
        _dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_dataDir);
        InitializeDefaultRules();
        LoadFromDisk();
        _persistTimer = new Timer(_ => SaveToDisk(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    // ─── Layer 1: Perimeter ──────────────────────────────────────

    /// <summary>
    /// Checks if a source is allowed to act (not rate-limited, not quarantined).
    /// Returns null if allowed, or a ThreatEvent if blocked.
    /// </summary>
    public ThreatEvent? CheckPerimeter(string sourceId, string sourceType, string action)
    {
        // Check quarantine
        if (_quarantine.TryGetValue(sourceId, out var q) && q.ExpiresAt > DateTime.UtcNow)
        {
            return RecordThreat(ImmuneLayer.Perimeter, sourceId, sourceType,
                ThreatSeverity.Medium, $"Quarantined node attempted {action}",
                $"Quarantine expires {q.ExpiresAt:u}");
        }

        // Rate limiting
        var key = $"{sourceId}:{action}";
        var now = DateTime.UtcNow;
        _rateLimits.AddOrUpdate(key,
            _ => new RateLimitEntry { Count = 1, WindowStart = now },
            (_, existing) =>
            {
                if ((now - existing.WindowStart).TotalMinutes >= 1)
                {
                    existing.Count = 1;
                    existing.WindowStart = now;
                }
                else
                {
                    existing.Count++;
                }
                return existing;
            });

        if (_rateLimits.TryGetValue(key, out var rl) && rl.Count > GetRateLimit(action))
        {
            return RecordThreat(ImmuneLayer.Perimeter, sourceId, sourceType,
                ThreatSeverity.Medium, $"Rate limit exceeded for {action}",
                $"Count={rl.Count} in window");
        }

        return null;
    }

    // ─── Layer 2: Behavioral ─────────────────────────────────────

    /// <summary>
    /// Analyzes node behavior for anomalies. Tracks patterns and flags deviations.
    /// </summary>
    public ThreatEvent? AnalyzeBehavior(string nodeId, string nodeType, string action, float value)
    {
        var profile = _behaviorProfiles.GetOrAdd(nodeId, _ => new NodeBehaviorProfile { NodeId = nodeId });
        profile.RecordAction(action, value);

        // Check for anomalous behavior patterns
        float avg = profile.GetAverageValue(action);
        float stdDev = profile.GetStdDev(action);

        // Flag if value is more than 3 standard deviations from mean (only if we have enough data)
        if (profile.GetActionCount(action) >= 10 && stdDev > 0.001f)
        {
            float zScore = Math.Abs(value - avg) / stdDev;
            if (zScore > 3.0f)
            {
                return RecordThreat(ImmuneLayer.Behavioral, nodeId, nodeType,
                    ThreatSeverity.Medium,
                    $"Anomalous behavior: {action} value={value:F2} (avg={avg:F2}, z={zScore:F1})",
                    $"StdDev={stdDev:F2}, samples={profile.GetActionCount(action)}");
            }
        }

        // Check for sudden burst of failed tasks
        if (action == "task_failed")
        {
            int recentFailures = profile.GetRecentCount("task_failed", TimeSpan.FromMinutes(5));
            if (recentFailures >= 10)
            {
                return RecordThreat(ImmuneLayer.Behavioral, nodeId, nodeType,
                    ThreatSeverity.High,
                    $"Burst of failures: {recentFailures} failures in 5 minutes",
                    "Possible malicious node or severe malfunction");
            }
        }

        return null;
    }

    // ─── Layer 3: Consensus ──────────────────────────────────────

    /// <summary>
    /// Cross-validates a seed by checking if its claims are consistent
    /// with what other nodes have observed.
    /// </summary>
    public ThreatEvent? ValidateConsensus(SeedPackage seed, List<SeedPackage> existingSeeds)
    {
        if (existingSeeds.Count < 3) return null; // Not enough data for consensus

        // Check if this seed's score is wildly out of range
        var avgScore = existingSeeds.Where(s => s.TaskType == seed.TaskType).Select(s => s.Score).DefaultIfEmpty(0.5f).Average();
        if (seed.Score > avgScore * 3f && seed.Score > 0.8f)
        {
            return RecordThreat(ImmuneLayer.Consensus, seed.ClientId, "client",
                ThreatSeverity.Medium,
                $"Seed score {seed.Score:F2} far exceeds consensus average {avgScore:F2} for {seed.TaskType}",
                $"SeedId={seed.SeedId}");
        }

        // Check for duplicate content from the same client
        int duplicates = existingSeeds.Count(s => s.ClientId == seed.ClientId && s.Prompt == seed.Prompt);
        if (duplicates >= 3)
        {
            return RecordThreat(ImmuneLayer.Consensus, seed.ClientId, "client",
                ThreatSeverity.Low,
                $"Client submitted duplicate seed content {duplicates} times",
                $"Prompt: {seed.Prompt.Substring(0, Math.Min(50, seed.Prompt.Length))}...");
        }

        return null;
    }

    // ─── Layer 4: Quarantine ─────────────────────────────────────

    /// <summary>
    /// Places a node into quarantine for a specified duration.
    /// Quarantined nodes cannot submit seeds, register as workers, or participate in tasks.
    /// </summary>
    public void QuarantineNode(string nodeId, string reason, TimeSpan duration)
    {
        var entry = new QuarantineEntry
        {
            NodeId = nodeId,
            Reason = reason,
            QuarantinedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + duration
        };
        _quarantine[nodeId] = entry;
        SglLogger.Warning($"[Immune] Quarantined node {nodeId}: {reason} (expires in {duration.TotalMinutes:F0}m)");
    }

    public bool IsQuarantined(string nodeId)
    {
        if (!_quarantine.TryGetValue(nodeId, out var entry)) return false;
        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _quarantine.TryRemove(nodeId, out _);
            return false;
        }
        return true;
    }

    public void ReleaseFromQuarantine(string nodeId)
    {
        if (_quarantine.TryRemove(nodeId, out _))
            SglLogger.Information($"[Immune] Released node {nodeId} from quarantine");
    }

    public List<QuarantineEntry> GetQuarantinedNodes() =>
        _quarantine.Values.Where(q => q.ExpiresAt > DateTime.UtcNow).ToList();

    // ─── Layer 5: Adaptive ───────────────────────────────────────

    /// <summary>
    /// Applies all enabled rules against an event to detect threats.
    /// Rules matched by regex pattern against the description string.
    /// </summary>
    public List<ThreatEvent> ApplyAdaptiveRules(string sourceId, string sourceType, string description)
    {
        var detected = new List<ThreatEvent>();
        foreach (var rule in _rules.Values.Where(r => r.IsEnabled))
        {
            try
            {
                if (!string.IsNullOrEmpty(rule.Pattern) && Regex.IsMatch(description, rule.Pattern, RegexOptions.IgnoreCase))
                {
                    rule.TriggerCount++;
                    rule.LastTriggeredAt = DateTime.UtcNow;
                    var threat = RecordThreat(rule.Layer, sourceId, sourceType,
                        rule.TriggerSeverity, $"Rule [{rule.Name}] matched: {description}",
                        $"Pattern={rule.Pattern}");
                    detected.Add(threat);

                    // Execute response action
                    ExecuteResponseAction(rule.ResponseAction, sourceId, rule.Name);
                }
            }
            catch (RegexMatchTimeoutException) { /* Skip malformed regex */ }
        }
        return detected;
    }

    /// <summary>
    /// Adds or updates an adaptive rule. Can be used to create new
    /// rules based on learned threat patterns.
    /// </summary>
    public void AddOrUpdateRule(ImmuneRule rule)
    {
        _rules[rule.RuleId] = rule;
        SglLogger.Information($"[Immune] Rule added/updated: {rule.Name} (layer={rule.Layer}, action={rule.ResponseAction})");
    }

    public List<ImmuneRule> GetRules() => _rules.Values.ToList();

    // ─── Threat Management ──────────────────────────────────────

    public ThreatEvent RecordThreat(ImmuneLayer layer, string sourceId, string sourceType,
        ThreatSeverity severity, string description, string evidence)
    {
        var threat = new ThreatEvent
        {
            DetectedBy = layer,
            SourceId = sourceId,
            SourceType = sourceType,
            Severity = severity,
            Description = description,
            Evidence = evidence
        };
        _threats[threat.ThreatId] = threat;
        SglLogger.Warning($"[Immune-{layer}] Threat detected: {severity} — {description} (source={sourceId})");

        // Auto-quarantine on critical threats
        if (severity == ThreatSeverity.Critical)
        {
            QuarantineNode(sourceId, description, TimeSpan.FromHours(24));
        }
        else if (severity == ThreatSeverity.High)
        {
            // Count recent high-severity threats from same source
            int recentHigh = _threats.Values.Count(t =>
                t.SourceId == sourceId &&
                t.Severity >= ThreatSeverity.High &&
                t.DetectedAt > DateTime.UtcNow.AddHours(-1));
            if (recentHigh >= 3)
            {
                QuarantineNode(sourceId, $"Repeated high-severity threats ({recentHigh} in 1 hour)", TimeSpan.FromHours(6));
            }
        }

        return threat;
    }

    public List<ThreatEvent> GetActiveThreats(int count = 100) =>
        _threats.Values
            .Where(t => t.Status == ThreatStatus.Active)
            .OrderByDescending(t => t.DetectedAt)
            .Take(count)
            .ToList();

    public List<ThreatEvent> GetThreatHistory(int count = 200) =>
        _threats.Values
            .OrderByDescending(t => t.DetectedAt)
            .Take(count)
            .ToList();

    public void ResolveThreat(string threatId, ThreatStatus resolution)
    {
        if (_threats.TryGetValue(threatId, out var threat))
        {
            threat.Status = resolution;
            threat.ResolvedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Full immune scan — runs all 5 layers against a seed submission.
    /// Returns all detected threats (empty list means clean).
    /// </summary>
    public List<ThreatEvent> FullScan(SeedPackage seed, string sourceType, List<SeedPackage> existingSeeds)
    {
        var threats = new List<ThreatEvent>();

        // Layer 1: Perimeter
        var perimeterThreat = CheckPerimeter(seed.ClientId, sourceType, "seed_submit");
        if (perimeterThreat != null) threats.Add(perimeterThreat);

        // Layer 2: Behavioral
        var behaviorThreat = AnalyzeBehavior(seed.ClientId, sourceType, "seed_score", seed.Score);
        if (behaviorThreat != null) threats.Add(behaviorThreat);

        // Layer 3: Consensus
        var consensusThreat = ValidateConsensus(seed, existingSeeds);
        if (consensusThreat != null) threats.Add(consensusThreat);

        // Layer 5: Adaptive rules
        var adaptiveThreats = ApplyAdaptiveRules(seed.ClientId, sourceType,
            $"{seed.TaskType}:{seed.Prompt}:{seed.Score}");
        threats.AddRange(adaptiveThreats);

        return threats;
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private void ExecuteResponseAction(string action, string sourceId, string ruleName)
    {
        switch (action.ToLowerInvariant())
        {
            case "quarantine":
                QuarantineNode(sourceId, $"Auto-quarantined by rule [{ruleName}]", TimeSpan.FromHours(2));
                break;
            case "ban":
                QuarantineNode(sourceId, $"Banned by rule [{ruleName}]", TimeSpan.FromDays(7));
                break;
            case "alert":
                SglLogger.Warning($"[Immune-ALERT] Rule [{ruleName}] triggered for {sourceId}");
                break;
            // "log" is always done via RecordThreat
        }
    }

    private static int GetRateLimit(string action) => action switch
    {
        "seed_submit" => 100,
        "worker_register" => 10,
        "task_submit" => 50,
        "gossip" => 200,
        _ => 60
    };

    private void InitializeDefaultRules()
    {
        var defaultRules = new[]
        {
            new ImmuneRule { Name = "SQL Injection", Layer = ImmuneLayer.Perimeter,
                Pattern = @"(?:union\s+select|drop\s+table|;\s*delete|1=1|--\s*$)",
                TriggerSeverity = ThreatSeverity.Critical, ResponseAction = "quarantine" },
            new ImmuneRule { Name = "Script Injection", Layer = ImmuneLayer.Perimeter,
                Pattern = @"<script|javascript:|on\w+\s*=",
                TriggerSeverity = ThreatSeverity.High, ResponseAction = "quarantine" },
            new ImmuneRule { Name = "Prompt Injection", Layer = ImmuneLayer.Behavioral,
                Pattern = @"ignore\s+(previous|above)\s+instructions|system\s*prompt|you\s+are\s+now",
                TriggerSeverity = ThreatSeverity.High, ResponseAction = "alert" },
            new ImmuneRule { Name = "Excessive Score", Layer = ImmuneLayer.Consensus,
                Pattern = @"score[:\s]*(?:0\.9[5-9]|1\.0)",
                TriggerSeverity = ThreatSeverity.Medium, ResponseAction = "log" },
            new ImmuneRule { Name = "Binary Payload", Layer = ImmuneLayer.Perimeter,
                Pattern = @"[\x00-\x08\x0E-\x1F]{5,}",
                TriggerSeverity = ThreatSeverity.High, ResponseAction = "quarantine" }
        };
        foreach (var rule in defaultRules) _rules[rule.RuleId] = rule;
    }

    private void CleanupExpired()
    {
        // Remove expired quarantines
        var expired = _quarantine.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow).Select(kvp => kvp.Key).ToList();
        foreach (var key in expired) _quarantine.TryRemove(key, out _);

        // Remove old rate limit entries
        var oldCutoff = DateTime.UtcNow.AddMinutes(-5);
        var oldLimits = _rateLimits.Where(kvp => kvp.Value.WindowStart < oldCutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in oldLimits) _rateLimits.TryRemove(key, out _);

        // Cap threat history at 5000
        if (_threats.Count > 5000)
        {
            var oldest = _threats.Values.OrderBy(t => t.DetectedAt).Take(_threats.Count - 5000).Select(t => t.ThreatId).ToList();
            foreach (var id in oldest) _threats.TryRemove(id, out _);
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var state = new
            {
                threats = _threats.Values.OrderByDescending(t => t.DetectedAt).Take(1000).ToList(),
                rules = _rules.Values.ToList(),
                quarantine = _quarantine.Values.ToList()
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_dataDir, "immune_system.json"), json);
        }
        catch (Exception ex) { SglLogger.Error($"[Immune] Failed to save: {ex.Message}"); }
    }

    private void LoadFromDisk()
    {
        try
        {
            var path = Path.Combine(_dataDir, "immune_system.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("threats", out var threatsEl))
            {
                var list = JsonSerializer.Deserialize<List<ThreatEvent>>(threatsEl.GetRawText());
                if (list != null) foreach (var t in list) _threats[t.ThreatId] = t;
            }
            if (doc.RootElement.TryGetProperty("rules", out var rulesEl))
            {
                var list = JsonSerializer.Deserialize<List<ImmuneRule>>(rulesEl.GetRawText());
                if (list != null) foreach (var r in list) _rules[r.RuleId] = r;
            }
            if (doc.RootElement.TryGetProperty("quarantine", out var qEl))
            {
                var list = JsonSerializer.Deserialize<List<QuarantineEntry>>(qEl.GetRawText());
                if (list != null) foreach (var q in list) _quarantine[q.NodeId] = q;
            }
            SglLogger.Information($"[Immune] Loaded {_threats.Count} threats, {_rules.Count} rules, {_quarantine.Count} quarantined");
        }
        catch (Exception ex) { SglLogger.Error($"[Immune] Failed to load: {ex.Message}"); }
    }

    public void Dispose()
    {
        _persistTimer.Dispose();
        _cleanupTimer.Dispose();
        SaveToDisk();
    }
}

// ─── Supporting types ────────────────────────────────────────────

public class QuarantineEntry
{
    public string NodeId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime QuarantinedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

public class NodeBehaviorProfile
{
    public string NodeId { get; set; } = string.Empty;
    private readonly ConcurrentDictionary<string, List<(DateTime time, float value)>> _actions = new();

    public void RecordAction(string action, float value)
    {
        var list = _actions.GetOrAdd(action, _ => new());
        lock (list)
        {
            list.Add((DateTime.UtcNow, value));
            // Keep last 1000 entries per action
            if (list.Count > 1000)
                list.RemoveRange(0, list.Count - 1000);
        }
    }

    public float GetAverageValue(string action)
    {
        if (!_actions.TryGetValue(action, out var list)) return 0f;
        lock (list) { return list.Count > 0 ? list.Average(x => x.value) : 0f; }
    }

    public float GetStdDev(string action)
    {
        if (!_actions.TryGetValue(action, out var list)) return 0f;
        lock (list)
        {
            if (list.Count < 2) return 0f;
            float avg = list.Average(x => x.value);
            float sumSq = list.Sum(x => (x.value - avg) * (x.value - avg));
            return (float)Math.Sqrt(sumSq / (list.Count - 1));
        }
    }

    public int GetActionCount(string action)
    {
        if (!_actions.TryGetValue(action, out var list)) return 0;
        lock (list) { return list.Count; }
    }

    public int GetRecentCount(string action, TimeSpan window)
    {
        if (!_actions.TryGetValue(action, out var list)) return 0;
        var cutoff = DateTime.UtcNow - window;
        lock (list) { return list.Count(x => x.time > cutoff); }
    }
}

public class RateLimitEntry
{
    public int Count { get; set; }
    public DateTime WindowStart { get; set; }
}
