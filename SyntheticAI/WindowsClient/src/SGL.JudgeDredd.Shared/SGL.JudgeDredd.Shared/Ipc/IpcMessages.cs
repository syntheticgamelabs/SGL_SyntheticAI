using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Shared.Ipc;

[JsonDerivedType(typeof(ScanRequestMessage), "ScanRequest")]
[JsonDerivedType(typeof(ScanResponseMessage), "ScanResponse")]
[JsonDerivedType(typeof(AlertNotificationMessage), "AlertNotification")]
[JsonDerivedType(typeof(StatusQueryMessage), "StatusQuery")]
[JsonDerivedType(typeof(StatusResponseMessage), "StatusResponse")]
[JsonDerivedType(typeof(SettingsUpdateMessage), "SettingsUpdate")]
public record IpcMessage
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record ScanRequestMessage : IpcMessage
{
    [JsonPropertyName("targetPath")]
    public string TargetPath { get; init; } = string.Empty;

    [JsonPropertyName("scanType")]
    public string ScanType { get; init; } = "Quick";

    [JsonPropertyName("recursive")]
    public bool Recursive { get; init; } = true;

    public ScanRequestMessage()
    {
        MessageType = "ScanRequest";
    }
}

public record ScanResponseMessage : IpcMessage
{
    [JsonPropertyName("requestId")]
    public Guid RequestId { get; init; }

    [JsonPropertyName("threatsFound")]
    public int ThreatsFound { get; init; }

    [JsonPropertyName("filesScanned")]
    public int FilesScanned { get; init; }

    [JsonPropertyName("scanDurationMs")]
    public long ScanDurationMs { get; init; }

    [JsonPropertyName("threats")]
    public List<ThreatInfo> Threats { get; init; } = new();

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    public ScanResponseMessage()
    {
        MessageType = "ScanResponse";
    }
}

public record ThreatInfo
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("threatName")]
    public string ThreatName { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "Unknown";

    [JsonPropertyName("sha256Hash")]
    public string Sha256Hash { get; init; } = string.Empty;

    [JsonPropertyName("detectionSource")]
    public string DetectionSource { get; init; } = string.Empty;
}

public record AlertNotificationMessage : IpcMessage
{
    [JsonPropertyName("alertId")]
    public Guid AlertId { get; init; } = Guid.NewGuid();

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "Warning";

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("actionRequired")]
    public bool ActionRequired { get; init; }

    public AlertNotificationMessage()
    {
        MessageType = "AlertNotification";
    }
}

public record StatusQueryMessage : IpcMessage
{
    [JsonPropertyName("component")]
    public string Component { get; init; } = "All";

    public StatusQueryMessage()
    {
        MessageType = "StatusQuery";
    }
}

public record StatusResponseMessage : IpcMessage
{
    [JsonPropertyName("component")]
    public string Component { get; init; } = string.Empty;

    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; init; }

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; init; }

    [JsonPropertyName("lastScanTime")]
    public DateTime? LastScanTime { get; init; }

    [JsonPropertyName("realTimeProtectionActive")]
    public bool RealTimeProtectionActive { get; init; }

    [JsonPropertyName("threatsBlockedToday")]
    public int ThreatsBlockedToday { get; init; }

    [JsonPropertyName("statusDetails")]
    public Dictionary<string, string> StatusDetails { get; init; } = new();

    public StatusResponseMessage()
    {
        MessageType = "StatusResponse";
    }
}

public record SettingsUpdateMessage : IpcMessage
{
    [JsonPropertyName("settingSection")]
    public string SettingSection { get; init; } = string.Empty;

    [JsonPropertyName("settingKey")]
    public string SettingKey { get; init; } = string.Empty;

    [JsonPropertyName("settingValue")]
    public string SettingValue { get; init; } = string.Empty;

    [JsonPropertyName("requiresRestart")]
    public bool RequiresRestart { get; init; }

    public SettingsUpdateMessage()
    {
        MessageType = "SettingsUpdate";
    }
}
