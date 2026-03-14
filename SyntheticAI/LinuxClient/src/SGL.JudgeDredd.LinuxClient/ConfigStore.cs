using System.Text.Json;

namespace SGL.JudgeDredd.LinuxClient;

public class ConfigStore
{
    private readonly string _configPath;

    public string ServerUrl { get; set; } = "https://syntheticgamelabs.dpdns.org";
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public ConfigStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".syntheticai");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");
        Load();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(new
        {
            ServerUrl, Token, Username, ClientId
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public void Load()
    {
        if (!File.Exists(_configPath)) return;
        try
        {
            var json = File.ReadAllText(_configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            ServerUrl = root.TryGetProperty("ServerUrl", out var s) ? s.GetString() ?? ServerUrl : ServerUrl;
            Token = root.TryGetProperty("Token", out var t) ? t.GetString() ?? "" : "";
            Username = root.TryGetProperty("Username", out var u) ? u.GetString() ?? "" : "";
            ClientId = root.TryGetProperty("ClientId", out var c) && c.TryGetGuid(out var g) ? g : Guid.Empty;
        }
        catch { }
    }

    public void Clear()
    {
        Token = string.Empty;
        Username = string.Empty;
        ClientId = Guid.Empty;
        Save();
    }
}
