using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.JudgeDredd.Shared.Configuration;

public class AppSettings
{
    [JsonIgnore]
    private string? _filePath;

    [JsonPropertyName("general")]
    public GeneralSettings General { get; set; } = new();

    [JsonPropertyName("scanner")]
    public ScannerSettings Scanner { get; set; } = new();

    [JsonPropertyName("firewall")]
    public FirewallSettings Firewall { get; set; } = new();

    [JsonPropertyName("llm")]
    public LlmSettings Llm { get; set; } = new();

    [JsonPropertyName("security")]
    public SecuritySettings Security { get; set; } = new();

    [JsonPropertyName("deploymentMode")]
    public string DeploymentMode { get; set; } = "Client";

    [JsonPropertyName("server")]
    public ServerSettings Server { get; set; } = new();

    [JsonPropertyName("client")]
    public ClientSettings Client { get; set; } = new();

    [JsonIgnore]
    public bool IsServerMode => DeploymentMode.Equals("Server", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsClientMode => DeploymentMode.Equals("Client", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettings LoadFromFile(string filePath)
    {
        AppSettings settings;
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                settings = new AppSettings();
            }
        }
        else
        {
            settings = new AppSettings();
        }

        settings._filePath = filePath;
        return settings;
    }

    public void SaveToFile(string? filePath = null)
    {
        var path = filePath ?? _filePath;
        if (string.IsNullOrEmpty(path)) return;

        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }
}

public class GeneralSettings
{
    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; }

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("notificationSounds")]
    public bool NotificationSounds { get; set; } = true;
}

public class ScannerSettings
{
    [JsonPropertyName("realTimeProtection")]
    public bool RealTimeProtection { get; set; } = true;

    [JsonPropertyName("scanExclusions")]
    public List<string> ScanExclusions { get; set; } = new();

    [JsonPropertyName("scheduledScanTime")]
    public TimeSpan? ScheduledScanTime { get; set; }
}

public class FirewallSettings
{
    [JsonPropertyName("defaultAction")]
    public string DefaultAction { get; set; } = "Block";

    [JsonPropertyName("loggingVerbosity")]
    public string LoggingVerbosity { get; set; } = "Normal";
}

public class LlmSettings
{
    [JsonPropertyName("contextSize")]
    public int ContextSize { get; set; } = 8192;

    [JsonPropertyName("gpuLayers")]
    public int GpuLayers { get; set; } = 33;

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.8f;

    [JsonPropertyName("modelPath")]
    public string ModelPath { get; set; } = string.Empty;

    [JsonPropertyName("ttsServerUrl")]
    public string? TtsServerUrl { get; set; } = "http://localhost:8091";

    [JsonPropertyName("ttsVoice")]
    public string TtsVoice { get; set; } = "Chelsie";
}

public class SecuritySettings
{
    [JsonPropertyName("enableRemoteAccessDetection")]
    public bool EnableRemoteAccessDetection { get; set; } = true;

    [JsonPropertyName("enableBadUsbDetection")]
    public bool EnableBadUsbDetection { get; set; } = true;

    [JsonPropertyName("enableAiDetection")]
    public bool EnableAiDetection { get; set; } = true;

    [JsonPropertyName("enableRegistryWatcher")]
    public bool EnableRegistryWatcher { get; set; } = true;
}

public class ServerSettings
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 5000;

    [JsonPropertyName("legacyPort")]
    public int LegacyPort { get; set; } = 7743;

    [JsonPropertyName("listenAddress")]
    public string ListenAddress { get; set; } = "0.0.0.0";

    [JsonPropertyName("maxClients")]
    public int MaxClients { get; set; } = 100;

    [JsonPropertyName("publicDomain")]
    public string PublicDomain { get; set; } = "syntheticgamelabs.dpdns.org";

    [JsonPropertyName("tunnelToken")]
    public string TunnelToken { get; set; } = string.Empty;

    [JsonPropertyName("cloudflareZoneId")]
    public string CloudflareZoneId { get; set; } = string.Empty;

    [JsonPropertyName("cloudflareAccountId")]
    public string CloudflareAccountId { get; set; } = string.Empty;

    [JsonPropertyName("originCertPath")]
    public string OriginCertPath { get; set; } = string.Empty;

    [JsonPropertyName("originKeyPath")]
    public string OriginKeyPath { get; set; } = string.Empty;

    [JsonPropertyName("cloudflareApiEmail")]
    public string CloudflareApiEmail { get; set; } = string.Empty;

    [JsonPropertyName("cloudflareApiKey")]
    public string CloudflareApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string PublicUrl => $"https://{PublicDomain}";
}

public class ClientSettings
{
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = "https://syntheticgamelabs.dpdns.org";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("syncIntervalMinutes")]
    public int SyncIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("signatureSyncIntervalMinutes")]
    public int SignatureSyncIntervalMinutes { get; set; } = 30;

    [JsonPropertyName("autoSync")]
    public bool AutoSync { get; set; } = true;
}
