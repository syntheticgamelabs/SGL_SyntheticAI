namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

// ───────────────────────────────────────────────────────────────
// Hierarchical AI Swarm Cluster Model — 3-Layer Architecture
//   Layer 1: Global Core (this server instance)
//   Layer 2: Edge Clusters (regional aggregator nodes)
//   Layer 3: Client Agents (individual worker devices)
// ───────────────────────────────────────────────────────────────

/// <summary>
/// Layer 2 — an edge cluster node that aggregates multiple client agents
/// and acts as a regional coordinator between them and the global core.
/// </summary>
public class EdgeCluster
{
    public string ClusterId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public ClusterRole Role { get; set; } = ClusterRole.General;
    public ClusterStatus Status { get; set; } = ClusterStatus.Online;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int ConnectedAgentCount { get; set; }
    public float AggregateGpuPower { get; set; }
    public float TrustScore { get; set; } = 0.5f;
    public float AvailableCapacity { get; set; } = 1.0f;
    public List<string> Capabilities { get; set; } = new();
    public Dictionary<string, float> ResourceMetrics { get; set; } = new();
}

public enum ClusterRole
{
    General,
    Inference,
    Training,
    ImageGen,
    Security,
    Research
}

public enum ClusterStatus
{
    Online,
    Degraded,
    Offline,
    Draining,
    Maintenance
}

/// <summary>
/// Layer 3 — a client agent that connects to an edge cluster or directly to the core.
/// Represents a single device contributing compute to the swarm.
/// </summary>
public class ClientAgent
{
    public string AgentId { get; set; } = Guid.NewGuid().ToString("N");
    public string ClusterId { get; set; } = string.Empty; // empty = direct-to-core
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // win-x64, linux-x64, android
    public AgentStatus Status { get; set; } = AgentStatus.Connected;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public float ContributedGpuPower { get; set; }
    public int ContributedRamMb { get; set; }
    public float TrustScore { get; set; } = 0.5f;
    public List<string> Capabilities { get; set; } = new();
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }
    public float SuccessRate => TasksCompleted + TasksFailed > 0
        ? (float)TasksCompleted / (TasksCompleted + TasksFailed) : 0f;
}

public enum AgentStatus
{
    Connected,
    Working,
    Idle,
    Disconnected,
    Banned
}

// ───────────────────────────────────────────────────────────────
// Digital Immune System — 5 Defensive Layers
// ───────────────────────────────────────────────────────────────

/// <summary>
/// Represents a threat detected by any layer of the immune system.
/// </summary>
public class ThreatEvent
{
    public string ThreatId { get; set; } = Guid.NewGuid().ToString("N");
    public ThreatSeverity Severity { get; set; } = ThreatSeverity.Low;
    public ImmuneLayer DetectedBy { get; set; }
    public string SourceId { get; set; } = string.Empty;    // worker/agent/cluster ID
    public string SourceType { get; set; } = string.Empty;  // "worker", "agent", "cluster", "external"
    public string Description { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public ThreatStatus Status { get; set; } = ThreatStatus.Active;
    public string? ResponseAction { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum ThreatSeverity { Low, Medium, High, Critical }

public enum ThreatStatus { Active, Mitigated, Resolved, FalsePositive }

public enum ImmuneLayer
{
    Perimeter,      // Layer 1: Input validation, rate limiting, IP reputation
    Behavioral,     // Layer 2: Anomaly detection, pattern analysis
    Consensus,      // Layer 3: Cross-validation of seeds, multi-node agreement
    Quarantine,     // Layer 4: Isolation of suspicious nodes/data
    Adaptive        // Layer 5: Learning from past threats, updating defenses
}

/// <summary>
/// An immune rule that defines detection criteria and response actions.
/// </summary>
public class ImmuneRule
{
    public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
    public ImmuneLayer Layer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;       // regex or expression to match
    public float ThresholdScore { get; set; } = 0.7f;
    public ThreatSeverity TriggerSeverity { get; set; } = ThreatSeverity.Medium;
    public string ResponseAction { get; set; } = "log";       // log, quarantine, ban, alert
    public bool IsEnabled { get; set; } = true;
    public int TriggerCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
}

// ───────────────────────────────────────────────────────────────
// Self-Healing Network — Health checks and auto-recovery
// ───────────────────────────────────────────────────────────────

/// <summary>
/// A health check result from a node (worker, cluster, or service).
/// </summary>
public class HealthCheckResult
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty; // "worker", "cluster", "service"
    public bool IsHealthy { get; set; }
    public float HealthScore { get; set; } = 1.0f; // 0.0 = dead, 1.0 = perfect
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public List<string> Issues { get; set; } = new();
    public Dictionary<string, float> Metrics { get; set; } = new();
}

/// <summary>
/// A recovery action taken by the self-healing system.
/// </summary>
public class RecoveryAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString("N");
    public string TargetNodeId { get; set; } = string.Empty;
    public RecoveryType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}

public enum RecoveryType
{
    Restart,
    Reassign,
    Isolate,
    Failover,
    ScaleUp,
    Rebalance
}

// ───────────────────────────────────────────────────────────────
// Gossip Protocol — Peer-to-peer state propagation
// ───────────────────────────────────────────────────────────────

/// <summary>
/// A gossip message exchanged between nodes for peer learning
/// and state synchronization.
/// </summary>
public class GossipMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string SenderId { get; set; } = string.Empty;
    public GossipType Type { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int Ttl { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> SeenBy { get; set; } = new(); // prevents re-broadcast
}

public enum GossipType
{
    SeedShare,          // Share a high-quality seed with peers
    ThreatAlert,        // Warn peers about detected threats
    CapabilityUpdate,   // Announce new or changed capabilities
    HealthReport,       // Share node health status
    ModelUpdate,        // Notify peers about new model availability
    ReputationUpdate    // Share updated trust scores
}

// ───────────────────────────────────────────────────────────────
// Compression metadata for seed distribution
// ───────────────────────────────────────────────────────────────

public enum CompressionAlgorithm
{
    None,
    Deflate,
    Brotli,
    GZip
}

public class CompressedPayload
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.None;
    public int OriginalSize { get; set; }
    public int CompressedSize { get; set; }
    public float CompressionRatio => OriginalSize > 0 ? 1f - ((float)CompressedSize / OriginalSize) : 0f;
}
