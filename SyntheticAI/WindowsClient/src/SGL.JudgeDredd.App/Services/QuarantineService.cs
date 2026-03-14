using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.JudgeDredd.App.Services;

/// <summary>
/// Represents a file that has been moved to the quarantine vault.
/// </summary>
public class QuarantinedItem
{
    public string Id { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string QuarantinePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ThreatName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public DateTime QuarantinedAt { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

/// <summary>
/// Manages a quarantine vault directory where detected threats are isolated.
/// Files are moved to <c>{LocalAppData}/SGL-JudgeDredd/Quarantine/</c>, renamed with a GUID,
/// and accompanied by a JSON metadata sidecar file. An ignore list is persisted so that
/// the user can suppress repeated detections for trusted files.
/// </summary>
public class QuarantineService
{
    private readonly string _quarantineDir;
    private readonly string _ignoreListPath;
    private readonly object _ignoreLock = new();
    private HashSet<string> _ignoredFiles;

    public QuarantineService()
    {
        _quarantineDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGL-JudgeDredd", "Quarantine");

        _ignoreListPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGL-JudgeDredd", "quarantine_ignore.json");

        Directory.CreateDirectory(_quarantineDir);
        _ignoredFiles = LoadIgnoreList();
    }

    /// <summary>
    /// Moves a file into the quarantine vault, renames it with a GUID, and writes a
    /// JSON metadata sidecar containing the original path, SHA-256 hash, timestamp,
    /// and threat information.
    /// </summary>
    /// <param name="filePath">Absolute path of the file to quarantine.</param>
    /// <param name="threatName">Name / classification of the detected threat.</param>
    /// <param name="severity">Severity level string (e.g. "Low", "Medium", "High", "Critical").</param>
    /// <returns>The <see cref="QuarantinedItem"/> describing the quarantined file.</returns>
    public async Task<QuarantinedItem> QuarantineAsync(string filePath, string threatName = "Malware.Generic", string severity = "High")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path must not be empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The file to quarantine was not found.", filePath);

        var id = Guid.NewGuid().ToString("N");
        var quarantinePath = Path.Combine(_quarantineDir, $"{id}.qvault");
        var metadataPath = Path.Combine(_quarantineDir, $"{id}.json");

        // Compute SHA-256 hash before moving
        var fileHash = await ComputeSha256Async(filePath);
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var fileName = fileInfo.Name;

        // Read the file contents and write to quarantine location
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        await File.WriteAllBytesAsync(quarantinePath, fileBytes);

        // Build metadata
        var item = new QuarantinedItem
        {
            Id = id,
            OriginalPath = filePath,
            QuarantinePath = quarantinePath,
            FileName = fileName,
            ThreatName = threatName,
            Severity = severity,
            QuarantinedAt = DateTime.UtcNow,
            FileHash = fileHash,
            FileSize = fileSize
        };

        // Write metadata sidecar
        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json);

        // Delete the original file
        File.Delete(filePath);

        return item;
    }

    /// <summary>
    /// Restores a quarantined file back to its original location.
    /// </summary>
    /// <param name="quarantineId">The GUID-based identifier of the quarantined item.</param>
    /// <returns><c>true</c> if the file was restored successfully; otherwise <c>false</c>.</returns>
    public async Task<bool> RestoreAsync(string quarantineId)
    {
        if (string.IsNullOrWhiteSpace(quarantineId))
            return false;

        var metadataPath = Path.Combine(_quarantineDir, $"{quarantineId}.json");
        if (!File.Exists(metadataPath))
            return false;

        var json = await File.ReadAllTextAsync(metadataPath);
        var item = JsonSerializer.Deserialize<QuarantinedItem>(json);
        if (item == null)
            return false;

        if (!File.Exists(item.QuarantinePath))
            return false;

        // Ensure the original directory exists
        var originalDir = Path.GetDirectoryName(item.OriginalPath);
        if (!string.IsNullOrEmpty(originalDir))
            Directory.CreateDirectory(originalDir);

        // Restore the file
        var fileBytes = await File.ReadAllBytesAsync(item.QuarantinePath);
        await File.WriteAllBytesAsync(item.OriginalPath, fileBytes);

        // Clean up quarantine files
        File.Delete(item.QuarantinePath);
        File.Delete(metadataPath);

        return true;
    }

    /// <summary>
    /// Permanently deletes a quarantined file and its metadata from the vault.
    /// </summary>
    /// <param name="quarantineId">The GUID-based identifier of the quarantined item.</param>
    /// <returns><c>true</c> if the files were deleted; otherwise <c>false</c>.</returns>
    public Task<bool> DeletePermanentlyAsync(string quarantineId)
    {
        if (string.IsNullOrWhiteSpace(quarantineId))
            return Task.FromResult(false);

        var quarantinePath = Path.Combine(_quarantineDir, $"{quarantineId}.qvault");
        var metadataPath = Path.Combine(_quarantineDir, $"{quarantineId}.json");

        var deleted = false;

        if (File.Exists(quarantinePath))
        {
            File.Delete(quarantinePath);
            deleted = true;
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
            deleted = true;
        }

        return Task.FromResult(deleted);
    }

    /// <summary>
    /// Returns all items currently held in the quarantine vault by reading their
    /// JSON metadata sidecar files.
    /// </summary>
    public List<QuarantinedItem> GetQuarantinedItems()
    {
        var items = new List<QuarantinedItem>();

        try
        {
            var metaFiles = Directory.GetFiles(_quarantineDir, "*.json");
            foreach (var metaFile in metaFiles)
            {
                try
                {
                    // Skip the ignore list file
                    if (Path.GetFileName(metaFile).Equals("quarantine_ignore.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var json = File.ReadAllText(metaFile);
                    var item = JsonSerializer.Deserialize<QuarantinedItem>(json);
                    if (item != null && File.Exists(item.QuarantinePath))
                    {
                        items.Add(item);
                    }
                }
                catch
                {
                    // Skip corrupt metadata files
                }
            }
        }
        catch
        {
            // Vault directory may not exist yet
        }

        return items.OrderByDescending(i => i.QuarantinedAt).ToList();
    }

    /// <summary>
    /// Adds a file path to the ignore list so future scans skip it.
    /// The ignore list is persisted as a JSON array.
    /// </summary>
    /// <param name="filePath">Absolute path of the file to ignore.</param>
    public async Task IgnoreThreatAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        lock (_ignoreLock)
        {
            _ignoredFiles.Add(filePath);
        }

        await SaveIgnoreListAsync();
    }

    /// <summary>
    /// Checks whether a file path is on the ignore list.
    /// </summary>
    /// <param name="filePath">Absolute path to check.</param>
    /// <returns><c>true</c> if the file is ignored; otherwise <c>false</c>.</returns>
    public bool IsIgnored(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        lock (_ignoreLock)
        {
            return _ignoredFiles.Contains(filePath);
        }
    }

    /// <summary>
    /// Removes a file path from the ignore list.
    /// </summary>
    public async Task RemoveFromIgnoreListAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        lock (_ignoreLock)
        {
            _ignoredFiles.Remove(filePath);
        }

        await SaveIgnoreListAsync();
    }

    /// <summary>
    /// Returns all currently-ignored file paths.
    /// </summary>
    public IReadOnlyList<string> GetIgnoredFiles()
    {
        lock (_ignoreLock)
        {
            return _ignoredFiles.ToList().AsReadOnly();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private HashSet<string> LoadIgnoreList()
    {
        try
        {
            if (File.Exists(_ignoreListPath))
            {
                var json = File.ReadAllText(_ignoreListPath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                    return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Non-critical: start with empty ignore list on failure
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveIgnoreListAsync()
    {
        try
        {
            List<string> snapshot;
            lock (_ignoreLock)
            {
                snapshot = _ignoredFiles.ToList();
            }

            var dir = Path.GetDirectoryName(_ignoreListPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_ignoreListPath, json);
        }
        catch
        {
            // Non-critical: ignore list save failure is not fatal
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
