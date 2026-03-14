using System.Net.Http.Json;
using System.Text.Json;
using SGL.JudgeDredd.Mobile.Models;

namespace SGL.JudgeDredd.Mobile.Services;

public class GitService
{
    private readonly HttpClient _httpClient;
    private readonly string _localRepoPath;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? DownloadProgress;

    public GitService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SGL-JudgeDredd-Mobile/3.3.1");
        _localRepoPath = Path.Combine(FileSystem.AppDataDirectory, "repos");
        Directory.CreateDirectory(_localRepoPath);
    }

    public async Task<List<GitRepoInfo>> SearchReposAsync(string query, int perPage = 20)
    {
        var repos = new List<GitRepoInfo>();
        try
        {
            var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(query)}&per_page={perPage}&sort=stars";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return repos;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                repos.Add(new GitRepoInfo
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    FullName = item.GetProperty("full_name").GetString() ?? "",
                    Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    Url = item.GetProperty("html_url").GetString() ?? "",
                    CloneUrl = item.GetProperty("clone_url").GetString() ?? "",
                    Language = item.TryGetProperty("language", out var lang) ? lang.GetString() ?? "" : "",
                    Stars = item.GetProperty("stargazers_count").GetInt32(),
                    Forks = item.GetProperty("forks_count").GetInt32(),
                    Owner = item.GetProperty("owner").GetProperty("login").GetString() ?? "",
                    OwnerAvatarUrl = item.GetProperty("owner").GetProperty("avatar_url").GetString() ?? "",
                    IsPrivate = item.GetProperty("private").GetBoolean(),
                    SizeKb = item.GetProperty("size").GetInt64(),
                    DefaultBranch = item.TryGetProperty("default_branch", out var branch) ? branch.GetString() ?? "main" : "main"
                });
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Search failed: {ex.Message}");
        }
        return repos;
    }

    public async Task<List<GitFileItem>> GetRepoContentsAsync(string owner, string repo, string path = "")
    {
        var files = new List<GitFileItem>();
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return files;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                files.Add(new GitFileItem
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    Path = item.GetProperty("path").GetString() ?? "",
                    Type = item.GetProperty("type").GetString() ?? "file",
                    Size = item.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                    DownloadUrl = item.TryGetProperty("download_url", out var dl) ? dl.GetString() ?? "" : ""
                });
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Browse failed: {ex.Message}");
        }
        return files.OrderBy(f => f.Type == "file" ? 1 : 0).ThenBy(f => f.Name).ToList();
    }

    public async Task<bool> DownloadRepoAsync(GitRepoInfo repo, IProgress<double>? progress = null)
    {
        try
        {
            StatusChanged?.Invoke(this, $"Downloading {repo.Name}...");
            var zipUrl = $"https://api.github.com/repos/{repo.FullName}/zipball/{repo.DefaultBranch}";

            using var response = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                StatusChanged?.Invoke(this, $"Download failed: HTTP {(int)response.StatusCode}");
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var repoDir = Path.Combine(_localRepoPath, repo.Name);
            var zipPath = repoDir + ".zip";

            Directory.CreateDirectory(repoDir);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(zipPath);
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                if (totalBytes > 0)
                {
                    var pct = (double)totalRead / totalBytes;
                    progress?.Report(pct);
                    DownloadProgress?.Invoke(this, pct);
                }
            }

            fileStream.Close();

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, repoDir, true);
            File.Delete(zipPath);

            StatusChanged?.Invoke(this, $"{repo.Name} downloaded to {repoDir}");
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Download failed: {ex.Message}");
            return false;
        }
    }

    public List<GitRepoInfo> GetLocalRepos()
    {
        var repos = new List<GitRepoInfo>();
        if (!Directory.Exists(_localRepoPath)) return repos;

        foreach (var dir in Directory.GetDirectories(_localRepoPath))
        {
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            repos.Add(new GitRepoInfo
            {
                Name = dirInfo.Name,
                IsLocal = true,
                LocalPath = dir,
                SizeKb = files.Sum(f => f.Length) / 1024,
                UpdatedAt = dirInfo.LastWriteTime,
                Status = "Local"
            });
        }
        return repos;
    }

    public async Task<bool> DeleteLocalRepoAsync(string repoName)
    {
        var path = Path.Combine(_localRepoPath, repoName);
        if (Directory.Exists(path))
        {
            await Task.Run(() => Directory.Delete(path, true));
            StatusChanged?.Invoke(this, $"Deleted {repoName}");
            return true;
        }
        return false;
    }

    public async Task<string> GetFileContentAsync(string owner, string repo, string filePath)
    {
        try
        {
            var url = $"https://raw.githubusercontent.com/{owner}/{repo}/main/{filePath}";
            return await _httpClient.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            return $"Error loading file: {ex.Message}";
        }
    }
}