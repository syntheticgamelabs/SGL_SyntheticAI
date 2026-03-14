using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SGL.JudgeDredd.Api.Contracts;

namespace SGL.JudgeDredd.LinuxClient;

public class LinuxApiClient
{
    private readonly ConfigStore _config;
    private readonly HttpClient _http;

    public LinuxApiClient(ConfigStore config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private void SetAuth()
    {
        if (!string.IsNullOrEmpty(_config.Token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);
    }

    public async Task<(bool success, string message)> RegisterAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_config.ServerUrl}{ApiConstants.ClientRegister}", new
            {
                username, password,
                machineName = Environment.MachineName,
                clientVersion = "Linux CLI 1.0.0",
                platform = "Linux",
                deviceModel = "CLI",
                osVersion = Environment.OSVersion.ToString()
            });

            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                _config.Token = root.GetProperty("token").GetString() ?? "";
                _config.ClientId = root.GetProperty("clientId").GetGuid();
                _config.Username = username;
                _config.Save();
                return (true, "Registration successful!");
            }
            if ((int)response.StatusCode == 409)
                return (false, "Username already exists. Use 'login' instead.");
            return (false, $"Registration failed: {content}");
        }
        catch (Exception ex) { return (false, $"Connection error: {ex.Message}"); }
    }

    public async Task<(bool success, string message)> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_config.ServerUrl}{ApiConstants.ClientLogin}", new { username, password });
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                _config.Token = root.GetProperty("token").GetString() ?? "";
                _config.ClientId = root.GetProperty("clientId").GetGuid();
                _config.Username = username;
                _config.Save();
                return (true, "Login successful!");
            }
            return (false, $"Login failed: {content}");
        }
        catch (Exception ex) { return (false, $"Connection error: {ex.Message}"); }
    }

    public async Task<string> GetServerStatusAsync()
    {
        try
        {
            SetAuth();
            var response = await _http.GetAsync($"{_config.ServerUrl}{ApiConstants.ServerStatus}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var version = root.TryGetProperty("serverVersion", out var v) ? v.GetString() : "?";
                var online = root.TryGetProperty("onlineClients", out var oc) ? oc.GetInt32() : 0;
                var llm = root.TryGetProperty("llmModelLoaded", out var lm) && lm.GetBoolean();
                return $"Server v{version} | {online} client(s) online | LLM: {(llm ? "Loaded" : "Not loaded")}";
            }
            return $"Server returned {(int)response.StatusCode}";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public async Task<string> ChatAsync(string message)
    {
        try
        {
            SetAuth();
            var response = await _http.PostAsJsonAsync($"{_config.ServerUrl}{ApiConstants.ThreatAnalyze}", new
            {
                fileName = "chat", filePath = "interactive", sha256 = "", fileSize = 0, userQuery = message
            });
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("analysis", out var a))
                    return a.GetString() ?? "No response.";
                return content;
            }
            return $"Server returned {(int)response.StatusCode}";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public async Task<List<FaqEntry>> GetFaqAsync()
    {
        try
        {
            SetAuth();
            var response = await _http.GetAsync($"{_config.ServerUrl}/api/v1/faq");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<FaqEntry>>() ?? new();
            return new();
        }
        catch { return new(); }
    }

    public async Task<string> AskFaqAsync(string question)
    {
        try
        {
            SetAuth();
            var response = await _http.PostAsJsonAsync($"{_config.ServerUrl}/api/v1/faq/ask", new { question });
            return response.IsSuccessStatusCode ? "Question submitted!" : "Failed to submit question.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}

public class FaqEntry
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string AskedBy { get; set; } = "";
    public DateTime AskedAt { get; set; }
    public bool IsAnswered { get; set; }
}
