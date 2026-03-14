using System.Collections.Concurrent;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Knowledge Graph service that organizes seeds, tasks, and embeddings into a
/// semantic network. Supports vector similarity search using cosine distance.
/// </summary>
public class KnowledgeGraphService
{
    private readonly ConcurrentDictionary<string, KnowledgeNode> _nodes = new();
    private readonly ConcurrentDictionary<string, float[]> _embeddings = new();
    private readonly SeedService _seedService;

    public int NodeCount => _nodes.Count;
    public int EmbeddingCount => _embeddings.Count;

    public KnowledgeGraphService(SeedService seedService)
    {
        _seedService = seedService;
        InitializeDefaultGraph();
    }

    private void InitializeDefaultGraph()
    {
        // Create root task categories
        var categories = new[]
        {
            ("security", "Security Analysis", new[] { "malware_detection", "network_analysis", "vulnerability_scan", "threat_intelligence" }),
            ("code", "Code Operations", new[] { "code_generation", "code_review", "code_analysis", "debugging" }),
            ("image", "Image Operations", new[] { "image_generation", "image_analysis", "upscaling" }),
            ("knowledge", "Knowledge Base", new[] { "qa", "summarization", "classification", "embedding" })
        };

        foreach (var (id, label, children) in categories)
        {
            var parent = new KnowledgeNode
            {
                NodeId = id,
                Category = "root",
                Label = label
            };
            _nodes[parent.NodeId] = parent;

            foreach (var childId in children)
            {
                var child = new KnowledgeNode
                {
                    NodeId = childId,
                    Category = id,
                    Label = childId.Replace("_", " "),
                    ParentNodeId = parent.NodeId
                };
                parent.ChildNodeIds.Add(child.NodeId);
                _nodes[child.NodeId] = child;
            }
        }
    }

    /// <summary>
    /// Add a seed's embedding to the vector index for similarity search.
    /// </summary>
    public void IndexSeedEmbedding(string seedId, float[] embedding)
    {
        if (embedding is { Length: > 0 })
        {
            _embeddings[seedId] = embedding;
        }
    }

    /// <summary>
    /// Link a seed to a knowledge graph node by category.
    /// </summary>
    public void LinkSeedToGraph(SeedPackage seed)
    {
        // Find matching category node
        var targetNode = _nodes.Values.FirstOrDefault(n =>
            n.Category != "root" &&
            (n.NodeId.Equals(seed.TaskType, StringComparison.OrdinalIgnoreCase) ||
             n.Label.Contains(seed.TaskType, StringComparison.OrdinalIgnoreCase)));

        if (targetNode != null)
        {
            if (!targetNode.LinkedSeedIds.Contains(seed.SeedId))
                targetNode.LinkedSeedIds.Add(seed.SeedId);
        }

        // Index embedding if present
        if (seed.Embedding is { Length: > 0 })
        {
            IndexSeedEmbedding(seed.SeedId, seed.Embedding);
        }
    }

    /// <summary>
    /// Find the most similar seeds to a query embedding using cosine similarity.
    /// </summary>
    public List<(string SeedId, float Similarity)> FindSimilarSeeds(float[] queryEmbedding, int topK = 10)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return new List<(string, float)>();

        var results = new List<(string SeedId, float Similarity)>();

        foreach (var kv in _embeddings)
        {
            if (kv.Value.Length != queryEmbedding.Length)
                continue;

            float similarity = CosineSimilarity(queryEmbedding, kv.Value);
            results.Add((kv.Key, similarity));
        }

        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Get the knowledge graph as a structured tree.
    /// </summary>
    public List<KnowledgeNode> GetGraph()
    {
        return _nodes.Values
            .Where(n => n.Category == "root")
            .OrderBy(n => n.Label)
            .ToList();
    }

    /// <summary>
    /// Get a knowledge node with its linked seeds.
    /// </summary>
    public KnowledgeNode? GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    /// <summary>
    /// Get all child nodes of a parent.
    /// </summary>
    public List<KnowledgeNode> GetChildren(string parentId)
    {
        return _nodes.Values
            .Where(n => n.ParentNodeId == parentId)
            .OrderBy(n => n.Label)
            .ToList();
    }

    /// <summary>
    /// Compute cosine similarity between two vectors.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denominator = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denominator > 0 ? dotProduct / denominator : 0f;
    }
}
