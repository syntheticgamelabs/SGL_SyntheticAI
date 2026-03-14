using System.Collections.Concurrent;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Manages the 3-layer hierarchical swarm cluster:
///   Layer 1 — Global Core (this server)
///   Layer 2 — Edge Clusters (regional aggregators)
///   Layer 3 — Client Agents (individual devices)
///
/// Provides real registration, heartbeat, scoring, task routing,
/// gossip propagation, and cluster-aware worker selection.
/// </summary>
public sealed class HierarchicalClusterService : IDisposable
{
    private readonly ConcurrentDictionary<string, EdgeCluster> _clusters = new();
    private readonly ConcurrentDictionary<string, ClientAgent> _agents = new();
    private readonly ConcurrentDictionary<string, List<GossipMessage>> _gossipQueue = new();
    private readonly ConcurrentDictionary<string, GossipMessage> _seenGossip = new();
    private readonly Timer _healthTimer;
    private readonly Timer _persistTimer;
    private readonly string _dataDir;

    public int ClusterCount => _clusters.Count;
    public int AgentCount => _agents.Count;
    public float TotalGpuPower => _clusters.Values.Sum(c => c.AggregateGpuPower) +
                                   _agents.Values.Where(a => string.IsNullOrEmpty(a.ClusterId))
                                                  .Sum(a => a.ContributedGpuPower);

    public HierarchicalClusterService()
    {
        _dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_dataDir);
        LoadFromDisk();
        // Health check every 30 seconds
        _healthTimer = new Timer(_ => PruneStaleNodes(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        // Persist every 60 seconds
        _persistTimer = new Timer(_ => SaveToDisk(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    // ─── Edge Cluster Management ─────────────────────────────────

    public void RegisterCluster(EdgeCluster cluster)
    {
        cluster.JoinedAt = DateTime.UtcNow;
        cluster.LastHeartbeat = DateTime.UtcNow;
        cluster.Status = ClusterStatus.Online;
        _clusters[cluster.ClusterId] = cluster;
        _gossipQueue[cluster.ClusterId] = new List<GossipMessage>();
        SglLogger.Information($"[Cluster] Edge cluster registered: {cluster.Name} ({cluster.ClusterId}) region={cluster.Region}");
    }

    public bool ClusterHeartbeat(string clusterId, int agentCount, float gpuPower, float capacity)
    {
        if (!_clusters.TryGetValue(clusterId, out var cluster)) return false;
        cluster.LastHeartbeat = DateTime.UtcNow;
        cluster.ConnectedAgentCount = agentCount;
        cluster.AggregateGpuPower = gpuPower;
        cluster.AvailableCapacity = capacity;
        if (cluster.Status == ClusterStatus.Offline) cluster.Status = ClusterStatus.Online;
        return true;
    }

    public EdgeCluster? GetCluster(string clusterId) =>
        _clusters.TryGetValue(clusterId, out var c) ? c : null;

    public List<EdgeCluster> GetClusters() => _clusters.Values.ToList();

    public EdgeCluster? SelectBestCluster(string operation)
    {
        var online = _clusters.Values.Where(c => c.Status == ClusterStatus.Online && c.AvailableCapacity > 0.1f).ToList();
        if (online.Count == 0) return null;

        EdgeCluster? best = null;
        float bestScore = float.MinValue;
        foreach (var cluster in online)
        {
            float capMatch = cluster.Capabilities.Contains(operation) ? 1.0f : 0.0f;
            float score = (cluster.AggregateGpuPower * 0.30f)
                        + (cluster.TrustScore * 0.25f)
                        + (capMatch * 0.25f)
                        + (cluster.AvailableCapacity * 0.20f);
            if (score > bestScore) { bestScore = score; best = cluster; }
        }
        return best;
    }

    // ─── Client Agent Management ─────────────────────────────────

    public void RegisterAgent(ClientAgent agent)
    {
        agent.ConnectedAt = DateTime.UtcNow;
        agent.LastHeartbeat = DateTime.UtcNow;
        agent.Status = AgentStatus.Connected;
        _agents[agent.AgentId] = agent;
        SglLogger.Information($"[Cluster] Client agent registered: {agent.DeviceName} ({agent.AgentId}) platform={agent.Platform}" +
                       (string.IsNullOrEmpty(agent.ClusterId) ? " [direct-to-core]" : $" cluster={agent.ClusterId}"));
    }

    public bool AgentHeartbeat(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var agent)) return false;
        agent.LastHeartbeat = DateTime.UtcNow;
        if (agent.Status == AgentStatus.Disconnected) agent.Status = AgentStatus.Connected;
        return true;
    }

    public ClientAgent? GetAgent(string agentId) =>
        _agents.TryGetValue(agentId, out var a) ? a : null;

    public List<ClientAgent> GetAgents(string? clusterId = null)
    {
        var list = _agents.Values.AsEnumerable();
        if (clusterId != null) list = list.Where(a => a.ClusterId == clusterId);
        return list.ToList();
    }

    public List<ClientAgent> GetAgentsForCluster(string clusterId) =>
        _agents.Values.Where(a => a.ClusterId == clusterId).ToList();

    public void RecordAgentTaskResult(string agentId, bool success)
    {
        if (!_agents.TryGetValue(agentId, out var agent)) return;
        if (success) agent.TasksCompleted++;
        else agent.TasksFailed++;
        // Update trust based on success rate
        agent.TrustScore = Math.Clamp(
            0.3f + (agent.SuccessRate * 0.7f),
            0.0f, 1.0f);
    }

    // ─── Gossip Protocol ─────────────────────────────────────────

    public void BroadcastGossip(GossipMessage message)
    {
        if (!_seenGossip.TryAdd(message.MessageId, message)) return;

        foreach (var kvp in _gossipQueue)
        {
            if (message.SeenBy.Contains(kvp.Key)) continue;
            lock (kvp.Value)
            {
                kvp.Value.Add(message);
            }
        }
        SglLogger.Information($"[Gossip] Broadcast {message.Type} from {message.SenderId} TTL={message.Ttl}");
    }

    public List<GossipMessage> DrainGossipForCluster(string clusterId)
    {
        if (!_gossipQueue.TryGetValue(clusterId, out var queue)) return new();
        List<GossipMessage> messages;
        lock (queue)
        {
            messages = new List<GossipMessage>(queue);
            queue.Clear();
        }
        // Mark as seen by this cluster, decrement TTL
        foreach (var m in messages)
        {
            m.SeenBy.Add(clusterId);
            m.Ttl--;
        }
        return messages.Where(m => m.Ttl > 0).ToList();
    }

    public void ReceiveGossipFromCluster(string clusterId, GossipMessage message)
    {
        message.SeenBy.Add(clusterId);
        if (message.Ttl <= 0 || !_seenGossip.TryAdd(message.MessageId, message)) return;
        // Re-broadcast to other clusters
        foreach (var kvp in _gossipQueue)
        {
            if (kvp.Key == clusterId || message.SeenBy.Contains(kvp.Key)) continue;
            lock (kvp.Value) { kvp.Value.Add(message); }
        }
    }

    /// <summary>
    /// Returns a snapshot of the entire cluster hierarchy for admin/metrics display.
    /// </summary>
    public ClusterHierarchySnapshot GetHierarchySnapshot()
    {
        var snapshot = new ClusterHierarchySnapshot
        {
            TotalClusters = _clusters.Count,
            TotalAgents = _agents.Count,
            TotalGpuPower = TotalGpuPower,
            OnlineClusters = _clusters.Values.Count(c => c.Status == ClusterStatus.Online),
            ActiveAgents = _agents.Values.Count(a => a.Status is AgentStatus.Connected or AgentStatus.Working)
        };
        foreach (var cluster in _clusters.Values)
        {
            snapshot.ClusterDetails.Add(new ClusterDetail
            {
                ClusterId = cluster.ClusterId,
                Name = cluster.Name,
                Region = cluster.Region,
                Status = cluster.Status.ToString(),
                AgentCount = _agents.Values.Count(a => a.ClusterId == cluster.ClusterId),
                GpuPower = cluster.AggregateGpuPower,
                TrustScore = cluster.TrustScore
            });
        }
        return snapshot;
    }

    // ─── Health + Pruning ────────────────────────────────────────

    private void PruneStaleNodes()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        foreach (var cluster in _clusters.Values)
        {
            if (cluster.LastHeartbeat < cutoff && cluster.Status != ClusterStatus.Offline)
            {
                cluster.Status = ClusterStatus.Offline;
                SglLogger.Warning($"[Cluster] Edge cluster went offline: {cluster.Name} ({cluster.ClusterId})");
            }
        }

        foreach (var agent in _agents.Values)
        {
            if (agent.LastHeartbeat < cutoff && agent.Status != AgentStatus.Disconnected)
            {
                agent.Status = AgentStatus.Disconnected;
            }
        }

        // Prune old gossip (>1 hour old)
        var gossipCutoff = DateTime.UtcNow.AddHours(-1);
        var stale = _seenGossip.Where(kvp => kvp.Value.CreatedAt < gossipCutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in stale) _seenGossip.TryRemove(key, out _);
    }

    // ─── Persistence ─────────────────────────────────────────────

    private void SaveToDisk()
    {
        try
        {
            var state = new
            {
                clusters = _clusters.Values.ToList(),
                agents = _agents.Values.Take(5000).ToList()
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_dataDir, "swarm_clusters.json"), json);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"[Cluster] Failed to persist cluster state: {ex.Message}");
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            var path = Path.Combine(_dataDir, "swarm_clusters.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("clusters", out var clustersEl))
            {
                var clusters = JsonSerializer.Deserialize<List<EdgeCluster>>(clustersEl.GetRawText());
                if (clusters != null)
                    foreach (var c in clusters) _clusters[c.ClusterId] = c;
            }
            if (doc.RootElement.TryGetProperty("agents", out var agentsEl))
            {
                var agents = JsonSerializer.Deserialize<List<ClientAgent>>(agentsEl.GetRawText());
                if (agents != null)
                    foreach (var a in agents) _agents[a.AgentId] = a;
            }
            SglLogger.Information($"[Cluster] Loaded {_clusters.Count} clusters, {_agents.Count} agents from disk");
        }
        catch (Exception ex)
        {
            SglLogger.Error($"[Cluster] Failed to load cluster state: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _healthTimer.Dispose();
        _persistTimer.Dispose();
        SaveToDisk();
    }
}

/// <summary>
/// Snapshot DTO for admin/metrics display of the cluster hierarchy.
/// </summary>
public class ClusterHierarchySnapshot
{
    public int TotalClusters { get; set; }
    public int OnlineClusters { get; set; }
    public int TotalAgents { get; set; }
    public int ActiveAgents { get; set; }
    public float TotalGpuPower { get; set; }
    public List<ClusterDetail> ClusterDetails { get; set; } = new();
}

public class ClusterDetail
{
    public string ClusterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AgentCount { get; set; }
    public float GpuPower { get; set; }
    public float TrustScore { get; set; }
}
