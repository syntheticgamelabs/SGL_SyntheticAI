namespace SGL.JudgeDredd.Core.Interfaces;

/// <summary>
/// Service for checking email addresses and domains against known data breaches.
/// Provides dark web monitoring capabilities.
/// </summary>
public interface IBreachDetectionService
{
    /// <summary>
    /// Check if an email has been involved in known data breaches.
    /// </summary>
    Task<BreachCheckResult> CheckEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Check if a domain has been involved in known data breaches.
    /// </summary>
    Task<BreachCheckResult> CheckDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Check if a password has been exposed in known breaches (using k-anonymity / SHA-1 prefix).
    /// </summary>
    Task<PasswordCheckResult> CheckPasswordAsync(string password, CancellationToken ct = default);

    /// <summary>
    /// Get the list of monitored emails/domains.
    /// </summary>
    List<MonitoredItem> GetMonitoredItems();

    /// <summary>
    /// Add an email or domain to the monitoring watchlist.
    /// </summary>
    void AddMonitoredItem(string value, string type);

    /// <summary>
    /// Remove an item from the monitoring watchlist.
    /// </summary>
    void RemoveMonitoredItem(string value);
}

public class BreachCheckResult
{
    public string Query { get; set; } = "";
    public bool IsBreached { get; set; }
    public int BreachCount { get; set; }
    public List<BreachInfo> Breaches { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class BreachInfo
{
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public DateTime BreachDate { get; set; }
    public string Description { get; set; } = "";
    public long PwnCount { get; set; }
    public List<string> DataClasses { get; set; } = new();
}

public class PasswordCheckResult
{
    public bool IsExposed { get; set; }
    public int ExposureCount { get; set; }
    public string Severity { get; set; } = "safe";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class MonitoredItem
{
    public string Value { get; set; } = "";
    public string Type { get; set; } = "email"; // email or domain
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastChecked { get; set; }
    public bool IsBreached { get; set; }
    public int BreachCount { get; set; }
}
