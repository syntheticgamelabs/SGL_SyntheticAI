using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;
using SGL.JudgeDredd.KnowledgeBase.Data;
using SGL.JudgeDredd.KnowledgeBase.Entities;
using SGL.JudgeDredd.KnowledgeBase.Seeders;

namespace SGL.JudgeDredd.KnowledgeBase;

public class KnowledgeBaseService : IKnowledgeBase
{
    private readonly KnowledgeDbContext _context;
    private readonly string _quarantineDir;

    public KnowledgeBaseService(KnowledgeDbContext context)
    {
        _context = context;
        _quarantineDir = Path.Combine(AppContext.BaseDirectory, "data", "quarantine");
    }

    public KnowledgeBaseService(string? dbPath = null)
    {
        _context = new KnowledgeDbContext(dbPath);
        _quarantineDir = Path.Combine(AppContext.BaseDirectory, "data", "quarantine");
    }

    /// <summary>
    /// Initializes the knowledge base: ensures the database is created, runs migrations, and seeds initial data.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();

        // Ensure quarantine directory exists
        Directory.CreateDirectory(_quarantineDir);

        // Run all seeders
        await InitialThreatSeeder.SeedAsync(_context);
        await SystemPromptSeeder.SeedAsync(_context);
        await FirewallTemplateSeeder.SeedAsync(_context);
    }

    /// <summary>
    /// Looks up a threat signature by its SHA256 hash.
    /// </summary>
    public async Task<ThreatSignature?> LookupHashAsync(string sha256Hash)
    {
        var entity = await _context.ThreatSignatures
            .FirstOrDefaultAsync(t => t.Sha256Hash == sha256Hash);

        if (entity is null)
            return null;

        return MapToThreatSignature(entity);
    }

    /// <summary>
    /// Adds a new threat signature to the knowledge base.
    /// </summary>
    public async Task AddSignatureAsync(ThreatSignature signature)
    {
        var existing = await _context.ThreatSignatures
            .FirstOrDefaultAsync(t => t.Sha256Hash == signature.Sha256Hash);

        if (existing is not null)
        {
            // Update existing signature
            existing.Name = signature.Name;
            existing.Family = signature.Family;
            existing.Severity = (int)signature.Severity;
            existing.Description = signature.Description;
            existing.Tags = JsonSerializer.Serialize(signature.Tags);
            existing.LastUpdated = DateTime.UtcNow;
            existing.Md5Hash = signature.Md5Hash;
        }
        else
        {
            var entity = new ThreatSignatureEntity
            {
                Sha256Hash = signature.Sha256Hash,
                Md5Hash = signature.Md5Hash,
                Name = signature.Name,
                Family = signature.Family,
                Severity = (int)signature.Severity,
                Description = signature.Description,
                Tags = JsonSerializer.Serialize(signature.Tags),
                FirstSeen = signature.FirstSeen != default ? signature.FirstSeen : DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _context.ThreatSignatures.Add(entity);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets remediation solutions for a given threat by its ID.
    /// </summary>
    public async Task<IReadOnlyList<RemediationAction>> GetSolutionsForThreatAsync(int threatId)
    {
        var solutions = await _context.Solutions
            .Where(s => s.ThreatId == threatId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        return solutions.Select(s => new RemediationAction
        {
            Id = s.Id,
            ThreatId = s.ThreatId,
            StepOrder = s.StepOrder,
            Description = s.Description,
            AutomatedAction = s.AutomatedAction,
            Script = s.Script
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets firewall preset templates filtered by category.
    /// </summary>
    public async Task<IReadOnlyList<FirewallPreset>> GetFirewallTemplatesAsync(string category)
    {
        var query = _context.FirewallTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(f => f.Category == category);
        }

        var templates = await query.ToListAsync();

        return templates.Select(t =>
        {
            var rules = DeserializeFirewallRules(t.RulesJson);
            return new FirewallPreset
            {
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                Rules = rules,
                IsActive = false
            };
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Logs a completed scan session to the knowledge base for historical tracking.
    /// </summary>
    public async Task LogScanResultAsync(ScanSession session)
    {
        var summaryJson = JsonSerializer.Serialize(new
        {
            session.TotalFiles,
            session.ScannedFiles,
            session.ThreatsFound,
            session.ErrorCount,
            session.ProgressPercent,
            ThreatNames = session.Threats.Select(t => t.ThreatName).Where(n => n != null).ToList(),
            Duration = session.CompletedAt.HasValue
                ? (session.CompletedAt.Value - session.StartedAt).TotalSeconds
                : (double?)null
        });

        var entity = new ScanHistoryEntity
        {
            SessionId = session.SessionId.ToString(),
            ScanType = (int)session.Type,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            TotalFiles = session.TotalFiles,
            ThreatsFound = session.ThreatsFound,
            ResultsJson = summaryJson
        };

        _context.ScanHistory.Add(entity);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Quarantines a file by moving it to the quarantine directory with encryption and logging the action.
    /// </summary>
    public async Task QuarantineFileAsync(string filePath, ThreatInfo threat)
    {
        Directory.CreateDirectory(_quarantineDir);

        // Generate quarantine file name using hash + timestamp to avoid collisions
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var quarantineFileName = $"{threat.Name}_{timestamp}.quarantine";
        var quarantinePath = Path.Combine(_quarantineDir, quarantineFileName);

        // Compute SHA256 hash of the file
        var sha256Hash = await ComputeSha256Async(filePath);

        // Encrypt the file with AES-256 to prevent accidental execution and tampering
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var encryptedBytes = EncryptQuarantineData(fileBytes);
        await File.WriteAllBytesAsync(quarantinePath, encryptedBytes);

        // Delete the original file
        File.Delete(filePath);

        // Log quarantine action in database
        var entity = new QuarantineEntity
        {
            OriginalPath = filePath,
            QuarantinePath = quarantinePath,
            Sha256Hash = sha256Hash,
            ThreatName = threat.Name,
            QuarantinedAt = DateTime.UtcNow
        };

        _context.QuarantinedFiles.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SaveThreatAnalysisAsync(string systemData, string analysisResult, string summary, int severityScore)
    {
        var entity = new ThreatAnalysisEntity
        {
            AnalyzedAt = DateTime.UtcNow,
            SystemDataSnapshot = systemData,
            LlmAnalysisResult = analysisResult,
            ThreatsSummary = summary,
            SeverityScore = severityScore
        };

        _context.ThreatAnalyses.Add(entity);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Returns the total number of threat signatures in the knowledge base.
    /// </summary>
    public async Task<int> GetSignatureCountAsync()
    {
        return await _context.ThreatSignatures.CountAsync();
    }

    /// <summary>
    /// Returns all threat signatures in the knowledge base.
    /// </summary>
    public async Task<IReadOnlyList<ThreatSignature>> GetAllSignaturesAsync()
    {
        var entities = await _context.ThreatSignatures.ToListAsync();
        return entities.Select(MapToThreatSignature).ToList().AsReadOnly();
    }

    #region Helper Methods

    private static ThreatSignature MapToThreatSignature(ThreatSignatureEntity entity)
    {
        string[] tags;
        try
        {
            tags = JsonSerializer.Deserialize<string[]>(entity.Tags) ?? [];
        }
        catch
        {
            tags = [];
        }

        return new ThreatSignature
        {
            Id = entity.Id,
            Sha256Hash = entity.Sha256Hash,
            Md5Hash = entity.Md5Hash,
            Name = entity.Name,
            Family = entity.Family,
            Severity = (ThreatSeverity)entity.Severity,
            Description = entity.Description,
            Tags = tags,
            FirstSeen = entity.FirstSeen,
            LastUpdated = entity.LastUpdated
        };
    }

    private static List<FirewallRule> DeserializeFirewallRules(string rulesJson)
    {
        try
        {
            var jsonRules = JsonSerializer.Deserialize<JsonElement[]>(rulesJson);
            if (jsonRules is null) return [];

            return jsonRules.Select(r => new FirewallRule
            {
                Name = r.GetProperty("Name").GetString() ?? string.Empty,
                Description = r.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                ApplicationPath = r.TryGetProperty("ApplicationPath", out var app) ? app.GetString() : null,
                Action = (FirewallAction)(r.TryGetProperty("Action", out var act) ? act.GetInt32() : 0),
                Direction = (FirewallDirection)(r.TryGetProperty("Direction", out var dir) ? dir.GetInt32() : 0),
                Protocol = (FirewallProtocol)(r.TryGetProperty("Protocol", out var proto) ? proto.GetInt32() : 0),
                LocalPorts = r.TryGetProperty("LocalPorts", out var lp) ? lp.GetString() : null,
                RemotePorts = r.TryGetProperty("RemotePorts", out var rp) ? rp.GetString() : null,
                RemoteAddresses = r.TryGetProperty("RemoteAddresses", out var ra) ? ra.GetString() : null,
                Enabled = r.TryGetProperty("Enabled", out var en) && en.GetBoolean(),
                GroupName = r.TryGetProperty("GroupName", out var gn) ? gn.GetString() : null
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Encrypts quarantined file data using AES-256-CBC with a machine-specific key.
    /// The key is derived from a master secret protected by DPAPI (machine scope),
    /// ensuring quarantined files cannot be restored on a different machine.
    /// Format: [16-byte IV][encrypted data]
    /// </summary>
    private static byte[] EncryptQuarantineData(byte[] data)
    {
        byte[] key = GetOrCreateQuarantineKey();
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Prepend IV to ciphertext
        byte[] result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return result;
    }

    /// <summary>
    /// Decrypts quarantined file data previously encrypted with <see cref="EncryptQuarantineData"/>.
    /// </summary>
    private static byte[] DecryptQuarantineData(byte[] encryptedData)
    {
        byte[] key = GetOrCreateQuarantineKey();
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Extract IV from first 16 bytes
        byte[] iv = new byte[16];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 16, encryptedData.Length - 16);
    }

    /// <summary>
    /// Gets or creates a 256-bit AES key for quarantine encryption.
    /// The key is stored in a file protected by DPAPI (machine scope on Windows)
    /// or generated per-installation on non-Windows platforms.
    /// </summary>
    private static byte[] GetOrCreateQuarantineKey()
    {
        string keyDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(keyDir);
        string keyFile = Path.Combine(keyDir, ".quarantine.key");

        if (File.Exists(keyFile))
        {
            byte[] protectedKey = File.ReadAllBytes(keyFile);
            try
            {
                // Unprotect using DPAPI (machine scope) - Windows only
                if (OperatingSystem.IsWindows())
                    return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.LocalMachine);
            }
            catch { }

            // Fallback: use raw bytes as key
            if (protectedKey.Length >= 32)
                return protectedKey[..32];
        }

        // Generate a new 256-bit key
        byte[] newKey = RandomNumberGenerator.GetBytes(32);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Protect using DPAPI (machine scope) and save
                byte[] protectedNewKey = ProtectedData.Protect(newKey, null, DataProtectionScope.LocalMachine);
                File.WriteAllBytes(keyFile, protectedNewKey);
            }
            else
            {
                File.WriteAllBytes(keyFile, newKey);
            }
        }
        catch
        {
            // Fallback: store raw
            File.WriteAllBytes(keyFile, newKey);
        }

        return newKey;
    }

    #endregion
}
