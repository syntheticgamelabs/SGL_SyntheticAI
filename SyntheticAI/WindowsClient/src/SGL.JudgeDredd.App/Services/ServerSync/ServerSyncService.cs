using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Api.Contracts.Models;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.App.Services.ServerSync;

/// <summary>
/// Background service that manages client-server communication in Client deployment mode.
/// Handles registration, heartbeat, signature sync, and log upload.
/// </summary>
public sealed class ServerSyncService : IHostedService, IDisposable
{
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private Timer? _heartbeatTimer;
    private Timer? _signatureSyncTimer;
    private bool _isRegistered;
    private Guid _clientId;
    private string _apiKey = string.Empty;
    private readonly string _configPath;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 5;

    // Stats tracked for heartbeat
    public int ThreatsDetected { get; set; }
    public int FilesScanned { get; set; }
    public bool RealTimeProtectionActive { get; set; }
    public bool LlmModelLoaded { get; set; }

    private readonly DateTime _startedAt = DateTime.UtcNow;

    public bool IsConnected => _isRegistered && _consecutiveFailures < MaxConsecutiveFailures;
    public string ServerUrl => _settings.Client.ServerUrl;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<SignatureUpdateResponse>? SignatureUpdateAvailable;

    public ServerSyncService(AppSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.Client.ServerUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _configPath = Path.Combine(AppContext.BaseDirectory, "data", "server_config.json");

        // Try to load saved client config
        LoadSavedConfig();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.IsClientMode || !_settings.Client.AutoSync)
        {
            SglLogger.Information("ServerSyncService: Not in client mode or auto-sync disabled. Skipping.");
            return;
        }

        SglLogger.Information("ServerSyncService: Starting client sync to {ServerUrl}", _settings.Client.ServerUrl);
        RaiseStatus("Connecting to server...");

        // Register with server
        await RegisterWithServerAsync();

        // Start heartbeat timer
        var heartbeatInterval = TimeSpan.FromMinutes(_settings.Client.SyncIntervalMinutes);
        _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), null, heartbeatInterval, heartbeatInterval);

        // Start signature sync timer
        var sigSyncInterval = TimeSpan.FromMinutes(_settings.Client.SignatureSyncIntervalMinutes);
        _signatureSyncTimer = new Timer(async _ => await CheckSignatureUpdatesAsync(), null, sigSyncInterval, sigSyncInterval);

        SglLogger.Information("ServerSyncService: Background timers started (heartbeat: {Heartbeat}min, sig sync: {SigSync}min)",
            _settings.Client.SyncIntervalMinutes, _settings.Client.SignatureSyncIntervalMinutes);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _signatureSyncTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        // Upload final logs before shutdown
        await UploadLogsAsync("shutdown");

        SglLogger.Information("ServerSyncService: Stopped.");
    }

    /// <summary>
    /// Register this client with the server. If already registered, uses saved credentials.
    /// </summary>
    public async Task RegisterWithServerAsync()
    {
        // If we have saved credentials, validate them with a heartbeat
        if (_isRegistered && !string.IsNullOrEmpty(_apiKey))
        {
            SglLogger.Information("ServerSyncService: Using saved registration.");
            SetAuthHeader();
            await SendHeartbeatAsync();
            return;
        }

        try
        {
            var request = new ClientRegistrationRequest
            {
                Username = Environment.UserName,
                Password = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{Environment.MachineName}:{Environment.UserName}:SyntheticAI"))),
                MachineName = Environment.MachineName,
                ClientVersion = "3.2.0",
                Platform = "Windows",
                DeviceModel = Environment.MachineName,
                OsVersion = $"Windows {Environment.OSVersion.Version}"
            };

            var response = await _httpClient.PostAsJsonAsync(ApiConstants.ClientRegister, request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ClientRegistrationResponse>();
                if (result != null)
                {
                    _clientId = result.ClientId;
                    _apiKey = result.ApiKey;
                    _isRegistered = true;
                    _consecutiveFailures = 0;

                    SetAuthHeader();
                    SaveConfig();

                    SglLogger.Information("ServerSyncService: Registered with server. ClientId: {ClientId}", _clientId);
                    RaiseStatus("Connected to server.");
                }
            }
            else
            {
                SglLogger.Error("ServerSyncService: Registration failed. Status: " + response.StatusCode);
                RaiseStatus("Registration failed.");
                _consecutiveFailures++;
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Registration error.", ex);
            RaiseStatus("Server unreachable.");
            _consecutiveFailures++;
        }
    }

    /// <summary>
    /// Send heartbeat with current client stats.
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        if (!_isRegistered) return;

        try
        {
            var request = new ClientHeartbeatRequest
            {
                ClientId = _clientId,
                SignatureVersion = "2025.02.23.1",
                RealTimeProtectionActive = RealTimeProtectionActive,
                ThreatsDetected = ThreatsDetected,
                FilesScanned = FilesScanned,
                LlmModelLoaded = LlmModelLoaded,
                UptimeMinutes = (DateTime.UtcNow - _startedAt).TotalMinutes
            };

            var response = await _httpClient.PostAsJsonAsync(ApiConstants.ClientHeartbeat, request);

            if (response.IsSuccessStatusCode)
            {
                _consecutiveFailures = 0;
                var result = await response.Content.ReadFromJsonAsync<ClientHeartbeatResponse>();

                if (result?.HasUpdate == true)
                {
                    SglLogger.Information("ServerSyncService: Server reports update available.");
                    RaiseStatus("Signature update available.");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Client not recognized — re-register
                _isRegistered = false;
                await RegisterWithServerAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expired or invalid — re-register
                SglLogger.Warning("ServerSyncService: Heartbeat returned 401 Unauthorized. Re-registering...");
                _isRegistered = false;
                await RegisterWithServerAsync();
            }
            else
            {
                _consecutiveFailures++;
                SglLogger.Error("ServerSyncService: Heartbeat failed. Status: " + response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            SglLogger.Error("ServerSyncService: Heartbeat error.", ex);

            if (_consecutiveFailures >= MaxConsecutiveFailures)
                RaiseStatus("Server connection lost.");
        }
    }

    /// <summary>
    /// Check the server for signature database updates.
    /// </summary>
    public async Task CheckSignatureUpdatesAsync()
    {
        if (!_isRegistered) return;

        try
        {
            var response = await _httpClient.GetAsync(ApiConstants.SignatureCheck);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SignatureUpdateResponse>();
                if (result?.Available == true)
                {
                    SglLogger.Information("ServerSyncService: Signature update available. Version: {Version}", result.Version);
                    SignatureUpdateAvailable?.Invoke(this, result);
                    RaiseStatus($"Signature update available: {result.Version}");
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Signature check error.", ex);
        }
    }

    /// <summary>
    /// Send a threat analysis request to the server's LLM.
    /// </summary>
    public async Task<ThreatAnalysisResponse?> RequestThreatAnalysisAsync(ThreatAnalysisRequest request)
    {
        if (!_isRegistered) return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(ApiConstants.ThreatAnalyze, request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ThreatAnalysisResponse>();
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Threat analysis request failed.", ex);
        }

        return null;
    }

    /// <summary>
    /// Report a detected threat to the server so it can learn and share with other clients.
    /// </summary>
    public async Task ReportThreatAsync(string fileName, string sha256Hash, string threatName, double confidence)
    {
        if (!_isRegistered) return;

        try
        {
            var request = new ThreatAnalysisRequest
            {
                FileName = fileName,
                Sha256Hash = sha256Hash,
                PrimaryIndicator = threatName,
                HeuristicScore = confidence,
                FileSize = 0,
                ImportedApis = Array.Empty<string>(),
                SectionNames = Array.Empty<string>(),
                SectionEntropies = Array.Empty<double>()
            };

            await _httpClient.PostAsJsonAsync(ApiConstants.ThreatAnalyze, request);
            SglLogger.Information("ServerSyncService: Reported threat '{ThreatName}' ({Hash}) to server.", threatName, sha256Hash[..8]);
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Failed to report threat.", ex);
        }
    }

    /// <summary>
    /// Upload logs to the server.
    /// </summary>
    public async Task UploadLogsAsync(string logType)
    {
        if (!_isRegistered) return;

        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDir)) return;

            var latestLog = Directory.GetFiles(logDir, "*.log")
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestLog == null) return;

            var content = await File.ReadAllTextAsync(latestLog);

            // Limit to last 50KB
            if (content.Length > 51200)
                content = content[^51200..];

            var request = new LogUploadRequest
            {
                ClientId = _clientId,
                LogType = logType,
                Timestamp = DateTime.UtcNow,
                Content = content,
                MachineName = Environment.MachineName
            };

            await _httpClient.PostAsJsonAsync(ApiConstants.LogUpload, request);
            SglLogger.Information("ServerSyncService: Logs uploaded ({LogType}).", logType);
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Log upload failed.", ex);
        }
    }

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Remove(ApiConstants.AuthHeaderName);
        _httpClient.DefaultRequestHeaders.Add(ApiConstants.AuthHeaderName, $"{ApiConstants.AuthScheme} {_apiKey}");
    }

    private void RaiseStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private void SaveConfig()
    {
        try
        {
            var config = new
            {
                ClientId = _clientId.ToString(),
                ApiKey = _apiKey,
                ServerUrl = _settings.Client.ServerUrl,
                RegisteredAt = DateTime.UtcNow
            };

            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            SglLogger.Error("ServerSyncService: Failed to save config.", ex);
        }
    }

    private void LoadSavedConfig()
    {
        try
        {
            if (!File.Exists(_configPath)) return;

            var json = File.ReadAllText(_configPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("ClientId", out var cidProp) &&
                Guid.TryParse(cidProp.GetString(), out var clientId) &&
                doc.RootElement.TryGetProperty("ApiKey", out var keyProp))
            {
                _clientId = clientId;
                _apiKey = keyProp.GetString() ?? string.Empty;

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    _isRegistered = true;
                    SglLogger.Information("ServerSyncService: Loaded saved config. ClientId: {ClientId}", _clientId);
                }
            }
        }
        catch
        {
            // Config file corrupted — will re-register
        }
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _signatureSyncTimer?.Dispose();
        _httpClient.Dispose();
    }
}
