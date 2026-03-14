using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ClientManagement;

/// <summary>
/// Persists client data, logs, threat reports, and crash logs to the file system.
/// Structure: data/clients/{clientId}/registration.json, logs/, threat-reports/, crash-logs/
/// </summary>
public sealed class ClientDataStore
{
    private readonly string _baseDir;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ClientDataStore(string? baseDirectory = null)
    {
        _baseDir = baseDirectory ?? Path.Combine(AppContext.BaseDirectory, "data", "clients");
        Directory.CreateDirectory(_baseDir);
    }

    /// <summary>
    /// Save client registration data to disk.
    /// </summary>
    public async Task SaveRegistrationAsync(Guid clientId, string username, string machineName,
        string clientVersion, string platform, string apiKey,
        string deviceModel = "", string osVersion = "")
    {
        var clientDir = GetClientDir(clientId);
        Directory.CreateDirectory(clientDir);

        var registration = new
        {
            ClientId = clientId,
            Username = username,
            MachineName = machineName,
            ClientVersion = clientVersion,
            Platform = platform,
            DeviceModel = deviceModel,
            OsVersion = osVersion,
            ApiKey = apiKey,
            RegisteredAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(registration, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(clientDir, "registration.json"), json);

        SglLogger.Information("Client registration saved: {ClientId}", clientId);
    }

    /// <summary>
    /// Save an uploaded log file from a client.
    /// </summary>
    public async Task SaveLogAsync(Guid clientId, string logType, string content)
    {
        var logDir = Path.Combine(GetClientDir(clientId), "logs");
        Directory.CreateDirectory(logDir);

        var fileName = $"{logType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
        await File.WriteAllTextAsync(Path.Combine(logDir, fileName), content);
    }

    /// <summary>
    /// Save a threat report from a client.
    /// </summary>
    public async Task SaveThreatReportAsync(Guid clientId, string fileName, string sha256,
        string threatName, double confidence)
    {
        var reportDir = Path.Combine(GetClientDir(clientId), "threat-reports");
        Directory.CreateDirectory(reportDir);

        var report = new
        {
            FileName = fileName,
            Sha256 = sha256,
            ThreatName = threatName,
            Confidence = confidence,
            DetectedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);
        var reportFile = $"threat_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(fileName)}.json";
        await File.WriteAllTextAsync(Path.Combine(reportDir, reportFile), json);
    }

    /// <summary>
    /// Save a crash log from a client.
    /// </summary>
    public async Task SaveCrashLogAsync(Guid clientId, string content)
    {
        var crashDir = Path.Combine(GetClientDir(clientId), "crash-logs");
        Directory.CreateDirectory(crashDir);

        var fileName = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
        await File.WriteAllTextAsync(Path.Combine(crashDir, fileName), content);
    }

    /// <summary>
    /// Get the list of registered client IDs from disk.
    /// </summary>
    public IReadOnlyList<Guid> GetRegisteredClientIds()
    {
        var ids = new List<Guid>();
        if (!Directory.Exists(_baseDir)) return ids;

        foreach (var dir in Directory.GetDirectories(_baseDir))
        {
            var dirName = Path.GetFileName(dir);
            if (Guid.TryParse(dirName, out var id))
                ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    /// Save a username -> passwordHash entry to disk so logins survive server restarts.
    /// </summary>
    public async Task SavePasswordHashAsync(string username, string passwordHash)
    {
        var passwordsFile = Path.Combine(Path.GetDirectoryName(_baseDir)!, "passwords.json");
        Dictionary<string, string> passwords = new();

        if (File.Exists(passwordsFile))
        {
            var json = await File.ReadAllTextAsync(passwordsFile);
            passwords = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }

        passwords[username.ToLowerInvariant()] = passwordHash;
        var updatedJson = JsonSerializer.Serialize(passwords, JsonOptions);
        await File.WriteAllTextAsync(passwordsFile, updatedJson);
    }

    /// <summary>
    /// Load all persisted password hashes from disk.
    /// </summary>
    public async Task<Dictionary<string, string>> LoadPasswordHashesAsync()
    {
        var passwordsFile = Path.Combine(Path.GetDirectoryName(_baseDir)!, "passwords.json");
        if (!File.Exists(passwordsFile))
            return new Dictionary<string, string>();

        var json = await File.ReadAllTextAsync(passwordsFile);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
    }

    private string GetClientDir(Guid clientId)
    {
        return Path.Combine(_baseDir, clientId.ToString());
    }
}
