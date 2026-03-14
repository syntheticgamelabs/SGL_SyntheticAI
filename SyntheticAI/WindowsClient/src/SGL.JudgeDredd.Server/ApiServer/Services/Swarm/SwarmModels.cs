namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Represents a seed package — a unit of knowledge exchanged between server and clients
/// in the distributed AI learning network. Seeds contain training prompts, feedback,
/// embeddings, and quality scores.
/// </summary>
public class SeedPackage
{
    public string SeedId { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskType { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public float Score { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSignature { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SeedStatus Status { get; set; } = SeedStatus.Pending;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum SeedStatus
{
    Pending,
    Validated,
    Accepted,
    Rejected,
    Distributed
}

/// <summary>
/// A worker node that can execute tasks in the swarm network.
/// Workers can be GPU cluster nodes, client CPU workers, or edge devices.
/// Remote workers must provide an EndpointUrl for HTTP dispatch.
/// </summary>
public class WorkerInfo
{
    public string WorkerId { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public float GpuPower { get; set; }
    public float TrustScore { get; set; } = 0.5f;
    public float CurrentLoad { get; set; }
    public float Latency { get; set; }
    public float PerformanceScore { get; set; }
    public int GpuCount { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public WorkerStatus Status { get; set; } = WorkerStatus.Idle;
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }

    /// <summary>
    /// HTTP endpoint URL for remote task dispatch (e.g. "http://192.168.1.10:8080").
    /// When null, the worker's assigned tasks execute locally on the server.
    /// </summary>
    public string? EndpointUrl { get; set; }

    public float SuccessRate => CompletedTasks + FailedTasks > 0
        ? (float)CompletedTasks / (CompletedTasks + FailedTasks)
        : 0f;
}

public enum WorkerStatus
{
    Idle,
    Busy,
    Offline,
    Maintenance
}

/// <summary>
/// A task node in the DAG execution graph.
/// </summary>
public class TaskNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N");
    public string JobId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public string? AssignedWorker { get; set; }
    public string InputData { get; set; } = string.Empty;
    public string? OutputData { get; set; }
    public TaskNodeStatus Status { get; set; } = TaskNodeStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
}

public enum TaskNodeStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// A directed acyclic graph of task nodes representing a distributed job.
/// </summary>
public class TaskGraph
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public string RequesterId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TaskNode> Nodes { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TaskGraphStatus Status { get; set; } = TaskGraphStatus.Created;
    public string? FinalResult { get; set; }
}

public enum TaskGraphStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Client reputation/trust tracking for consensus validation.
/// </summary>
public class ClientReputation
{
    public string ClientId { get; set; } = string.Empty;
    public float TrustScore { get; set; } = 0.5f;
    public int SeedsSubmitted { get; set; }
    public int SeedsAccepted { get; set; }
    public int SeedsRejected { get; set; }
    public float PeerFeedbackScore { get; set; }
    public TimeSpan TotalUptime { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    public void RecalculateTrust()
    {
        float successRate = SeedsSubmitted > 0
            ? (float)SeedsAccepted / SeedsSubmitted
            : 0f;
        float uptimeScore = Math.Min(1f, (float)TotalUptime.TotalHours / 720f); // Max 30 days
        TrustScore = (successRate * 0.5f) + (uptimeScore * 0.2f) + (PeerFeedbackScore * 0.2f) + 0.1f;
        TrustScore = Math.Clamp(TrustScore, 0f, 1f);
    }
}

/// <summary>
/// Knowledge graph node linking tasks and seeds into a semantic network.
/// </summary>
public class KnowledgeNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N");
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<string> LinkedSeedIds { get; set; } = new();
    public List<string> ChildNodeIds { get; set; } = new();
    public string? ParentNodeId { get; set; }
    public float Relevance { get; set; } = 1f;
}
