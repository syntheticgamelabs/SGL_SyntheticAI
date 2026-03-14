using System.Collections.Concurrent;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Self-Healing Network Architecture — monitors all nodes in the cluster
/// hierarchy, detects failures, and automatically performs recovery actions:
///
///   - Health checks on workers, clusters, and agents
///   - Automatic task reassignment when workers fail
///   - Cluster failover (re-route agents to another cluster)
///   - Service restart scheduling for degraded components
///   - Load rebalancing across healthy nodes
///
/// Integrates with the Digital Immune System to consider quarantine
/// status and threat data when making recovery decisions.
/// </summary>
public sealed class SelfHealingNetwork : IDisposable
{
    private readonly ConcurrentDictionary<string, HealthCheckResult> _latestHealth = new();
    private readonly ConcurrentBag<RecoveryAction> _recoveryHistory = new();
    private readonly Timer _healthCheckTimer;

    private HierarchicalClusterService? _clusterService;
    private SwarmScheduler? _swarmScheduler;
    private DigitalImmuneSystem? _immuneSystem;

    public int HealthyNodes => _latestHealth.Values.Count(h => h.IsHealthy);
    public int UnhealthyNodes => _latestHealth.Values.Count(h => !h.IsHealthy);
    public int RecoveryActionsPerformed => _recoveryHistory.Count;

    public SelfHealingNetwork()
    {
        // Run health checks every 45 seconds
        _healthCheckTimer = new Timer(_ => RunHealthChecks(), null, TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45));
    }

    // ─── Dependency Injection (post-construction) ────────────────

    public void SetClusterService(HierarchicalClusterService cs) => _clusterService = cs;
    public void SetSwarmScheduler(SwarmScheduler ss) => _swarmScheduler = ss;
    public void SetImmuneSystem(DigitalImmuneSystem ims) => _immuneSystem = ims;

    // ─── Health Check Core ───────────────────────────────────────

    /// <summary>
    /// Runs full health checks across all known nodes. Called automatically by timer
    /// and can also be called on-demand from admin endpoints.
    /// </summary>
    public void RunHealthChecks()
    {
        try
        {
            CheckWorkers();
            CheckClusters();
            CheckAgents();
        }
        catch (Exception ex)
        {
            SglLogger.Error($"[SelfHeal] Health check cycle failed: {ex.Message}");
        }
    }

    private void CheckWorkers()
    {
        if (_swarmScheduler == null) return;
        var workers = _swarmScheduler.GetWorkers();
        foreach (var worker in workers)
        {
            var result = new HealthCheckResult
            {
                NodeId = worker.WorkerId,
                NodeType = "worker"
            };

            var issues = new List<string>();

            // Check heartbeat freshness
            var sinceBeat = DateTime.UtcNow - worker.LastHeartbeat;
            if (sinceBeat > TimeSpan.FromMinutes(5))
            {
                issues.Add($"No heartbeat for {sinceBeat.TotalMinutes:F0}m");
                result.HealthScore -= 0.5f;
            }
            else if (sinceBeat > TimeSpan.FromMinutes(2))
            {
                issues.Add($"Stale heartbeat ({sinceBeat.TotalMinutes:F1}m)");
                result.HealthScore -= 0.2f;
            }

            // Check failure rate
            if (worker.CompletedTasks + worker.FailedTasks > 10 && worker.SuccessRate < 0.5f)
            {
                issues.Add($"High failure rate: {worker.SuccessRate:P0}");
                result.HealthScore -= 0.3f;
            }

            // Check load
            if (worker.CurrentLoad > 0.95f)
            {
                issues.Add($"Overloaded: {worker.CurrentLoad:P0}");
                result.HealthScore -= 0.1f;
            }

            // Check quarantine
            if (_immuneSystem?.IsQuarantined(worker.WorkerId) == true)
            {
                issues.Add("Node is quarantined");
                result.HealthScore -= 0.4f;
            }

            result.HealthScore = Math.Clamp(result.HealthScore, 0f, 1f);
            result.IsHealthy = result.HealthScore >= 0.5f && issues.Count == 0;
            result.Issues = issues;
            result.Metrics["success_rate"] = worker.SuccessRate;
            result.Metrics["load"] = worker.CurrentLoad;
            result.Metrics["latency"] = worker.Latency;
            _latestHealth[worker.WorkerId] = result;

            // Trigger recovery if unhealthy
            if (!result.IsHealthy)
            {
                HandleUnhealthyWorker(worker, result);
            }
        }
    }

    private void CheckClusters()
    {
        if (_clusterService == null) return;
        var clusters = _clusterService.GetClusters();
        foreach (var cluster in clusters)
        {
            var result = new HealthCheckResult
            {
                NodeId = cluster.ClusterId,
                NodeType = "cluster"
            };

            var issues = new List<string>();
            var sinceBeat = DateTime.UtcNow - cluster.LastHeartbeat;
            if (sinceBeat > TimeSpan.FromMinutes(5))
            {
                issues.Add($"No heartbeat for {sinceBeat.TotalMinutes:F0}m");
                result.HealthScore -= 0.5f;
            }
            if (cluster.Status == ClusterStatus.Degraded)
            {
                issues.Add("Cluster is in degraded state");
                result.HealthScore -= 0.3f;
            }
            if (cluster.Status == ClusterStatus.Offline)
            {
                issues.Add("Cluster is offline");
                result.HealthScore = 0f;
            }
            if (cluster.AvailableCapacity < 0.1f)
            {
                issues.Add($"Very low capacity: {cluster.AvailableCapacity:P0}");
                result.HealthScore -= 0.2f;
            }

            result.HealthScore = Math.Clamp(result.HealthScore, 0f, 1f);
            result.IsHealthy = result.HealthScore >= 0.5f;
            result.Issues = issues;
            result.Metrics["capacity"] = cluster.AvailableCapacity;
            result.Metrics["gpu_power"] = cluster.AggregateGpuPower;
            result.Metrics["agent_count"] = cluster.ConnectedAgentCount;
            _latestHealth[cluster.ClusterId] = result;

            if (!result.IsHealthy && cluster.Status != ClusterStatus.Offline)
            {
                HandleUnhealthyCluster(cluster, result);
            }
        }
    }

    private void CheckAgents()
    {
        if (_clusterService == null) return;
        var agents = _clusterService.GetAgents();
        foreach (var agent in agents)
        {
            var result = new HealthCheckResult
            {
                NodeId = agent.AgentId,
                NodeType = "agent"
            };

            var issues = new List<string>();
            var sinceBeat = DateTime.UtcNow - agent.LastHeartbeat;
            if (sinceBeat > TimeSpan.FromMinutes(5))
            {
                issues.Add($"No heartbeat for {sinceBeat.TotalMinutes:F0}m");
                result.HealthScore -= 0.5f;
            }
            if (agent.Status == AgentStatus.Disconnected)
            {
                result.HealthScore = 0f;
            }

            result.HealthScore = Math.Clamp(result.HealthScore, 0f, 1f);
            result.IsHealthy = result.HealthScore >= 0.5f;
            result.Issues = issues;
            _latestHealth[agent.AgentId] = result;
        }
    }

    // ─── Recovery Actions ────────────────────────────────────────

    private void HandleUnhealthyWorker(WorkerInfo worker, HealthCheckResult health)
    {
        // If worker has an endpoint and is unreachable, mark offline and reassign tasks
        if (worker.Status == WorkerStatus.Offline)
        {
            var action = new RecoveryAction
            {
                TargetNodeId = worker.WorkerId,
                Type = RecoveryType.Reassign,
                Description = $"Worker offline. Issues: {string.Join("; ", health.Issues)}"
            };

            // Reassign pending tasks from this worker to others
            var jobs = _swarmScheduler?.GetJobs(100) ?? new();
            int reassigned = 0;
            foreach (var job in jobs.Where(j => j.Status == TaskGraphStatus.Running))
            {
                foreach (var node in job.Nodes.Where(n =>
                    n.AssignedWorker == worker.WorkerId &&
                    n.Status == TaskNodeStatus.Running))
                {
                    // Find a replacement worker
                    var replacement = _swarmScheduler?.SelectWorker(node.Operation);
                    if (replacement != null && replacement.WorkerId != worker.WorkerId)
                    {
                        node.AssignedWorker = replacement.WorkerId;
                        node.Status = TaskNodeStatus.Pending;
                        node.RetryCount++;
                        reassigned++;
                    }
                }
            }

            action.Success = true;
            action.CompletedAt = DateTime.UtcNow;
            action.Description += $" Reassigned {reassigned} tasks.";
            _recoveryHistory.Add(action);
            SglLogger.Information($"[SelfHeal] Reassigned {reassigned} tasks from offline worker {worker.WorkerId}");
        }
        else if (health.HealthScore < 0.3f)
        {
            // Worker is severely degraded — reduce its load
            var action = new RecoveryAction
            {
                TargetNodeId = worker.WorkerId,
                Type = RecoveryType.Rebalance,
                Description = $"Worker severely degraded (health={health.HealthScore:F2}). Reducing load."
            };
            worker.Status = WorkerStatus.Maintenance;
            action.Success = true;
            action.CompletedAt = DateTime.UtcNow;
            _recoveryHistory.Add(action);
        }
    }

    private void HandleUnhealthyCluster(EdgeCluster cluster, HealthCheckResult health)
    {
        if (_clusterService == null) return;

        var action = new RecoveryAction
        {
            TargetNodeId = cluster.ClusterId,
            Type = RecoveryType.Failover,
            Description = $"Cluster {cluster.Name} unhealthy. Issues: {string.Join("; ", health.Issues)}"
        };

        // Find agents in this cluster and try to move them to another healthy cluster
        var agents = _clusterService.GetAgentsForCluster(cluster.ClusterId);
        var healthyClusters = _clusterService.GetClusters()
            .Where(c => c.ClusterId != cluster.ClusterId && c.Status == ClusterStatus.Online)
            .OrderByDescending(c => c.AvailableCapacity)
            .ToList();

        if (healthyClusters.Count > 0)
        {
            var target = healthyClusters[0];
            int moved = 0;
            foreach (var agent in agents)
            {
                agent.ClusterId = target.ClusterId;
                moved++;
            }
            action.Description += $" Moved {moved} agents to cluster {target.Name}.";
            action.Success = true;
        }
        else
        {
            // No healthy cluster — move agents to direct-to-core mode
            foreach (var agent in agents)
            {
                agent.ClusterId = string.Empty; // direct-to-core
            }
            action.Description += $" Moved {agents.Count} agents to direct-to-core mode (no healthy clusters).";
            action.Success = true;
        }

        action.CompletedAt = DateTime.UtcNow;
        _recoveryHistory.Add(action);
        SglLogger.Information($"[SelfHeal] Cluster failover: {action.Description}");
    }

    // ─── Public API ──────────────────────────────────────────────

    public HealthCheckResult? GetNodeHealth(string nodeId) =>
        _latestHealth.TryGetValue(nodeId, out var h) ? h : null;

    public List<HealthCheckResult> GetAllHealth() =>
        _latestHealth.Values.ToList();

    public List<RecoveryAction> GetRecoveryHistory(int count = 100) =>
        _recoveryHistory.OrderByDescending(r => r.InitiatedAt).Take(count).ToList();

    public SelfHealingStats GetStats() => new()
    {
        TotalNodes = _latestHealth.Count,
        HealthyNodes = HealthyNodes,
        UnhealthyNodes = UnhealthyNodes,
        RecoveryActionsPerformed = RecoveryActionsPerformed,
        LastCheckAt = _latestHealth.Values.Any()
            ? _latestHealth.Values.Max(h => h.CheckedAt)
            : DateTime.MinValue
    };

    public void Dispose()
    {
        _healthCheckTimer.Dispose();
    }
}

public class SelfHealingStats
{
    public int TotalNodes { get; set; }
    public int HealthyNodes { get; set; }
    public int UnhealthyNodes { get; set; }
    public int RecoveryActionsPerformed { get; set; }
    public DateTime LastCheckAt { get; set; }
}
