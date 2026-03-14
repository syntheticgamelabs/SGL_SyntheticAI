using System.Collections.Concurrent;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Core seed management service for the distributed AI learning network.
/// Handles seed ingestion, validation, consensus scoring, storage, and distribution.
/// Seeds are the knowledge DNA packets exchanged between server and clients.
/// Persists seeds and reputation data to disk every 60 seconds.
/// </summary>
public class SeedService
{
    private readonly ConcurrentDictionary<string, SeedPackage> _seeds = new();
    private readonly ConcurrentDictionary<string, ClientReputation> _clientReputations = new();
    private readonly SeedValidator _validator = new();
    private readonly object _lock = new();
    private readonly string _seedsFilePath;
    private readonly string _reputationFilePath;
    private readonly Timer _persistTimer;

    /// <summary>Total seeds currently stored.</summary>
    public int SeedCount => _seeds.Count;

    /// <summary>Number of clients with reputation records.</summary>
    public int ClientCount => _clientReputations.Count;

    public SeedService()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _seedsFilePath = Path.Combine(dataDir, "swarm_seeds.json");
        _reputationFilePath = Path.Combine(dataDir, "swarm_reputation.json");
        LoadFromDisk();
        _persistTimer = new Timer(_ => SaveToDisk(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Process an incoming seed from a client. Validates, scores, and stores it.
    /// </summary>
    public async Task<SeedProcessResult> ProcessSeedAsync(SeedPackage seed)
    {
        if (seed == null)
            return new SeedProcessResult { Success = false, Message = "Seed package is null" };

        // Ensure client reputation exists
        var reputation = _clientReputations.GetOrAdd(seed.ClientId, id => new ClientReputation
        {
            ClientId = id,
            FirstSeen = DateTime.UtcNow
        });
        reputation.LastActive = DateTime.UtcNow;
        reputation.SeedsSubmitted++;

        // Validate the seed
        var validationResult = _validator.Validate(seed, reputation);
        if (!validationResult.IsValid)
        {
            reputation.SeedsRejected++;
            reputation.RecalculateTrust();
            seed.Status = SeedStatus.Rejected;
            return new SeedProcessResult
            {
                Success = false,
                Message = validationResult.Reason,
                SeedId = seed.SeedId
            };
        }

        // Apply consensus scoring
        seed.Score = CalculateConsensusScore(seed, reputation);
        seed.Status = seed.Score >= 0.6f ? SeedStatus.Accepted : SeedStatus.Pending;

        if (seed.Status == SeedStatus.Accepted)
        {
            reputation.SeedsAccepted++;
        }

        reputation.RecalculateTrust();

        // Store the seed
        _seeds[seed.SeedId] = seed;

        SglLogger.Information("Seed {SeedId} from client {ClientId} processed: {Status} (score: {Score:F2})",
            seed.SeedId, seed.ClientId, seed.Status, seed.Score);

        return new SeedProcessResult
        {
            Success = true,
            SeedId = seed.SeedId,
            Score = seed.Score,
            Status = seed.Status,
            Message = $"Seed processed successfully. Score: {seed.Score:F2}"
        };
    }

    /// <summary>
    /// Get the top-scoring seeds for a client to learn from.
    /// </summary>
    public List<SeedPackage> GetTopSeeds(int count = 20, string? taskType = null, string? excludeClientId = null)
    {
        var query = _seeds.Values.Where(s => s.Status == SeedStatus.Accepted || s.Status == SeedStatus.Distributed);

        if (!string.IsNullOrEmpty(taskType))
            query = query.Where(s => s.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(excludeClientId))
            query = query.Where(s => s.ClientId != excludeClientId);

        return query
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get seeds matching a specific task type for training.
    /// </summary>
    public List<SeedPackage> GetSeedsByTaskType(string taskType, int count = 50)
    {
        return _seeds.Values
            .Where(s => s.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase) &&
                        s.Status == SeedStatus.Accepted)
            .OrderByDescending(s => s.Score)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Upload a batch of seeds from a client.
    /// </summary>
    public async Task<BatchSeedResult> ProcessBatchAsync(List<SeedPackage> seeds, string clientId)
    {
        var result = new BatchSeedResult { TotalSubmitted = seeds.Count };

        foreach (var seed in seeds)
        {
            seed.ClientId = clientId;
            var processResult = await ProcessSeedAsync(seed);
            if (processResult.Success)
                result.Accepted++;
            else
                result.Rejected++;
        }

        return result;
    }

    /// <summary>
    /// Get a client's reputation/trust score.
    /// </summary>
    public ClientReputation? GetClientReputation(string clientId)
    {
        return _clientReputations.TryGetValue(clientId, out var rep) ? rep : null;
    }

    /// <summary>
    /// Get summary statistics for the seed network.
    /// </summary>
    public SwarmNetworkStats GetNetworkStats()
    {
        var seeds = _seeds.Values.ToList();
        return new SwarmNetworkStats
        {
            TotalSeeds = seeds.Count,
            AcceptedSeeds = seeds.Count(s => s.Status == SeedStatus.Accepted),
            PendingSeeds = seeds.Count(s => s.Status == SeedStatus.Pending),
            RejectedSeeds = seeds.Count(s => s.Status == SeedStatus.Rejected),
            DistributedSeeds = seeds.Count(s => s.Status == SeedStatus.Distributed),
            TotalClients = _clientReputations.Count,
            AverageTrustScore = _clientReputations.Values.Any()
                ? _clientReputations.Values.Average(r => r.TrustScore) : 0f,
            TopTaskTypes = seeds
                .GroupBy(s => s.TaskType)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            AverageSeedScore = seeds.Any() ? seeds.Average(s => s.Score) : 0f,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calculate consensus score for a seed based on quality and client trust.
    /// </summary>
    private float CalculateConsensusScore(SeedPackage seed, ClientReputation reputation)
    {
        float contentScore = 0f;

        // Content quality: non-empty prompt/solution, has embedding
        if (!string.IsNullOrWhiteSpace(seed.Prompt)) contentScore += 0.2f;
        if (!string.IsNullOrWhiteSpace(seed.Solution)) contentScore += 0.3f;
        if (seed.Embedding != null && seed.Embedding.Length > 0) contentScore += 0.2f;
        if (!string.IsNullOrWhiteSpace(seed.TaskType)) contentScore += 0.1f;

        // Self-reported score (clamped and weighted low)
        float selfScore = Math.Clamp(seed.Score, 0f, 1f) * 0.1f;

        // Trust-weighted final score
        float trustWeight = reputation.TrustScore * 0.3f;

        return Math.Clamp(contentScore + selfScore + trustWeight, 0f, 1f);
    }

    private void SaveToDisk()
    {
        try
        {
            // Save accepted/pending seeds (limit to 5000 most recent)
            var seedList = _seeds.Values
                .OrderByDescending(s => s.Timestamp)
                .Take(5000)
                .ToList();
            var seedJson = JsonSerializer.Serialize(seedList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_seedsFilePath, seedJson);

            // Save reputation data
            var repList = _clientReputations.Values.ToList();
            var repJson = JsonSerializer.Serialize(repList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_reputationFilePath, repJson);
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to save swarm seed data: {Error}", ex, ex.Message);
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_seedsFilePath))
            {
                var json = File.ReadAllText(_seedsFilePath);
                var seeds = JsonSerializer.Deserialize<List<SeedPackage>>(json);
                if (seeds != null)
                {
                    foreach (var seed in seeds)
                        _seeds.TryAdd(seed.SeedId, seed);
                    SglLogger.Information("Loaded {Count} seeds from disk", seeds.Count);
                }
            }

            if (File.Exists(_reputationFilePath))
            {
                var json = File.ReadAllText(_reputationFilePath);
                var reps = JsonSerializer.Deserialize<List<ClientReputation>>(json);
                if (reps != null)
                {
                    foreach (var rep in reps)
                        _clientReputations.TryAdd(rep.ClientId, rep);
                    SglLogger.Information("Loaded {Count} client reputations from disk", reps.Count);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to load swarm seed data: {Error}", ex, ex.Message);
        }
    }
}

/// <summary>
/// Validates seed packages before acceptance.
/// </summary>
public class SeedValidator
{
    public ValidationResult Validate(SeedPackage seed, ClientReputation reputation)
    {
        if (string.IsNullOrWhiteSpace(seed.Prompt))
            return new ValidationResult { IsValid = false, Reason = "Seed prompt is empty" };

        if (string.IsNullOrWhiteSpace(seed.TaskType))
            return new ValidationResult { IsValid = false, Reason = "Task type is required" };

        if (string.IsNullOrWhiteSpace(seed.ClientId))
            return new ValidationResult { IsValid = false, Reason = "Client ID is required" };

        // Block untrusted clients from flooding
        if (reputation.TrustScore < 0.1f && reputation.SeedsSubmitted > 100)
            return new ValidationResult { IsValid = false, Reason = "Client trust too low for further submissions" };

        // Rate limit: max 100 seeds per minute per client
        if (reputation.SeedsSubmitted > 0 &&
            (DateTime.UtcNow - reputation.FirstSeen).TotalMinutes > 0 &&
            reputation.SeedsSubmitted / (DateTime.UtcNow - reputation.FirstSeen).TotalMinutes > 100)
            return new ValidationResult { IsValid = false, Reason = "Rate limit exceeded" };

        return new ValidationResult { IsValid = true };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SeedProcessResult
{
    public bool Success { get; set; }
    public string SeedId { get; set; } = string.Empty;
    public float Score { get; set; }
    public SeedStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BatchSeedResult
{
    public int TotalSubmitted { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}

public class SwarmNetworkStats
{
    public int TotalSeeds { get; set; }
    public int AcceptedSeeds { get; set; }
    public int PendingSeeds { get; set; }
    public int RejectedSeeds { get; set; }
    public int DistributedSeeds { get; set; }
    public int TotalClients { get; set; }
    public float AverageTrustScore { get; set; }
    public Dictionary<string, int> TopTaskTypes { get; set; } = new();
    public float AverageSeedScore { get; set; }
    public DateTime Timestamp { get; set; }
}
