using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services;

/// <summary>
/// Tracks website visitor metrics: unique visitors, page views, active sessions,
/// download counts by platform, and visitor countries. Persists to disk periodically.
/// Includes background GeoIP resolution for visitors without Cloudflare country headers.
/// </summary>
public class WebsiteMetricsService : IDisposable
{
    private readonly string _filePath;
    private readonly Timer _persistTimer;
    private readonly object _lock = new();

    // Unique visitor tracking (hashed IPs)
    private readonly HashSet<string> _uniqueVisitors = new();

    // Active sessions — key: hashed IP, value: last seen UTC
    private readonly ConcurrentDictionary<string, DateTime> _activeSessions = new();

    // Page view counts per path
    private readonly ConcurrentDictionary<string, long> _pageViews = new();

    // Download counts by platform
    private readonly ConcurrentDictionary<string, long> _downloads = new();

    // Country visitor counts (ISO 3166-1 alpha-2 codes)
    private readonly ConcurrentDictionary<string, long> _countries = new();

    // Session duration tracking — key: hashed IP, value: first seen UTC
    private readonly ConcurrentDictionary<string, DateTime> _sessionStarts = new();

    // GeoIP background resolution
    private readonly ConcurrentDictionary<string, string> _geoCache = new();
    private readonly ConcurrentQueue<string> _geoQueue = new();
    private readonly HttpClient _geoHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Timer _geoTimer;

    // Totals
    private long _totalPageViews;
    private long _totalDownloads;

    /// <summary>
    /// ISO country code to display-name and approximate centroid coordinates.
    /// Used to enrich the public metrics API response with lat/lng for the world map.
    /// </summary>
    private static readonly Dictionary<string, (string Name, double Lat, double Lng)> CountryInfo =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        {"US", ("United States", 39.8, -98.6)},
        {"CA", ("Canada", 56.1, -106.3)},
        {"MX", ("Mexico", 23.6, -102.6)},
        {"GT", ("Guatemala", 15.8, -90.2)},
        {"CU", ("Cuba", 21.5, -77.8)},
        {"DO", ("Dominican Republic", 18.7, -70.2)},
        {"CR", ("Costa Rica", 9.7, -83.8)},
        {"PA", ("Panama", 8.5, -80.8)},
        {"JM", ("Jamaica", 18.1, -77.3)},
        {"HN", ("Honduras", 15.2, -86.2)},
        {"SV", ("El Salvador", 13.8, -88.9)},
        {"NI", ("Nicaragua", 12.9, -85.2)},
        {"PR", ("Puerto Rico", 18.2, -66.6)},
        {"TT", ("Trinidad and Tobago", 10.7, -61.2)},
        // South America
        {"BR", ("Brazil", -14.2, -51.9)},
        {"AR", ("Argentina", -38.4, -63.6)},
        {"CO", ("Colombia", 4.6, -74.3)},
        {"CL", ("Chile", -35.7, -71.5)},
        {"PE", ("Peru", -9.2, -75.0)},
        {"VE", ("Venezuela", 6.4, -66.6)},
        {"EC", ("Ecuador", -1.8, -78.2)},
        {"BO", ("Bolivia", -16.3, -63.6)},
        {"UY", ("Uruguay", -32.5, -55.8)},
        {"PY", ("Paraguay", -23.4, -58.4)},
        // Europe
        {"GB", ("United Kingdom", 55.4, -3.4)},
        {"DE", ("Germany", 51.2, 10.4)},
        {"FR", ("France", 46.2, 2.2)},
        {"IT", ("Italy", 41.9, 12.6)},
        {"ES", ("Spain", 40.5, -3.7)},
        {"NL", ("Netherlands", 52.1, 5.3)},
        {"BE", ("Belgium", 50.5, 4.5)},
        {"PT", ("Portugal", 39.4, -8.2)},
        {"SE", ("Sweden", 60.1, 18.6)},
        {"NO", ("Norway", 60.5, 8.5)},
        {"DK", ("Denmark", 56.3, 9.5)},
        {"FI", ("Finland", 61.9, 25.7)},
        {"PL", ("Poland", 51.9, 19.1)},
        {"AT", ("Austria", 47.5, 14.6)},
        {"CH", ("Switzerland", 46.8, 8.2)},
        {"IE", ("Ireland", 53.1, -8.2)},
        {"CZ", ("Czech Republic", 49.8, 15.5)},
        {"RO", ("Romania", 45.9, 25.0)},
        {"HU", ("Hungary", 47.2, 19.5)},
        {"GR", ("Greece", 39.1, 21.8)},
        {"UA", ("Ukraine", 48.4, 31.2)},
        {"SK", ("Slovakia", 48.7, 19.7)},
        {"HR", ("Croatia", 45.1, 15.2)},
        {"BG", ("Bulgaria", 42.7, 25.5)},
        {"RS", ("Serbia", 44.0, 21.0)},
        {"LT", ("Lithuania", 55.2, 23.9)},
        {"LV", ("Latvia", 56.9, 24.1)},
        {"EE", ("Estonia", 58.6, 25.0)},
        {"SI", ("Slovenia", 46.2, 14.5)},
        {"IS", ("Iceland", 65.0, -19.0)},
        {"BY", ("Belarus", 53.7, 27.9)},
        {"MD", ("Moldova", 47.4, 28.4)},
        {"AL", ("Albania", 41.2, 20.2)},
        {"MK", ("North Macedonia", 41.5, 21.7)},
        {"BA", ("Bosnia and Herzegovina", 43.9, 17.7)},
        {"ME", ("Montenegro", 42.7, 19.4)},
        // Asia
        {"CN", ("China", 35.9, 104.2)},
        {"JP", ("Japan", 36.2, 138.3)},
        {"IN", ("India", 20.6, 79.0)},
        {"KR", ("South Korea", 35.9, 127.8)},
        {"ID", ("Indonesia", -0.8, 113.9)},
        {"TH", ("Thailand", 15.9, 100.9)},
        {"VN", ("Vietnam", 14.1, 108.3)},
        {"PH", ("Philippines", 12.9, 121.8)},
        {"MY", ("Malaysia", 4.2, 101.9)},
        {"SG", ("Singapore", 1.4, 103.8)},
        {"TW", ("Taiwan", 23.7, 121.0)},
        {"HK", ("Hong Kong", 22.4, 114.1)},
        {"PK", ("Pakistan", 30.4, 69.3)},
        {"BD", ("Bangladesh", 23.7, 90.4)},
        {"IL", ("Israel", 31.0, 34.9)},
        {"AE", ("UAE", 23.4, 53.8)},
        {"SA", ("Saudi Arabia", 23.9, 45.1)},
        {"TR", ("Turkey", 39.0, 35.2)},
        {"RU", ("Russia", 61.5, 105.3)},
        {"KZ", ("Kazakhstan", 48.0, 68.0)},
        {"IQ", ("Iraq", 33.2, 43.7)},
        {"IR", ("Iran", 32.4, 53.7)},
        {"QA", ("Qatar", 25.4, 51.2)},
        {"KW", ("Kuwait", 29.3, 47.5)},
        {"LB", ("Lebanon", 33.9, 35.9)},
        {"JO", ("Jordan", 30.6, 36.2)},
        {"MM", ("Myanmar", 21.9, 96.0)},
        {"NP", ("Nepal", 28.4, 84.1)},
        {"LK", ("Sri Lanka", 7.9, 80.8)},
        {"KH", ("Cambodia", 12.6, 105.0)},
        {"UZ", ("Uzbekistan", 41.4, 64.6)},
        {"GE", ("Georgia", 42.3, 43.4)},
        {"AM", ("Armenia", 40.1, 45.0)},
        {"AZ", ("Azerbaijan", 40.1, 47.6)},
        {"MN", ("Mongolia", 46.9, 103.8)},
        {"LA", ("Laos", 19.9, 102.5)},
        {"BN", ("Brunei", 4.9, 114.9)},
        // Africa
        {"ZA", ("South Africa", -30.6, 22.9)},
        {"NG", ("Nigeria", 9.1, 8.7)},
        {"EG", ("Egypt", 26.8, 30.8)},
        {"KE", ("Kenya", -0.02, 37.9)},
        {"GH", ("Ghana", 7.9, -1.0)},
        {"MA", ("Morocco", 31.8, -7.1)},
        {"TN", ("Tunisia", 33.9, 9.5)},
        {"ET", ("Ethiopia", 9.1, 40.5)},
        {"TZ", ("Tanzania", -6.4, 34.9)},
        {"DZ", ("Algeria", 28.0, 1.7)},
        {"CM", ("Cameroon", 7.4, 12.4)},
        {"CI", ("Ivory Coast", 7.5, -5.5)},
        {"SN", ("Senegal", 14.5, -14.5)},
        {"UG", ("Uganda", 1.4, 32.3)},
        {"AO", ("Angola", -11.2, 17.9)},
        {"MZ", ("Mozambique", -18.7, 35.5)},
        {"MG", ("Madagascar", -18.8, 46.9)},
        {"RW", ("Rwanda", -1.9, 29.9)},
        {"ZW", ("Zimbabwe", -19.0, 29.2)},
        {"LY", ("Libya", 26.3, 17.2)},
        {"SD", ("Sudan", 12.9, 30.2)},
        {"CD", ("DR Congo", -4.0, 21.8)},
        {"ML", ("Mali", 17.6, -4.0)},
        {"NE", ("Niger", 17.6, 8.1)},
        {"BF", ("Burkina Faso", 12.4, -1.6)},
        {"MW", ("Malawi", -13.3, 34.3)},
        {"ZM", ("Zambia", -13.1, 27.8)},
        {"BW", ("Botswana", -22.3, 24.7)},
        {"NA", ("Namibia", -22.6, 18.5)},
        // Oceania
        {"AU", ("Australia", -25.3, 133.8)},
        {"NZ", ("New Zealand", -40.9, 174.9)},
        {"FJ", ("Fiji", -17.7, 178.1)},
        {"PG", ("Papua New Guinea", -6.3, 143.9)},
    };

    public WebsiteMetricsService()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "website_metrics.json");

        LoadFromDisk();

        // Persist to disk every 60 seconds
        _persistTimer = new Timer(_ => SaveToDisk(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        // Process GeoIP lookups in the background (one every 2 seconds, well within ip-api.com free tier limits)
        _geoTimer = new Timer(ProcessGeoQueue, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Record a unique visitor from any HTTP request (API or page).
    /// Tracks unique visitors by hashed IP and resolves country via header or background GeoIP.
    /// </summary>
    public void RecordVisitor(string? remoteIp, string? country)
    {
        var ip = remoteIp ?? "unknown";
        var ipHash = HashIp(ip);

        bool isNewVisitor;
        lock (_lock)
        {
            isNewVisitor = _uniqueVisitors.Add(ipHash);
        }

        _activeSessions[ipHash] = DateTime.UtcNow;
        _sessionStarts.TryAdd(ipHash, DateTime.UtcNow);

        if (isNewVisitor)
        {
            if (!string.IsNullOrWhiteSpace(country) && country != "XX" && country != "T1")
            {
                _countries.AddOrUpdate(country, 1, (_, c) => c + 1);
                _geoCache.TryAdd(ip, country);
            }
            else if (_geoCache.TryGetValue(ip, out var cachedCountry))
            {
                _countries.AddOrUpdate(cachedCountry, 1, (_, c) => c + 1);
            }
            else if (ip != "unknown" && ip != "::1" && !ip.StartsWith("127.") && !ip.StartsWith("10."))
            {
                // Queue for background GeoIP lookup
                if (!_geoCache.ContainsKey(ip))
                    _geoQueue.Enqueue(ip);
            }
        }
    }

    /// <summary>
    /// Record a website page view (non-API request). Only tracks the page view count,
    /// not unique visitors (use RecordVisitor for that).
    /// </summary>
    public void RecordPageView(string path)
    {
        _pageViews.AddOrUpdate(path, 1, (_, count) => count + 1);
        Interlocked.Increment(ref _totalPageViews);
    }

    /// <summary>
    /// Record a file download (client, mobile, linux-client, copilot).
    /// </summary>
    public void RecordDownload(string platform)
    {
        _downloads.AddOrUpdate(platform, 1, (_, count) => count + 1);
        Interlocked.Increment(ref _totalDownloads);
    }

    /// <summary>
    /// Background GeoIP resolver. Dequeues one IP at a time and resolves it via ip-api.com.
    /// Rate limited to ~30/minute to stay within the free tier (45/minute max).
    /// </summary>
    private async void ProcessGeoQueue(object? state)
    {
        if (!_geoQueue.TryDequeue(out var ip)) return;
        if (_geoCache.ContainsKey(ip)) return;

        try
        {
            var response = await _geoHttp.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,countryCode");
            var json = JsonSerializer.Deserialize<JsonElement>(response);

            if (json.TryGetProperty("status", out var status) && status.GetString() == "success" &&
                json.TryGetProperty("countryCode", out var codeEl))
            {
                var countryCode = codeEl.GetString();
                if (!string.IsNullOrWhiteSpace(countryCode) && countryCode != "XX")
                {
                    _geoCache[ip] = countryCode;
                    // This was a new unique visitor queued without a country — add them now
                    _countries.AddOrUpdate(countryCode, 1, (_, c) => c + 1);
                }
            }
        }
        catch
        {
            // Silently ignore GeoIP lookup failures — they are non-critical
        }
    }

    /// <summary>
    /// Get the current website metrics snapshot (admin view — includes all data).
    /// </summary>
    public WebsiteMetricsSnapshot GetSnapshot()
    {
        // Prune stale sessions (older than 5 minutes)
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var staleKeys = _activeSessions.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
        foreach (var key in staleKeys)
        {
            _activeSessions.TryRemove(key, out _);
        }

        // Calculate average session duration from active sessions
        var avgDuration = TimeSpan.Zero;
        var sessionStarts = _sessionStarts.Where(kv => _activeSessions.ContainsKey(kv.Key)).ToList();
        if (sessionStarts.Count > 0)
        {
            var totalSeconds = sessionStarts.Sum(kv => (DateTime.UtcNow - kv.Value).TotalSeconds);
            avgDuration = TimeSpan.FromSeconds(totalSeconds / sessionStarts.Count);
        }

        int uniqueCount;
        lock (_lock)
        {
            uniqueCount = _uniqueVisitors.Count;
        }

        return new WebsiteMetricsSnapshot
        {
            TotalVisitors = uniqueCount,
            ActiveVisitors = _activeSessions.Count,
            AvgSessionDuration = $"{(int)avgDuration.TotalMinutes}:{avgDuration.Seconds:D2}",
            TotalDownloads = Interlocked.Read(ref _totalDownloads),
            TotalPageViews = Interlocked.Read(ref _totalPageViews),
            DownloadsByPlatform = _downloads.ToDictionary(kv => kv.Key, kv => kv.Value),
            TopCountries = _countries.OrderByDescending(kv => kv.Value).Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            TopPages = _pageViews.OrderByDescending(kv => kv.Value).Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get a sanitized public subset of metrics (no admin-only data).
    /// Includes visitorsByRegion with country names, counts, and lat/lng for the world map.
    /// </summary>
    public object GetPublicSnapshot()
    {
        int uniqueCount;
        lock (_lock)
        {
            uniqueCount = _uniqueVisitors.Count;
        }

        // Prune stale active sessions
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var staleKeys = _activeSessions.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
        foreach (var key in staleKeys)
        {
            _activeSessions.TryRemove(key, out _);
        }

        // Build visitor-by-region data with country names and coordinates
        var visitorsByRegion = _countries
            .Select(kv =>
            {
                var code = kv.Key;
                var info = CountryInfo.GetValueOrDefault(code, (Name: code, Lat: 0.0, Lng: 0.0));
                return new
                {
                    code,
                    name = info.Name,
                    count = kv.Value,
                    lat = info.Lat,
                    lng = info.Lng
                };
            })
            .OrderByDescending(c => c.count)
            .ToList();

        return new
        {
            totalVisitors = uniqueCount,
            activeVisitors = _activeSessions.Count,
            totalDownloads = Interlocked.Read(ref _totalDownloads),
            downloadsByPlatform = _downloads.ToDictionary(kv => kv.Key, kv => kv.Value),
            visitorsByRegion,
            timestamp = DateTime.UtcNow
        };
    }

    private static string HashIp(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip + "_sgl_salt"));
        return Convert.ToHexString(bytes)[..16];
    }

    private void SaveToDisk()
    {
        try
        {
            List<string> visitorsCopy;
            lock (_lock)
            {
                visitorsCopy = _uniqueVisitors.ToList();
            }

            var data = new WebsiteMetricsPersisted
            {
                UniqueVisitors = visitorsCopy,
                TotalPageViews = Interlocked.Read(ref _totalPageViews),
                TotalDownloads = Interlocked.Read(ref _totalDownloads),
                Downloads = _downloads.ToDictionary(kv => kv.Key, kv => kv.Value),
                Countries = _countries.ToDictionary(kv => kv.Key, kv => kv.Value),
                PageViews = _pageViews.ToDictionary(kv => kv.Key, kv => kv.Value),
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Failed to save website metrics: {ex.Message}");
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<WebsiteMetricsPersisted>(json);
            if (data == null) return;

            lock (_lock)
            {
                foreach (var v in data.UniqueVisitors)
                    _uniqueVisitors.Add(v);
            }

            _totalPageViews = data.TotalPageViews;
            _totalDownloads = data.TotalDownloads;

            foreach (var kv in data.Downloads)
                _downloads[kv.Key] = kv.Value;
            foreach (var kv in data.Countries)
                _countries[kv.Key] = kv.Value;
            foreach (var kv in data.PageViews)
                _pageViews[kv.Key] = kv.Value;

            SglLogger.Information("Loaded website metrics: {Visitors} visitors, {Downloads} downloads, {Countries} countries",
                _uniqueVisitors.Count, _totalDownloads, _countries.Count);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Failed to load website metrics: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _persistTimer.Dispose();
        _geoTimer.Dispose();
        _geoHttp.Dispose();
        SaveToDisk();
    }
}

public class WebsiteMetricsSnapshot
{
    public long TotalVisitors { get; set; }
    public int ActiveVisitors { get; set; }
    public string AvgSessionDuration { get; set; } = "0:00";
    public long TotalDownloads { get; set; }
    public long TotalPageViews { get; set; }
    public Dictionary<string, long> DownloadsByPlatform { get; set; } = new();
    public Dictionary<string, long> TopCountries { get; set; } = new();
    public Dictionary<string, long> TopPages { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class WebsiteMetricsPersisted
{
    public List<string> UniqueVisitors { get; set; } = new();
    public long TotalPageViews { get; set; }
    public long TotalDownloads { get; set; }
    public Dictionary<string, long> Downloads { get; set; } = new();
    public Dictionary<string, long> Countries { get; set; } = new();
    public Dictionary<string, long> PageViews { get; set; } = new();
}
