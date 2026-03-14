using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services;

/// <summary>
/// Dark web / breach detection service. Checks emails and passwords against
/// the HaveIBeenPwned API (v3) and maintains a local watchlist of monitored items.
/// Uses k-anonymity for password checks (only sends SHA-1 prefix, never the full hash).
/// Falls back to a local known-breach database when the API is unavailable.
/// </summary>
public class BreachDetectionService : IBreachDetectionService
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string WatchlistPath = Path.Combine(DataDir, "breach_watchlist.json");
    private static readonly string CachePath = Path.Combine(DataDir, "breach_cache.json");

    private readonly HttpClient _httpClient;
    private List<MonitoredItem> _watchlist = new();
    private readonly Dictionary<string, BreachCheckResult> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    // Known major breaches (local database for offline checks)
    private static readonly Dictionary<string, KnownBreach> KnownBreaches = new(StringComparer.OrdinalIgnoreCase)
    {
        ["adobe.com"] = new("Adobe", "adobe.com", new DateTime(2013, 10, 4), 152_445_165,
            "Email addresses, password hints, passwords, usernames",
            "In October 2013, 153 million Adobe accounts were breached with each containing an internal ID, username, email, encrypted password and a password hint in plain text."),
        ["linkedin.com"] = new("LinkedIn", "linkedin.com", new DateTime(2012, 5, 5), 164_611_595,
            "Email addresses, passwords",
            "In May 2016, LinkedIn had 164 million email addresses and passwords exposed from a 2012 breach. Originally only 6.5 million passwords were disclosed but the full breach was much larger."),
        ["dropbox.com"] = new("Dropbox", "dropbox.com", new DateTime(2012, 7, 1), 68_648_009,
            "Email addresses, passwords",
            "In mid-2012, Dropbox suffered a data breach which exposed 68 million unique email addresses and bcrypt hashes of passwords."),
        ["myspace.com"] = new("MySpace", "myspace.com", new DateTime(2008, 7, 1), 359_420_698,
            "Email addresses, passwords, usernames",
            "In approximately 2008, MySpace suffered a data breach that exposed almost 360 million accounts."),
        ["yahoo.com"] = new("Yahoo", "yahoo.com", new DateTime(2013, 8, 1), 3_000_000_000,
            "Dates of birth, email addresses, names, passwords, phone numbers, security questions",
            "In 2013, Yahoo was hacked and 3 billion accounts were compromised in the largest breach in history."),
        ["canva.com"] = new("Canva", "canva.com", new DateTime(2019, 5, 24), 137_272_116,
            "Email addresses, geographic locations, names, passwords, usernames",
            "In May 2019, the graphic design tool website Canva suffered a data breach that impacted 137 million users."),
        ["facebook.com"] = new("Facebook", "facebook.com", new DateTime(2019, 4, 1), 509_458_528,
            "Dates of birth, email addresses, employers, geographic locations, names, phone numbers",
            "In April 2021, a large data set of over 500 million Facebook users was made freely available for download."),
        ["twitter.com"] = new("Twitter", "twitter.com", new DateTime(2022, 1, 1), 211_524_284,
            "Bio, email addresses, names, profile photos, usernames",
            "In early 2023, over 200 million records scraped from Twitter were published on a popular hacking forum."),
    };

    public BreachDetectionService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SGL-SyntheticAI-BreachMonitor");
        // HIBP API requires a user-agent

        LoadWatchlist();
        LoadCache();
    }

    public async Task<BreachCheckResult> CheckEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return new BreachCheckResult { Query = email };

        // Check cache first
        var cacheKey = $"email:{email.ToLowerInvariant()}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) &&
                (DateTime.UtcNow - cached.CheckedAt).TotalHours < 24)
                return cached;
        }

        var result = new BreachCheckResult { Query = email };

        // Check against local known breach database
        var domain = email.Contains('@') ? email.Split('@')[1].ToLowerInvariant() : email.ToLowerInvariant();
        foreach (var kvp in KnownBreaches)
        {
            if (domain.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBreached = true;
                result.BreachCount++;
                result.Breaches.Add(new BreachInfo
                {
                    Name = kvp.Value.Name,
                    Domain = kvp.Value.Domain,
                    BreachDate = kvp.Value.Date,
                    PwnCount = kvp.Value.PwnCount,
                    Description = kvp.Value.Description,
                    DataClasses = kvp.Value.DataClasses.Split(", ").ToList()
                });
            }
        }

        // Try HIBP API (free tier — breachedaccount endpoint)
        try
        {
            var encoded = Uri.EscapeDataString(email.Trim().ToLowerInvariant());
            var response = await _httpClient.GetAsync(
                $"https://haveibeenpwned.com/api/v3/breachedaccount/{encoded}?truncateResponse=false", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var breaches = JsonSerializer.Deserialize<List<HibpBreach>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (breaches != null)
                {
                    result.IsBreached = true;
                    result.BreachCount = breaches.Count;
                    result.Breaches.Clear();
                    foreach (var b in breaches)
                    {
                        result.Breaches.Add(new BreachInfo
                        {
                            Name = b.Name ?? "",
                            Domain = b.Domain ?? "",
                            BreachDate = b.BreachDate,
                            PwnCount = b.PwnCount,
                            Description = b.Description ?? "",
                            DataClasses = b.DataClasses ?? new List<string>()
                        });
                    }
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Not in any breach (if local DB also found nothing)
                if (!result.IsBreached)
                {
                    result.IsBreached = false;
                    result.BreachCount = 0;
                }
            }
            // 401/403 = API key required for this endpoint; rely on local DB results
        }
        catch (Exception ex)
        {
            SglLogger.Warning("HIBP API check failed for email (using local DB): {Error}", ex.Message);
            // Local DB results remain
        }

        // Cache the result
        lock (_lock)
        {
            _cache[cacheKey] = result;
            SaveCache();
        }

        // Update watchlist if this email is monitored
        UpdateWatchlistItem(email, result);

        return result;
    }

    public async Task<BreachCheckResult> CheckDomainAsync(string domain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return new BreachCheckResult { Query = domain };

        var cacheKey = $"domain:{domain.ToLowerInvariant()}";
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) &&
                (DateTime.UtcNow - cached.CheckedAt).TotalHours < 24)
                return cached;
        }

        var result = new BreachCheckResult { Query = domain };

        // Check local known breaches
        foreach (var kvp in KnownBreaches)
        {
            if (kvp.Key.Contains(domain, StringComparison.OrdinalIgnoreCase) ||
                domain.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBreached = true;
                result.BreachCount++;
                result.Breaches.Add(new BreachInfo
                {
                    Name = kvp.Value.Name,
                    Domain = kvp.Value.Domain,
                    BreachDate = kvp.Value.Date,
                    PwnCount = kvp.Value.PwnCount,
                    Description = kvp.Value.Description,
                    DataClasses = kvp.Value.DataClasses.Split(", ").ToList()
                });
            }
        }

        // Try HIBP domain search
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://haveibeenpwned.com/api/v3/breaches?domain={Uri.EscapeDataString(domain)}", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var breaches = JsonSerializer.Deserialize<List<HibpBreach>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (breaches != null && breaches.Count > 0)
                {
                    result.IsBreached = true;
                    result.BreachCount = breaches.Count;
                    result.Breaches.Clear();
                    foreach (var b in breaches)
                    {
                        result.Breaches.Add(new BreachInfo
                        {
                            Name = b.Name ?? "",
                            Domain = b.Domain ?? "",
                            BreachDate = b.BreachDate,
                            PwnCount = b.PwnCount,
                            Description = b.Description ?? "",
                            DataClasses = b.DataClasses ?? new List<string>()
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("HIBP domain check failed (using local DB): {Error}", ex.Message);
        }

        lock (_lock)
        {
            _cache[cacheKey] = result;
            SaveCache();
        }

        UpdateWatchlistItem(domain, result);
        return result;
    }

    /// <summary>
    /// Checks if a password has been exposed using the HIBP Pwned Passwords API.
    /// Uses k-anonymity: only the first 5 characters of the SHA-1 hash are sent.
    /// The full password or hash never leaves the device.
    /// </summary>
    public async Task<PasswordCheckResult> CheckPasswordAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new PasswordCheckResult();

        var result = new PasswordCheckResult();

        try
        {
            // Compute SHA-1 hash of the password
            var sha1Bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var sha1Hex = BitConverter.ToString(sha1Bytes).Replace("-", "").ToLowerInvariant();
            var prefix = sha1Hex[..5];
            var suffix = sha1Hex[5..].ToUpperInvariant();

            // Query HIBP with only the prefix (k-anonymity)
            var response = await _httpClient.GetAsync(
                $"https://api.pwnedpasswords.com/range/{prefix}", ct);

            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Trim().Split(':');
                    if (parts.Length == 2 && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsExposed = true;
                        result.ExposureCount = int.TryParse(parts[1], out var count) ? count : 1;
                        result.Severity = result.ExposureCount switch
                        {
                            > 100_000 => "critical",
                            > 10_000 => "high",
                            > 1_000 => "medium",
                            > 0 => "low",
                            _ => "safe"
                        };
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Password breach check failed: {Error}", ex.Message);
        }

        return result;
    }

    public List<MonitoredItem> GetMonitoredItems()
    {
        lock (_lock) return new List<MonitoredItem>(_watchlist);
    }

    public void AddMonitoredItem(string value, string type)
    {
        lock (_lock)
        {
            if (_watchlist.Any(w => w.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
                return;

            _watchlist.Add(new MonitoredItem
            {
                Value = value,
                Type = type,
                AddedAt = DateTime.UtcNow
            });
            SaveWatchlist();
        }
    }

    public void RemoveMonitoredItem(string value)
    {
        lock (_lock)
        {
            _watchlist.RemoveAll(w => w.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            SaveWatchlist();
        }
    }

    private void UpdateWatchlistItem(string value, BreachCheckResult result)
    {
        lock (_lock)
        {
            var item = _watchlist.FirstOrDefault(w =>
                w.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsBreached = result.IsBreached;
                item.BreachCount = result.BreachCount;
                item.LastChecked = DateTime.UtcNow;
                SaveWatchlist();
            }
        }
    }

    private void LoadWatchlist()
    {
        try
        {
            if (File.Exists(WatchlistPath))
            {
                var json = File.ReadAllText(WatchlistPath);
                _watchlist = JsonSerializer.Deserialize<List<MonitoredItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to load breach watchlist: {Error}", ex.Message);
        }
    }

    private void SaveWatchlist()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(WatchlistPath,
                JsonSerializer.Serialize(_watchlist, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var json = File.ReadAllText(CachePath);
                var cached = JsonSerializer.Deserialize<Dictionary<string, BreachCheckResult>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cached != null)
                {
                    foreach (var kvp in cached)
                        _cache[kvp.Key] = kvp.Value;
                }
            }
        }
        catch { }
    }

    private void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(CachePath,
                JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // HIBP API response model
    private class HibpBreach
    {
        public string? Name { get; set; }
        public string? Domain { get; set; }
        public DateTime BreachDate { get; set; }
        public long PwnCount { get; set; }
        public string? Description { get; set; }
        public List<string>? DataClasses { get; set; }
    }

    private record KnownBreach(string Name, string Domain, DateTime Date, long PwnCount, string DataClasses, string Description);
}
