using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace SGL.JudgeDredd.App.Services;

public class GitService : IDisposable
{
    private Repository? _currentRepo;
    private string _gitRootPath;
    private string? _currentRepoPath;
    private string? _savedUsername;
    private string? _savedPassword;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? ProgressChanged;

    public string GitRootPath => _gitRootPath;
    public bool IsRepositoryOpen => _currentRepo != null;
    public string? CurrentRepositoryName => _currentRepoPath != null ? Path.GetFileName(_currentRepoPath) : null;
    public string? CurrentBranch => _currentRepo?.Head?.FriendlyName;

    public GitService()
    {
        _gitRootPath = Path.Combine(AppContext.BaseDirectory, "GIT");
        Directory.CreateDirectory(_gitRootPath);
    }

    public void SetCredentials(string username, string password)
    {
        _savedUsername = username;
        _savedPassword = password;
    }

    public void ClearCredentials()
    {
        _savedUsername = null;
        _savedPassword = null;
    }

    private CredentialsHandler? CreateCredentialsHandler()
    {
        if (string.IsNullOrEmpty(_savedUsername) || string.IsNullOrEmpty(_savedPassword))
            return null;
        return (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials
            {
                Username = _savedUsername,
                Password = _savedPassword
            };
    }

    private FetchOptions CreateFetchOptions()
    {
        var options = new FetchOptions();
        var handler = CreateCredentialsHandler();
        if (handler != null)
            options.CredentialsProvider = handler;
        options.OnTransferProgress = progress =>
        {
            if (progress.TotalObjects > 0)
            {
                double pct = (double)progress.ReceivedObjects / progress.TotalObjects;
                ProgressChanged?.Invoke(this, pct);
            }
            return true;
        };
        return options;
    }

    private PushOptions CreatePushOptions()
    {
        var options = new PushOptions();
        var handler = CreateCredentialsHandler();
        if (handler != null)
            options.CredentialsProvider = handler;
        return options;
    }

    // --- Repository Management ---

    public List<string> GetAvailableRepositories()
    {
        if (!Directory.Exists(_gitRootPath))
            return new List<string>();
        return Directory.GetDirectories(_gitRootPath)
            .Where(d => Repository.IsValid(d))
            .Select(d => Path.GetFileName(d))
            .OrderBy(n => n)
            .ToList();
    }

    public async Task CloneRepositoryAsync(string url, string? localName = null)
    {
        await Task.Run(() =>
        {
            var repoName = localName ?? ExtractRepoName(url);
            var targetPath = Path.Combine(_gitRootPath, repoName);

            if (Directory.Exists(targetPath))
                throw new InvalidOperationException($"Directory '{repoName}' already exists in the GIT folder.");

            StatusChanged?.Invoke(this, $"Cloning {url}...");

            var cloneOptions = new CloneOptions
            {
                Checkout = true,
                IsBare = false,
            };

            var credHandler = CreateCredentialsHandler();
            if (credHandler != null)
            {
                cloneOptions.FetchOptions.CredentialsProvider = credHandler;
            }

            cloneOptions.FetchOptions.OnTransferProgress = progress =>
            {
                if (progress.TotalObjects > 0)
                {
                    double pct = (double)progress.ReceivedObjects / progress.TotalObjects;
                    ProgressChanged?.Invoke(this, pct);
                    StatusChanged?.Invoke(this, $"Receiving objects: {progress.ReceivedObjects}/{progress.TotalObjects}");
                }
                return true;
            };

            Repository.Clone(url, targetPath, cloneOptions);
            StatusChanged?.Invoke(this, $"Clone complete: {repoName}");
        });
    }

    public void OpenRepository(string repoName)
    {
        CloseCurrentRepository();
        var repoPath = Path.Combine(_gitRootPath, repoName);
        if (!Repository.IsValid(repoPath))
            throw new InvalidOperationException($"'{repoName}' is not a valid Git repository.");
        _currentRepo = new Repository(repoPath);
        _currentRepoPath = repoPath;
        StatusChanged?.Invoke(this, $"Opened repository: {repoName} (branch: {CurrentBranch})");
    }

    public void OpenLocalRepository(string fullPath)
    {
        CloseCurrentRepository();
        if (!Repository.IsValid(fullPath))
            throw new InvalidOperationException($"'{fullPath}' is not a valid Git repository.");
        _currentRepo = new Repository(fullPath);
        _currentRepoPath = fullPath;
        StatusChanged?.Invoke(this, $"Opened local repository: {Path.GetFileName(fullPath)} (branch: {CurrentBranch})");
    }

    public void InitNewRepository(string repoName)
    {
        var repoPath = Path.Combine(_gitRootPath, repoName);
        Directory.CreateDirectory(repoPath);
        Repository.Init(repoPath);

        // Create default .gitattributes
        var gitattributesPath = Path.Combine(repoPath, ".gitattributes");
        if (!File.Exists(gitattributesPath))
        {
            File.WriteAllText(gitattributesPath, "# Auto detect text files and normalize line endings\n* text=auto\n\n# Binary files\n*.png binary\n*.jpg binary\n*.gif binary\n*.ico binary\n*.zip binary\n*.gz binary\n*.exe binary\n*.dll binary\n*.apk binary\n");
        }

        StatusChanged?.Invoke(this, $"Initialized new repository: {repoName}");
    }

    public string GetRepositoryPath(string repoName) => Path.Combine(_gitRootPath, repoName);

    public void CloseCurrentRepository()
    {
        _currentRepo?.Dispose();
        _currentRepo = null;
        _currentRepoPath = null;
    }

    // --- Branch Operations ---

    public List<string> GetLocalBranches()
    {
        if (_currentRepo == null) return new List<string>();
        return _currentRepo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => b.FriendlyName)
            .OrderBy(n => n)
            .ToList();
    }

    public List<string> GetRemoteBranches()
    {
        if (_currentRepo == null) return new List<string>();
        return _currentRepo.Branches
            .Where(b => b.IsRemote)
            .Select(b => b.FriendlyName)
            .OrderBy(n => n)
            .ToList();
    }

    public void SwitchBranch(string branchName)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        var branch = _currentRepo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");
        Commands.Checkout(_currentRepo, branch);
        StatusChanged?.Invoke(this, $"Switched to branch: {branchName}");
    }

    public void CreateBranch(string branchName, bool switchTo = true)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        var branch = _currentRepo.CreateBranch(branchName);
        if (switchTo)
        {
            Commands.Checkout(_currentRepo, branch);
        }
        StatusChanged?.Invoke(this, $"Created branch: {branchName}");
    }

    public void DeleteBranch(string branchName)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        if (branchName == CurrentBranch)
            throw new InvalidOperationException("Cannot delete the currently checked-out branch.");
        _currentRepo.Branches.Remove(branchName);
        StatusChanged?.Invoke(this, $"Deleted branch: {branchName}");
    }

    // --- Fetch/Pull/Push ---

    public async Task<string> FetchAsync()
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        return await Task.Run(() =>
        {
            StatusChanged?.Invoke(this, "Fetching from remote...");
            var remote = _currentRepo.Network.Remotes["origin"];
            if (remote == null)
                throw new InvalidOperationException("No 'origin' remote configured.");

            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
            Commands.Fetch(_currentRepo, remote.Name, refSpecs, CreateFetchOptions(), "Fetch from origin");

            var result = $"Fetch completed at {DateTime.Now:HH:mm:ss}";
            StatusChanged?.Invoke(this, result);
            return result;
        });
    }

    public async Task<string> PullAsync()
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        return await Task.Run(() =>
        {
            StatusChanged?.Invoke(this, "Pulling from remote...");

            var options = new PullOptions
            {
                FetchOptions = CreateFetchOptions(),
            };

            // Create a signature for the merge commit if needed
            var sig = GetSignature();
            var result = Commands.Pull(_currentRepo, sig, options);

            var statusMsg = result.Status switch
            {
                MergeStatus.FastForward => $"Pull fast-forwarded to {result.Commit?.Sha[..7]}",
                MergeStatus.NonFastForward => $"Pull merged - new merge commit {result.Commit?.Sha[..7]}",
                MergeStatus.UpToDate => "Already up to date",
                MergeStatus.Conflicts => "Pull completed with CONFLICTS - resolve manually",
                _ => $"Pull completed: {result.Status}"
            };

            StatusChanged?.Invoke(this, statusMsg);
            return statusMsg;
        });
    }

    public async Task<string> PushAsync()
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        return await Task.Run(() =>
        {
            StatusChanged?.Invoke(this, "Pushing to remote...");

            var currentBranch = _currentRepo.Head;
            if (currentBranch.TrackedBranch == null)
            {
                // Set up tracking if not already tracking
                var remote = _currentRepo.Network.Remotes["origin"];
                if (remote != null)
                {
                    _currentRepo.Branches.Update(currentBranch,
                        b => b.Remote = remote.Name,
                        b => b.UpstreamBranch = currentBranch.CanonicalName);
                }
            }

            _currentRepo.Network.Push(currentBranch, CreatePushOptions());

            var result = $"Pushed to {currentBranch.FriendlyName} at {DateTime.Now:HH:mm:ss}";
            StatusChanged?.Invoke(this, result);
            return result;
        });
    }

    // --- Commit Operations ---

    public List<GitFileChange> GetUnstagedChanges()
    {
        if (_currentRepo == null) return new List<GitFileChange>();
        var status = _currentRepo.RetrieveStatus(new StatusOptions());
        var changes = new List<GitFileChange>();

        foreach (var entry in status)
        {
            if (entry.State == FileStatus.Ignored) continue;
            changes.Add(new GitFileChange
            {
                FilePath = entry.FilePath,
                Status = entry.State.ToString(),
                IsStaged = entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
                           entry.State.HasFlag(FileStatus.NewInIndex) ||
                           entry.State.HasFlag(FileStatus.DeletedFromIndex) ||
                           entry.State.HasFlag(FileStatus.RenamedInIndex),
            });
        }

        return changes;
    }

    public void StageFile(string filePath)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        Commands.Stage(_currentRepo, filePath);
    }

    public void StageAll()
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        Commands.Stage(_currentRepo, "*");
    }

    public string Commit(string message)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        var sig = GetSignature();
        var commit = _currentRepo.Commit(message, sig, sig);
        var result = $"Committed {commit.Sha[..7]}: {message}";
        StatusChanged?.Invoke(this, result);
        return result;
    }

    // --- History ---

    public List<GitCommitInfo> GetCommitHistory(int maxCount = 50)
    {
        if (_currentRepo == null) return new List<GitCommitInfo>();
        return _currentRepo.Commits
            .Take(maxCount)
            .Select(c => new GitCommitInfo
            {
                Sha = c.Sha,
                ShortSha = c.Sha[..7],
                Message = c.MessageShort,
                FullMessage = c.Message,
                Author = c.Author.Name,
                Email = c.Author.Email,
                Date = c.Author.When.DateTime,
            })
            .ToList();
    }

    public void RevertToCommit(string sha)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        var commit = _currentRepo.Lookup<Commit>(sha);
        if (commit == null) throw new InvalidOperationException($"Commit {sha} not found.");
        _currentRepo.Reset(ResetMode.Hard, commit);
        StatusChanged?.Invoke(this, $"Reverted to commit {sha[..7]}");
    }

    // --- Merge ---

    public string MergeBranch(string branchName)
    {
        if (_currentRepo == null) throw new InvalidOperationException("No repository open.");
        var branch = _currentRepo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");

        var sig = GetSignature();
        var result = _currentRepo.Merge(branch, sig);

        var statusMsg = result.Status switch
        {
            MergeStatus.FastForward => $"Merged {branchName} (fast-forward)",
            MergeStatus.NonFastForward => $"Merged {branchName} - merge commit created",
            MergeStatus.UpToDate => $"{branchName} is already up to date",
            MergeStatus.Conflicts => $"Merge of {branchName} has CONFLICTS - resolve manually",
            _ => $"Merge {branchName}: {result.Status}"
        };

        StatusChanged?.Invoke(this, statusMsg);
        return statusMsg;
    }

    // --- Helpers ---

    private Signature GetSignature()
    {
        // Try to get identity from repo config, fall back to defaults
        var name = _currentRepo?.Config?.Get<string>("user.name")?.Value ?? _savedUsername ?? "SyntheticAI User";
        var email = _currentRepo?.Config?.Get<string>("user.email")?.Value ?? "user@syntheticai.local";
        return new Signature(name, email, DateTimeOffset.Now);
    }

    private static string ExtractRepoName(string url)
    {
        // Handle URLs like https://github.com/user/repo.git or git@github.com:user/repo.git
        var name = url.TrimEnd('/');
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        var lastSlash = name.LastIndexOfAny(new[] { '/', ':' });
        if (lastSlash >= 0)
            name = name[(lastSlash + 1)..];
        return string.IsNullOrEmpty(name) ? "repository" : name;
    }

    // --- Git Command Runner ---

    private async Task<(bool success, string output)> RunGitCommandAsync(string arguments, string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi)!;
            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return (proc.ExitCode == 0, stdout + stderr);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // --- Git LFS Support ---

    public async Task<bool> IsGitLfsInstalledAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "lfs version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            await proc!.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task<string> InitializeLfsAsync()
    {
        if (_currentRepo == null) return "No repository open";
        var result = await RunGitCommandAsync("lfs install", _currentRepoPath!);
        return result.success ? "Git LFS initialized" : $"LFS init failed: {result.output}";
    }

    public async Task<string> TrackLfsPatternAsync(string pattern)
    {
        if (_currentRepo == null) return "No repository open";
        var result = await RunGitCommandAsync($"lfs track \"{pattern}\"", _currentRepoPath!);
        return result.success ? $"Tracking: {pattern}" : $"Failed: {result.output}";
    }

    public async Task<List<string>> GetLfsTrackedPatternsAsync()
    {
        if (_currentRepo == null) return new List<string>();
        var result = await RunGitCommandAsync("lfs track", _currentRepoPath!);
        if (!result.success) return new List<string>();
        return result.output.Split('\n')
            .Where(l => l.Trim().StartsWith("*"))
            .Select(l => l.Trim().TrimStart('*').Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
    }

    // --- Built-in Terminal ---

    public async Task<string> ExecuteTerminalCommandAsync(string command)
    {
        if (string.IsNullOrEmpty(_currentRepoPath)) return "No repository open. Open a repository first.";

        // Safety: only allow git commands
        var trimmed = command.Trim();
        if (!trimmed.StartsWith("git ", StringComparison.OrdinalIgnoreCase) && trimmed != "git")
            return "Only git commands are allowed. Prefix with 'git'.";

        // Remove "git " prefix since we pass it as argument
        var args = trimmed.Length > 4 ? trimmed.Substring(4) : "";
        var result = await RunGitCommandAsync(args, _currentRepoPath);
        return result.output;
    }

    public void Dispose()
    {
        StopHosting();
        _currentRepo?.Dispose();
        _currentRepo = null;
    }

    // === Git Hosting ===
    private HttpListener? _hostListener;
    private bool _isHosting;
    private string? _hostedRepoPath;
    private string? _hostedRepoName;
    private bool _isPrivateHost;
    private string _hostSecretCode = "";
    private readonly HashSet<string> _usedSecretCodes = new();
    private readonly HashSet<string> _authorizedCodes = new();
    private readonly List<(string Name, string Path, bool IsPrivate, string SecretCode)> _hostedRepos = new();

    public bool IsHosting => _isHosting;
    public string HostSecretCode => _hostSecretCode;
    public IReadOnlyList<(string Name, string Path, bool IsPrivate, string SecretCode)> HostedRepos => _hostedRepos;

    public async Task<(bool Success, string Message)> StartHostingAsync(string folderPath, string repoName, bool isPrivate, int port = 9418)
    {
        try
        {
            // Initialize git repo if not already one
            if (!Repository.IsValid(folderPath))
            {
                Repository.Init(folderPath);
            }

            // Configure the repo for smart HTTP
            await RunGitCommandAsync("config http.receivepack true", folderPath);
            await RunGitCommandAsync("update-server-info", folderPath);

            string secretCode = "";
            if (isPrivate)
            {
                secretCode = GenerateSecretCode();
                while (_usedSecretCodes.Contains(secretCode))
                    secretCode = GenerateSecretCode();
                _usedSecretCodes.Add(secretCode);
                _authorizedCodes.Add(secretCode);
            }

            _hostedRepoPath = folderPath;
            _hostedRepoName = repoName;
            _isPrivateHost = isPrivate;
            _hostSecretCode = secretCode;

            // Start HTTP listener for git smart protocol
            _hostListener = new HttpListener();
            _hostListener.Prefixes.Add($"http://*:{port}/");
            _hostListener.Start();
            _isHosting = true;

            _hostedRepos.Add((repoName, folderPath, isPrivate, secretCode));

            // Start serving requests in background
            _ = Task.Run(async () => await ServeGitRequestsAsync());

            string msg = isPrivate
                ? $"Hosting private repo '{repoName}' on port {port}. Secret code: {secretCode}"
                : $"Hosting public repo '{repoName}' on port {port}. URL: http://<your-ip>:{port}/";

            return (true, msg);
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            return (false, "Access denied. Run as Administrator to host git repositories, or use a port above 1024.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to start hosting: {ex.Message}");
        }
    }

    public void StopHosting()
    {
        _isHosting = false;
        try { _hostListener?.Stop(); } catch { }
        try { _hostListener?.Close(); } catch { }
        _hostListener = null;
    }

    public bool ValidateSecretCode(string code)
    {
        return _authorizedCodes.Contains(code);
    }

    private static string GenerateSecretCode()
    {
        string[] words = {
            "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel",
            "india", "juliet", "kilo", "lima", "mike", "november", "oscar", "papa",
            "quebec", "romeo", "sierra", "tango", "uniform", "victor", "whiskey", "xray",
            "yankee", "zulu", "anvil", "beacon", "cipher", "dragon", "ember", "falcon",
            "granite", "harbor", "iron", "jade", "knight", "lancer", "marble", "nexus",
            "orbit", "prism", "quartz", "raven", "steel", "titan", "umbra", "vortex"
        };
        var rng = new Random();
        var selected = new string[5];
        for (int i = 0; i < 5; i++)
            selected[i] = words[rng.Next(words.Length)];
        return string.Join("-", selected);
    }

    private async Task ServeGitRequestsAsync()
    {
        while (_isHosting && _hostListener != null && _hostListener.IsListening)
        {
            try
            {
                var context = await _hostListener.GetContextAsync();
                _ = Task.Run(() => HandleGitRequest(context));
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
        }
    }

    private async Task HandleGitRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url?.AbsolutePath?.TrimStart('/') ?? "";

            // Check auth for private repos
            if (_isPrivateHost)
            {
                var code = request.QueryString["code"] ?? request.Headers["X-Secret-Code"] ?? "";
                if (!string.IsNullOrEmpty(_hostSecretCode) && !_authorizedCodes.Contains(code))
                {
                    response.StatusCode = 403;
                    var forbidden = System.Text.Encoding.UTF8.GetBytes("Access denied");
                    await response.OutputStream.WriteAsync(forbidden);
                    response.Close();
                    return;
                }
            }

            // Route to git smart HTTP backend
            var repo = _hostedRepos.FirstOrDefault();
            if (repo == default)
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            var service = request.QueryString["service"];

            if (path.EndsWith("/info/refs") && !string.IsNullOrEmpty(service))
            {
                // Smart HTTP: advertise refs
                var gitService = service == "git-upload-pack" ? "upload-pack" : "receive-pack";
                var psi = new ProcessStartInfo("git", $"{gitService} --stateless-rpc --advertise-refs \"{repo.Path}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi)!;
                var outputBytes = await ReadAllBytesAsync(proc.StandardOutput.BaseStream);
                await proc.WaitForExitAsync();

                response.ContentType = $"application/x-{service}-advertisement";
                response.StatusCode = 200;

                // Write pkt-line header
                var header = $"# service={service}\n";
                var pktLen = header.Length + 4;
                var pktHeader = $"{pktLen:x4}{header}0000";
                var headerBytes = System.Text.Encoding.UTF8.GetBytes(pktHeader);
                await response.OutputStream.WriteAsync(headerBytes);
                // Write advertised refs
                await response.OutputStream.WriteAsync(outputBytes);
                response.Close();
            }
            else if (path.EndsWith("/git-upload-pack") || path.EndsWith("/git-receive-pack"))
            {
                // Check git push size limit (100MB)
                if (path.EndsWith("/git-receive-pack") && context.Request.ContentLength64 > 100 * 1024 * 1024)
                {
                    response.StatusCode = 413;
                    var errorMsg = System.Text.Encoding.UTF8.GetBytes("Error: Push exceeds server maximum of 100MB. Please reduce the repository size.");
                    await response.OutputStream.WriteAsync(errorMsg);
                    response.Close();
                    return;
                }

                var gitCmd = path.EndsWith("/git-upload-pack") ? "upload-pack" : "receive-pack";
                var psi = new ProcessStartInfo("git", $"{gitCmd} --stateless-rpc \"{repo.Path}\"")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi)!;
                await request.InputStream.CopyToAsync(proc.StandardInput.BaseStream);
                proc.StandardInput.Close();
                response.ContentType = $"application/x-git-{gitCmd}-result";
                response.StatusCode = 200;
                await proc.StandardOutput.BaseStream.CopyToAsync(response.OutputStream);
                await proc.WaitForExitAsync();
                response.Close();
            }
            else
            {
                // Fallback: serve repo info as JSON
                var info = System.Text.Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(new { repo = repo.Name, path = repo.Path, @private = repo.IsPrivate }));
                response.ContentType = "application/json";
                response.StatusCode = 200;
                await response.OutputStream.WriteAsync(info);
                response.Close();
            }
        }
        catch { try { context.Response.Close(); } catch { } }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}

// DTO classes
public class GitFileChange
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsStaged { get; set; }
}

public class GitCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FullMessage { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string DateFormatted => Date.ToString("yyyy-MM-dd HH:mm");
}
