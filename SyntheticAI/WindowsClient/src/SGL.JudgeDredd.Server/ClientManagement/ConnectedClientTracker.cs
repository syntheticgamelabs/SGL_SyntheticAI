using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ClientManagement;

/// <summary>
/// Tracks connected clients, their registration data, and heartbeat timestamps.
/// Uses a ConcurrentDictionary for thread-safe access from API endpoints.
/// JWT handles authentication now; API key generation is removed.
/// </summary>
public sealed class ConnectedClientTracker
{
    private readonly ConcurrentDictionary<Guid, ConnectedClient> _clients = new();
    private readonly ConcurrentDictionary<string, string> _apiKeys = new(); // apiKey -> clientId mapping (legacy)
    private readonly ConcurrentDictionary<string, string> _userPasswords = new(); // username -> passwordHash

    /// <summary>
    /// Register a new client. JWT handles authentication, so no API key is generated.
    /// Returns the assigned client ID.
    /// </summary>
    public Guid RegisterClient(string username, string machineName,
        string clientVersion, string platform, string password = "",
        string deviceModel = "", string osVersion = "")
    {
        var clientId = Guid.NewGuid();

        // Store hashed password for login validation
        if (!string.IsNullOrEmpty(password))
        {
            var passwordHash = HashPassword(password);
            _userPasswords[username.ToLowerInvariant()] = passwordHash;
        }

        var client = new ConnectedClient
        {
            ClientId = clientId,
            Username = username,
            MachineName = machineName,
            ClientVersion = clientVersion,
            Platform = platform,
            DeviceModel = deviceModel,
            OsVersion = osVersion,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            ApiKey = string.Empty, // JWT handles auth now
            IsOnline = true
        };

        _clients[clientId] = client;

        SglLogger.Information("Client registered: {Username}@{Machine} (ID: {ClientId})",
            username, machineName, clientId);

        return clientId;
    }

    /// <summary>
    /// Validate login credentials and return client ID if successful.
    /// JWT token generation is handled by the caller (ClientEndpoints).
    /// </summary>
    public (Guid clientId, string machineName)? ValidateLogin(string username, string password)
    {
        var userKey = username.ToLowerInvariant();

        // Check if user exists and password matches
        if (!_userPasswords.TryGetValue(userKey, out var storedHash))
            return null;

        if (!VerifyPassword(password, storedHash))
            return null;

        // Find the existing client for this username
        var existingClient = _clients.Values.FirstOrDefault(c =>
            c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (existingClient != null)
        {
            existingClient.LastHeartbeat = DateTime.UtcNow;
            existingClient.IsOnline = true;

            SglLogger.Information("Client login successful: {Username} (ID: {ClientId})",
                username, existingClient.ClientId);

            return (existingClient.ClientId, existingClient.MachineName);
        }

        // User has a password stored but no active client session
        return null;
    }

    /// <summary>
    /// Look up a client by username.
    /// </summary>
    public ConnectedClient? GetClientByUsername(string username)
    {
        return _clients.Values.FirstOrDefault(c =>
            c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Look up a client by ID.
    /// </summary>
    public ConnectedClient? GetClient(Guid clientId)
    {
        _clients.TryGetValue(clientId, out var client);
        return client;
    }

    /// <summary>
    /// Check if a username already exists (for registration validation).
    /// </summary>
    public bool UsernameExists(string username)
    {
        return _userPasswords.ContainsKey(username.ToLowerInvariant());
    }

    /// <summary>
    /// Update heartbeat timestamp and stats for a client.
    /// </summary>
    public bool UpdateHeartbeat(Guid clientId, string signatureVersion, bool realTimeActive,
        int threatsDetected, int filesScanned, bool llmLoaded, double uptimeMinutes)
    {
        if (!_clients.TryGetValue(clientId, out var client))
            return false;

        client.LastHeartbeat = DateTime.UtcNow;
        client.SignatureVersion = signatureVersion;
        client.RealTimeProtectionActive = realTimeActive;
        client.ThreatsDetected = threatsDetected;
        client.FilesScanned = filesScanned;
        client.LlmModelLoaded = llmLoaded;
        client.UptimeMinutes = uptimeMinutes;
        client.IsOnline = true;

        return true;
    }

    /// <summary>
    /// Validate an API key and return the associated client ID.
    /// Kept for backward compatibility with clients that have not yet upgraded to JWT.
    /// </summary>
    public Guid? ValidateApiKey(string apiKey)
    {
        if (_apiKeys.TryGetValue(apiKey, out var clientIdStr) && Guid.TryParse(clientIdStr, out var clientId))
        {
            if (_clients.ContainsKey(clientId))
                return clientId;
        }
        return null;
    }

    /// <summary>
    /// Get all connected clients.
    /// </summary>
    public IReadOnlyList<ConnectedClient> GetAllClients()
    {
        return _clients.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get clients that have sent a heartbeat within the last N minutes.
    /// </summary>
    public IReadOnlyList<ConnectedClient> GetOnlineClients(int withinMinutes = 10)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-withinMinutes);
        return _clients.Values
            .Where(c => c.LastHeartbeat >= cutoff)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Load previously registered clients from disk so they survive server restarts.
    /// Reads data/clients/{guid}/registration.json for each saved client.
    /// </summary>
    public async Task LoadFromDiskAsync(ClientDataStore dataStore)
    {
        var clientIds = dataStore.GetRegisteredClientIds();
        SglLogger.Information("LoadFromDisk: Found {Count} saved client(s) on disk.", clientIds.Count);

        foreach (var clientId in clientIds)
        {
            try
            {
                var regPath = Path.Combine(AppContext.BaseDirectory, "data", "clients",
                    clientId.ToString(), "registration.json");

                if (!File.Exists(regPath))
                    continue;

                var json = await File.ReadAllTextAsync(regPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var username = root.TryGetProperty("Username", out var uProp) ? uProp.GetString() ?? "" : "";
                var machineName = root.TryGetProperty("MachineName", out var mProp) ? mProp.GetString() ?? "" : "";
                var clientVersion = root.TryGetProperty("ClientVersion", out var vProp) ? vProp.GetString() ?? "" : "";
                var platform = root.TryGetProperty("Platform", out var pProp) ? pProp.GetString() ?? "" : "";
                var deviceModel = root.TryGetProperty("DeviceModel", out var dProp) ? dProp.GetString() ?? "" : "";
                var osVersion = root.TryGetProperty("OsVersion", out var oProp) ? oProp.GetString() ?? "" : "";
                var registeredAt = root.TryGetProperty("RegisteredAt", out var rProp) && rProp.TryGetDateTime(out var dt)
                    ? dt : DateTime.UtcNow;

                // Reconstruct the client entry
                var client = new ConnectedClient
                {
                    ClientId = clientId,
                    Username = username,
                    MachineName = machineName,
                    ClientVersion = clientVersion,
                    Platform = platform,
                    DeviceModel = deviceModel,
                    OsVersion = osVersion,
                    RegisteredAt = registeredAt,
                    LastHeartbeat = registeredAt,
                    ApiKey = string.Empty,
                    IsOnline = false // Will become true on next heartbeat
                };

                _clients[clientId] = client;

                SglLogger.Information("LoadFromDisk: Restored client {Username}@{Machine} (ID: {ClientId})",
                    username, machineName, clientId);
            }
            catch (Exception ex)
            {
                SglLogger.Error($"LoadFromDisk: Failed to load client {clientId}: {ex.Message}", ex);
            }
        }

        // Load persisted password hashes so login works after restart
        var passwords = await dataStore.LoadPasswordHashesAsync();
        foreach (var kvp in passwords)
        {
            _userPasswords[kvp.Key] = kvp.Value;
        }
        SglLogger.Information("LoadFromDisk: Loaded {Count} persisted password hash(es).", passwords.Count);
    }

    /// <summary>
    /// Mark clients as offline if they haven't sent a heartbeat recently.
    /// </summary>
    public void PruneStaleClients(int staleMinutes = 15)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-staleMinutes);
        foreach (var client in _clients.Values)
        {
            if (client.LastHeartbeat < cutoff)
                client.IsOnline = false;
        }
    }

    public string? GetPasswordHash(string username)
    {
        _userPasswords.TryGetValue(username.ToLowerInvariant(), out var hash);
        return hash;
    }

    public void SetPasswordHash(string username, string passwordHash)
    {
        _userPasswords[username.ToLowerInvariant()] = passwordHash;
    }

    public int OnlineCount => _clients.Values.Count(c => c.IsOnline);
    public int TotalCount => _clients.Count;

    private static string HashPassword(string password)
    {
        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

/// <summary>
/// Represents a connected client instance.
/// </summary>
public class ConnectedClient
{
    public Guid ClientId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public string SignatureVersion { get; set; } = string.Empty;
    public bool RealTimeProtectionActive { get; set; }
    public int ThreatsDetected { get; set; }
    public int FilesScanned { get; set; }
    public bool LlmModelLoaded { get; set; }
    public double UptimeMinutes { get; set; }
}
