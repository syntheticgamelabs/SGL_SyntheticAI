// SGL SyntheticAI .NET SDK
// Copyright (c) 2024-2026 Synthetic Game Labs. MIT Licensed.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntheticAI.Sdk;

/// <summary>
/// Client for the SGL SyntheticAI REST API.
/// </summary>
public class SyntheticAIClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _token;

    public SyntheticAIClient(string baseUrl, string? token = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };

        if (token is not null)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Authenticate with the server and store the JWT token.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Invalid auth response");

        _token = auth.Token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return auth;
    }

    /// <summary>
    /// Scan a file for threats.
    /// </summary>
    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        using var content = new MultipartFormDataContent();
        using var stream = File.OpenRead(filePath);
        content.Add(new StreamContent(stream), "file", Path.GetFileName(filePath));

        var response = await _http.PostAsync("/api/v1/scan/file", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ScanResult>()
            ?? throw new InvalidOperationException("Invalid scan response");
    }

    /// <summary>
    /// Get current threat intelligence data.
    /// </summary>
    public async Task<ThreatIntelResponse> GetThreatsAsync()
    {
        var response = await _http.GetAsync("/api/v1/threats");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ThreatIntelResponse>()
            ?? throw new InvalidOperationException("Invalid threat response");
    }

    /// <summary>
    /// Get server metrics (admin only).
    /// </summary>
    public async Task<ServerMetrics> GetServerMetricsAsync()
    {
        var response = await _http.GetAsync("/api/v1/admin/metrics");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServerMetrics>()
            ?? throw new InvalidOperationException("Invalid metrics response");
    }

    /// <summary>
    /// Get available LLM models.
    /// </summary>
    public async Task<LlmCatalog> GetLlmCatalogAsync()
    {
        var response = await _http.GetAsync("/api/v1/llm/catalog");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LlmCatalog>()
            ?? throw new InvalidOperationException("Invalid catalog response");
    }

    /// <summary>
    /// Send a chat message to the AI assistant.
    /// </summary>
    public async IAsyncEnumerable<string> ChatAsync(string message, string? username = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/llm/chat")
        {
            Content = JsonContent.Create(new { message, username = username ?? "sdk-user" })
        };

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("data: "))
                yield return line[6..];
        }
    }

    public void Dispose() => _http.Dispose();
}

// --- Response Models ---

public record AuthResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("role")] string Role
);

public record ScanResult(
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("score")] double Score
);

public record ThreatIntelResponse(
    [property: JsonPropertyName("totalSignatures")] int TotalSignatures,
    [property: JsonPropertyName("lastUpdated")] DateTime LastUpdated
);

public record ServerMetrics(
    [property: JsonPropertyName("cpuUsage")] double CpuUsage,
    [property: JsonPropertyName("memoryUsageMb")] long MemoryUsageMb,
    [property: JsonPropertyName("activeConnections")] int ActiveConnections,
    [property: JsonPropertyName("serverVersion")] string ServerVersion
);

public record LlmCatalog(
    [property: JsonPropertyName("models")] List<LlmModel> Models
);

public record LlmModel(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("role")] string? Role
);
