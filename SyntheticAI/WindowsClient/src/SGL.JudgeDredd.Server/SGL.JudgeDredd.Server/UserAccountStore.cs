using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

public class ServerUserAccount
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
}

public class UserAccountStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, ServerUserAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public UserAccountStore()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "user_accounts.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _accounts = JsonSerializer.Deserialize<Dictionary<string, ServerUserAccount>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                SglLogger.Information("Loaded {Count} user accounts", _accounts.Count);
            }
        }
        catch (Exception ex) { SglLogger.Error($"Failed to load accounts: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_accounts, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex) { SglLogger.Error($"Failed to save accounts: {ex.Message}"); }
    }

    public async Task<(bool success, string message, ServerUserAccount? account)> RegisterAsync(string username, string password, string machineName, string platform)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.ContainsKey(username))
                return (false, "Username already taken", null);

            var account = new ServerUserAccount
            {
                Username = username,
                PasswordHash = HashPassword(password),
                ClientId = Guid.NewGuid().ToString(),
                MachineName = machineName,
                Platform = platform,
                RegisteredAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _accounts[username] = account;
            await SaveAsync();
            SglLogger.Information("New account registered: {Username} from {Machine}", username, machineName);
            return (true, "Account created", account);
        }
        finally { _lock.Release(); }
    }

    public async Task<(bool success, string message, ServerUserAccount? account)> LoginAsync(string username, string password)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_accounts.TryGetValue(username, out var account))
                return (false, "Invalid username or password", null);

            if (account.IsBanned)
                return (false, "Account is banned", null);

            if (!VerifyPassword(password, account.PasswordHash))
                return (false, "Invalid username or password", null);

            account.LastLoginAt = DateTime.UtcNow;
            await SaveAsync();
            return (true, "Login successful", account);
        }
        finally { _lock.Release(); }
    }

    public bool IsUsernameTaken(string username) => _accounts.ContainsKey(username);

    public List<ServerUserAccount> GetAllAccounts() => _accounts.Values.ToList();

    public async Task<bool> BanUserAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.TryGetValue(username, out var account))
            {
                account.IsBanned = true;
                await SaveAsync();
                return true;
            }
            return false;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> UnbanUserAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.TryGetValue(username, out var account))
            {
                account.IsBanned = false;
                await SaveAsync();
                return true;
            }
            return false;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> RemoveUserAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.Remove(username))
            {
                await SaveAsync();
                return true;
            }
            return false;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> PromoteUserAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.TryGetValue(username, out var account))
            {
                account.IsAdmin = true;
                await SaveAsync();
                return true;
            }
            return false;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> DemoteUserAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            if (_accounts.TryGetValue(username, out var account))
            {
                account.IsAdmin = false;
                await SaveAsync();
                return true;
            }
            return false;
        }
        finally { _lock.Release(); }
    }

    private static string HashPassword(string password)
    {
        // SHA256 hash with random salt for password storage
        using var sha = System.Security.Cryptography.SHA256.Create();
        var salt = Guid.NewGuid().ToString("N")[..16];
        var hash = Convert.ToBase64String(sha.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(salt + password)));
        return $"{salt}:{hash}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToBase64String(sha.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(parts[0] + password)));
        return hash == parts[1];
    }
}
