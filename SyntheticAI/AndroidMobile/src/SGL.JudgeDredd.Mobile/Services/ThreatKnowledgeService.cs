using System.Text.Json;
using SGL.JudgeDredd.Mobile.Models;

namespace SGL.JudgeDredd.Mobile.Services;

public class ThreatKnowledgeService
{
    private readonly string _dbPath;
    private ThreatKnowledgeDb _db;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ThreatKnowledgeService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "threat_knowledge.json");
        _db = LoadOrCreate();
    }

    private ThreatKnowledgeDb LoadOrCreate()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                var json = File.ReadAllText(_dbPath);
                return JsonSerializer.Deserialize<ThreatKnowledgeDb>(json) ?? new ThreatKnowledgeDb();
            }
        }
        catch { }
        return new ThreatKnowledgeDb
        {
            DeviceId = DeviceInfo.Current.Name + "_" + DeviceInfo.Current.Platform
        };
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _db.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dbPath, json);
        }
        finally { _lock.Release(); }
    }

    public async Task AddThreatEntryAsync(ThreatKnowledgeEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            _db.Entries.Add(entry);
            UpdatePatterns(entry);
        }
        finally { _lock.Release(); }
        await SaveAsync();
    }

    private void UpdatePatterns(ThreatKnowledgeEntry entry)
    {
        var existing = _db.LearnedPatterns.FirstOrDefault(p => p.Pattern == entry.ThreatType);
        if (existing != null)
        {
            existing.Occurrences++;
            existing.LastSeen = DateTime.UtcNow;
            existing.Confidence = Math.Min(1.0, existing.Confidence + 0.05);
        }
        else
        {
            _db.LearnedPatterns.Add(new LearnedPattern
            {
                Pattern = entry.ThreatType,
                PatternType = "threat_category",
                Confidence = 0.3,
                Occurrences = 1
            });
        }

        foreach (var perm in entry.Permissions)
        {
            var permPattern = _db.LearnedPatterns.FirstOrDefault(p => p.Pattern == $"perm:{perm}");
            if (permPattern != null)
            {
                permPattern.Occurrences++;
                permPattern.LastSeen = DateTime.UtcNow;
            }
            else
            {
                _db.LearnedPatterns.Add(new LearnedPattern
                {
                    Pattern = $"perm:{perm}",
                    PatternType = "dangerous_permission",
                    Confidence = 0.2,
                    Occurrences = 1
                });
            }
        }
    }

    public string GetKnowledgeJson()
    {
        return JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
    }

    public ThreatKnowledgeDb GetDatabase() => _db;

    public int TotalThreatsLogged => _db.Entries.Count;
    public int PatternsLearned => _db.LearnedPatterns.Count;

    public async Task<bool> SyncWithServerAsync(ApiClient apiClient)
    {
        try
        {
            var json = GetKnowledgeJson();
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await apiClient.PostThreatKnowledgeAsync(json);
            return response;
        }
        catch { return false; }
    }

    public List<ThreatKnowledgeEntry> GetRecentThreats(int count = 20)
    {
        return _db.Entries.OrderByDescending(e => e.Timestamp).Take(count).ToList();
    }

    public double GetThreatScore(string packageName, List<string> permissions)
    {
        double score = 0;
        foreach (var perm in permissions)
        {
            var pattern = _db.LearnedPatterns.FirstOrDefault(p => p.Pattern == $"perm:{perm}");
            if (pattern != null)
                score += pattern.Confidence * 0.1;
        }
        return Math.Min(1.0, score);
    }
}