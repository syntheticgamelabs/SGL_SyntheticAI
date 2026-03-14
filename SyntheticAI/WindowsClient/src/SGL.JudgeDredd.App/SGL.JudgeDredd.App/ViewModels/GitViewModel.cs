using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class GitViewModel : ViewModelBase
{
    private readonly GitService _gitService;

    // --- Repository Selection ---
    [ObservableProperty]
    private string? _selectedRepository;

    [ObservableProperty]
    private string? _selectedBranch;

    [ObservableProperty]
    private string _statusText = "No repository open. Clone, add, or open a local repository to get started.";

    [ObservableProperty]
    private bool _isRepositoryOpen;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _operationProgress;

    [ObservableProperty]
    private string _currentBranchName = string.Empty;

    // --- Clone Dialog ---
    [ObservableProperty]
    private bool _isCloneDialogOpen;

    [ObservableProperty]
    private string _cloneUrl = string.Empty;

    [ObservableProperty]
    private string _cloneLocalName = string.Empty;

    [ObservableProperty]
    private bool _cloneNeedsAuth;

    [ObservableProperty]
    private string _cloneUsername = string.Empty;

    private string _clonePassword = string.Empty;
    public string ClonePassword
    {
        get => _clonePassword;
        set => SetProperty(ref _clonePassword, value);
    }

    // --- New Repo Dialog ---
    [ObservableProperty]
    private bool _isNewRepoDialogOpen;

    [ObservableProperty]
    private string _newRepoName = string.Empty;

    // --- Commit ---
    [ObservableProperty]
    private string _commitMessage = string.Empty;

    // --- Fetch Info ---
    [ObservableProperty]
    private string _lastFetchTime = "Never";

    [ObservableProperty]
    private string _lastFetchResult = string.Empty;

    // --- History selected ---
    [ObservableProperty]
    private GitCommitInfo? _selectedCommit;

    // --- Active tab inside Git panel ---
    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<string> AvailableRepositories { get; } = [];
    public ObservableCollection<string> LocalBranches { get; } = [];
    public ObservableCollection<string> RemoteBranches { get; } = [];
    public ObservableCollection<GitFileChange> PendingChanges { get; } = [];
    public ObservableCollection<GitCommitInfo> CommitHistory { get; } = [];
    public ObservableCollection<string> ActivityLog { get; } = [];

    // --- Host Feature ---
    [ObservableProperty] private bool _isHostDialogOpen;
    [ObservableProperty] private string _hostFolderPath = "";
    [ObservableProperty] private string _hostRepoName = "";
    [ObservableProperty] private bool _hostIsPrivate;
    [ObservableProperty] private string _hostSecretCode = "";
    [ObservableProperty] private bool _isHosting;
    [ObservableProperty] private string _hostStatusText = "";
    [ObservableProperty] private int _hostPort = 9418;

    // Secret code entry for accessing private hosted repos
    [ObservableProperty] private string _secretCodeEntry = "";

    public ObservableCollection<HostedRepoInfo> LiveHostedRepos { get; } = new();

    // --- Clone URL ---
    [ObservableProperty]
    private string _selectedCloneProtocol = "HTTPS";

    [ObservableProperty]
    private string _generatedCloneUrl = "";

    // --- Git LFS ---
    [ObservableProperty]
    private bool _isLfsEnabled;

    [ObservableProperty]
    private ObservableCollection<string> _lfsTrackedPatterns = new();

    // --- Terminal ---
    [ObservableProperty]
    private string _terminalOutput = "";

    [ObservableProperty]
    private string _terminalCommand = "";

    public GitViewModel()
    {
        Title = "Git";
        _gitService = new GitService();
        _gitService.StatusChanged += (_, msg) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusText = msg;
                AddLog(msg);
            });
        };
        _gitService.ProgressChanged += (_, pct) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                OperationProgress = pct * 100;
            });
        };

        RefreshRepositoryList();
    }

    // ===================================================================
    // Repository Management
    // ===================================================================

    [RelayCommand]
    private void OpenCloneDialog()
    {
        CloneUrl = string.Empty;
        CloneLocalName = string.Empty;
        CloneNeedsAuth = false;
        CloneUsername = string.Empty;
        ClonePassword = string.Empty;
        IsCloneDialogOpen = true;
    }

    [RelayCommand]
    private async Task ExecuteCloneAsync()
    {
        if (string.IsNullOrWhiteSpace(CloneUrl))
        {
            StatusText = "Please enter a repository URL.";
            return;
        }

        IsCloneDialogOpen = false;
        IsBusy = true;
        OperationProgress = 0;

        try
        {
            if (CloneNeedsAuth && !string.IsNullOrWhiteSpace(CloneUsername))
            {
                _gitService.SetCredentials(CloneUsername, ClonePassword);
            }

            var localName = string.IsNullOrWhiteSpace(CloneLocalName) ? null : CloneLocalName;
            await _gitService.CloneRepositoryAsync(CloneUrl, localName);

            RefreshRepositoryList();

            // Auto-open the cloned repo
            var repoName = localName ?? Path.GetFileNameWithoutExtension(CloneUrl.TrimEnd('/').Replace(".git", ""));
            if (AvailableRepositories.Contains(repoName))
            {
                SelectedRepository = repoName;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Clone failed: {ex.Message}";
            AddLog($"ERROR: Clone failed - {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _gitService.ClearCredentials();
        }
    }

    [RelayCommand]
    private void OpenNewRepoDialog()
    {
        NewRepoName = string.Empty;
        IsNewRepoDialogOpen = true;
    }

    [RelayCommand]
    private void ExecuteNewRepo()
    {
        if (string.IsNullOrWhiteSpace(NewRepoName))
        {
            StatusText = "Please enter a repository name.";
            return;
        }

        IsNewRepoDialogOpen = false;

        try
        {
            _gitService.InitNewRepository(NewRepoName);
            RefreshRepositoryList();
            SelectedRepository = NewRepoName;
        }
        catch (Exception ex)
        {
            StatusText = $"Init failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLocalRepository()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a folder containing a .git repository",
            Filter = "All files|*.*",
            CheckFileExists = false,
            FileName = "Select Folder"
        };

        // Use folder browser via FolderBrowserDialog workaround
        var folderDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a Git repository folder",
            ShowNewFolderButton = false
        };

        if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                _gitService.OpenLocalRepository(folderDialog.SelectedPath);
                IsRepositoryOpen = true;
                CurrentBranchName = _gitService.CurrentBranch ?? "HEAD";
                RefreshBranches();
                RefreshChanges();
                RefreshHistory();
            }
            catch (Exception ex)
            {
                StatusText = $"Not a valid Git repository: {ex.Message}";
            }
        }
    }

    partial void OnSelectedRepositoryChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        try
        {
            _gitService.OpenRepository(value);
            IsRepositoryOpen = true;
            CurrentBranchName = _gitService.CurrentBranch ?? "HEAD";
            RefreshBranches();
            RefreshChanges();
            RefreshHistory();
            UpdateCloneUrl();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open: {ex.Message}";
            IsRepositoryOpen = false;
        }
    }

    partial void OnSelectedCloneProtocolChanged(string value) => UpdateCloneUrl();

    // ===================================================================
    // Branch Operations
    // ===================================================================

    partial void OnSelectedBranchChanged(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsRepositoryOpen) return;
        if (value == CurrentBranchName) return;

        try
        {
            _gitService.SwitchBranch(value);
            CurrentBranchName = _gitService.CurrentBranch ?? value;
            RefreshChanges();
            RefreshHistory();
        }
        catch (Exception ex)
        {
            StatusText = $"Branch switch failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateBranch()
    {
        var dialog = new Window
        {
            Title = "Create New Branch",
            Width = 400, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 46)),
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Branch name:",
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 12) };
        var createBtn = new System.Windows.Controls.Button
        {
            Content = "Create & Switch",
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        createBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    _gitService.CreateBranch(textBox.Text, true);
                    CurrentBranchName = _gitService.CurrentBranch ?? textBox.Text;
                    RefreshBranches();
                    RefreshChanges();
                    RefreshHistory();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    StatusText = $"Create branch failed: {ex.Message}";
                }
            }
        };

        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(createBtn);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void MergeToMain()
    {
        if (!IsRepositoryOpen) return;

        var mainBranch = LocalBranches.Contains("main") ? "main" :
                         LocalBranches.Contains("master") ? "master" : null;

        if (mainBranch == null)
        {
            StatusText = "No 'main' or 'master' branch found.";
            return;
        }

        if (CurrentBranchName == mainBranch)
        {
            StatusText = "Already on the main branch.";
            return;
        }

        // Switch to main, then merge the current branch
        var sourceBranch = CurrentBranchName;
        try
        {
            _gitService.SwitchBranch(mainBranch);
            CurrentBranchName = mainBranch;
            var result = _gitService.MergeBranch(sourceBranch);
            StatusText = result;
            RefreshBranches();
            RefreshChanges();
            RefreshHistory();
        }
        catch (Exception ex)
        {
            StatusText = $"Merge failed: {ex.Message}";
        }
    }

    // ===================================================================
    // Fetch / Pull / Push
    // ===================================================================

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (!IsRepositoryOpen) return;
        IsBusy = true;
        OperationProgress = 0;

        try
        {
            if (CloneNeedsAuth && !string.IsNullOrWhiteSpace(CloneUsername))
                _gitService.SetCredentials(CloneUsername, ClonePassword);

            var result = await _gitService.FetchAsync();
            LastFetchTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LastFetchResult = result;
        }
        catch (Exception ex)
        {
            StatusText = $"Fetch failed: {ex.Message}";
            AddLog($"ERROR: Fetch - {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _gitService.ClearCredentials();
        }
    }

    [RelayCommand]
    private async Task PullAsync()
    {
        if (!IsRepositoryOpen) return;
        IsBusy = true;

        try
        {
            if (CloneNeedsAuth && !string.IsNullOrWhiteSpace(CloneUsername))
                _gitService.SetCredentials(CloneUsername, ClonePassword);

            var result = await _gitService.PullAsync();
            LastFetchTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LastFetchResult = result;
            RefreshChanges();
            RefreshHistory();
        }
        catch (Exception ex)
        {
            StatusText = $"Pull failed: {ex.Message}";
            AddLog($"ERROR: Pull - {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _gitService.ClearCredentials();
        }
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        if (!IsRepositoryOpen) return;
        IsBusy = true;

        try
        {
            if (CloneNeedsAuth && !string.IsNullOrWhiteSpace(CloneUsername))
                _gitService.SetCredentials(CloneUsername, ClonePassword);

            await _gitService.PushAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Push failed: {ex.Message}";
            AddLog($"ERROR: Push - {ex.Message}");

            // If it's an auth failure, prompt for credentials
            if (ex.Message.Contains("401") || ex.Message.Contains("403") ||
                ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
            {
                CloneNeedsAuth = true;
                IsCloneDialogOpen = true;
                StatusText = "Authentication required. Enter credentials and retry.";
            }
        }
        finally
        {
            IsBusy = false;
            _gitService.ClearCredentials();
        }
    }

    // ===================================================================
    // Commit
    // ===================================================================

    [RelayCommand]
    private void StageAll()
    {
        if (!IsRepositoryOpen) return;
        try
        {
            _gitService.StageAll();
            RefreshChanges();
        }
        catch (Exception ex)
        {
            StatusText = $"Stage failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StageFile(GitFileChange? change)
    {
        if (!IsRepositoryOpen || change == null) return;
        try
        {
            _gitService.StageFile(change.FilePath);
            RefreshChanges();
        }
        catch (Exception ex)
        {
            StatusText = $"Stage failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CommitChanges()
    {
        if (!IsRepositoryOpen) return;
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            StatusText = "Please enter a commit message.";
            return;
        }

        try
        {
            var result = _gitService.Commit(CommitMessage);
            CommitMessage = string.Empty;
            RefreshChanges();
            RefreshHistory();
            StatusText = result;
        }
        catch (Exception ex)
        {
            StatusText = $"Commit failed: {ex.Message}";
            AddLog($"ERROR: Commit - {ex.Message}");
        }
    }

    // ===================================================================
    // History
    // ===================================================================

    [RelayCommand]
    private void RevertToCommit(GitCommitInfo? commit)
    {
        if (!IsRepositoryOpen || commit == null) return;

        var result = MessageBox.Show(
            $"Revert to commit {commit.ShortSha}?\n\n{commit.Message}\n\nThis will discard all changes after this commit.",
            "Confirm Revert",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _gitService.RevertToCommit(commit.Sha);
            RefreshChanges();
            RefreshHistory();
        }
        catch (Exception ex)
        {
            StatusText = $"Revert failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateBranchFromCommit(GitCommitInfo? commit)
    {
        if (!IsRepositoryOpen || commit == null) return;

        var dialog = new Window
        {
            Title = $"Create Branch from {commit.ShortSha}",
            Width = 400, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 46)),
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Branch name:",
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 12) };
        var btn = new System.Windows.Controls.Button
        {
            Content = "Create Branch",
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        btn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    // First revert to that commit, then create branch
                    _gitService.RevertToCommit(commit.Sha);
                    _gitService.CreateBranch(textBox.Text, true);
                    CurrentBranchName = _gitService.CurrentBranch ?? textBox.Text;
                    RefreshBranches();
                    RefreshChanges();
                    RefreshHistory();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    StatusText = $"Failed: {ex.Message}";
                }
            }
        };

        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(btn);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    // ===================================================================
    // Clone URL
    // ===================================================================

    private void UpdateCloneUrl()
    {
        if (string.IsNullOrEmpty(SelectedRepository)) { GeneratedCloneUrl = ""; return; }
        var repoPath = _gitService.GetRepositoryPath(SelectedRepository);
        if (SelectedCloneProtocol == "SSH")
            GeneratedCloneUrl = $"git@{GetLocalIP()}:{SelectedRepository}.git";
        else
            GeneratedCloneUrl = $"http://{GetLocalIP()}:9418/{SelectedRepository}.git";
    }

    [RelayCommand]
    private void CopyCloneUrl()
    {
        if (!string.IsNullOrEmpty(GeneratedCloneUrl))
            System.Windows.Clipboard.SetText(GeneratedCloneUrl);
    }

    private string GetLocalIP()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            return host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "localhost";
        }
        catch { return "localhost"; }
    }

    // ===================================================================
    // Git LFS
    // ===================================================================

    [RelayCommand]
    private async Task InitLfs()
    {
        var result = await _gitService.InitializeLfsAsync();
        IsLfsEnabled = await _gitService.IsGitLfsInstalledAsync();
        AddLog(result);
        await RefreshLfsPatterns();
    }

    [RelayCommand]
    private async Task AddLfsPattern()
    {
        var dialog = new Window
        {
            Title = "Track LFS Pattern",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 46)),
        };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock
        {
            Text = "Pattern (e.g., *.psd, *.zip):",
            Foreground = System.Windows.Media.Brushes.White
        });
        var tb = new TextBox { Margin = new Thickness(0, 8, 0, 8) };
        sp.Children.Add(tb);
        var btn = new Button
        {
            Content = "Add",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(16, 6, 16, 6)
        };
        btn.Click += (s, e) => dialog.DialogResult = true;
        sp.Children.Add(btn);
        dialog.Content = sp;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(tb.Text))
        {
            var result = await _gitService.TrackLfsPatternAsync(tb.Text.Trim());
            AddLog(result);
            await RefreshLfsPatterns();
        }
    }

    private async Task RefreshLfsPatterns()
    {
        var patterns = await _gitService.GetLfsTrackedPatternsAsync();
        LfsTrackedPatterns.Clear();
        foreach (var p in patterns) LfsTrackedPatterns.Add(p);
    }

    // ===================================================================
    // Terminal
    // ===================================================================

    [RelayCommand]
    private async Task ExecuteTerminalCommand()
    {
        if (string.IsNullOrWhiteSpace(TerminalCommand)) return;
        var cmd = TerminalCommand.Trim();
        TerminalOutput += $"\n$ {cmd}\n";
        var result = await _gitService.ExecuteTerminalCommandAsync(cmd);
        TerminalOutput += result + "\n";
        TerminalCommand = "";
        AddLog($"Terminal: {cmd}");
    }

    // ===================================================================
    // Refresh Helpers
    // ===================================================================

    private void RefreshRepositoryList()
    {
        AvailableRepositories.Clear();
        foreach (var repo in _gitService.GetAvailableRepositories())
            AvailableRepositories.Add(repo);
    }

    private void RefreshBranches()
    {
        LocalBranches.Clear();
        foreach (var b in _gitService.GetLocalBranches())
            LocalBranches.Add(b);

        RemoteBranches.Clear();
        foreach (var b in _gitService.GetRemoteBranches())
            RemoteBranches.Add(b);

        SelectedBranch = CurrentBranchName;
    }

    private void RefreshChanges()
    {
        PendingChanges.Clear();
        foreach (var c in _gitService.GetUnstagedChanges())
            PendingChanges.Add(c);
    }

    private void RefreshHistory()
    {
        CommitHistory.Clear();
        foreach (var c in _gitService.GetCommitHistory(100))
            CommitHistory.Add(c);
    }

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            ActivityLog.Insert(0, entry);
            while (ActivityLog.Count > 200) ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ActivityLog.Insert(0, entry);
                while (ActivityLog.Count > 200) ActivityLog.RemoveAt(ActivityLog.Count - 1);
            });
        }
    }

    // ===================================================================
    // Host Operations
    // ===================================================================

    [RelayCommand]
    private void OpenHostDialog()
    {
        IsHostDialogOpen = true;
        HostRepoName = "";
        HostFolderPath = "";
        HostIsPrivate = false;
        HostSecretCode = "";
    }

    [RelayCommand]
    private void SelectHostFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to host as Git repository"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            HostFolderPath = dialog.SelectedPath;
            if (string.IsNullOrEmpty(HostRepoName))
                HostRepoName = System.IO.Path.GetFileName(dialog.SelectedPath);
        }
    }

    [RelayCommand]
    private async Task StartHostingAsync()
    {
        if (string.IsNullOrWhiteSpace(HostFolderPath) || string.IsNullOrWhiteSpace(HostRepoName))
        {
            StatusText = "Please select a folder and enter a repository name.";
            return;
        }

        var result = await _gitService.StartHostingAsync(HostFolderPath, HostRepoName, HostIsPrivate, HostPort);
        if (result.Success)
        {
            IsHosting = true;
            HostSecretCode = _gitService.HostSecretCode;
            HostStatusText = result.Message;
            IsHostDialogOpen = false;
            AddLog($"HOSTING: {result.Message}");

            LiveHostedRepos.Add(new HostedRepoInfo
            {
                Name = HostRepoName,
                Path = HostFolderPath,
                IsPrivate = HostIsPrivate,
                SecretCode = HostSecretCode,
                Port = HostPort
            });
        }
        else
        {
            StatusText = result.Message;
            AddLog($"HOST FAILED: {result.Message}");
        }
    }

    [RelayCommand]
    private void StopHosting()
    {
        _gitService.StopHosting();
        IsHosting = false;
        HostStatusText = "Hosting stopped.";
        LiveHostedRepos.Clear();
        AddLog("Hosting stopped.");
    }

    [RelayCommand]
    private void CancelHostDialog()
    {
        IsHostDialogOpen = false;
    }

    [RelayCommand]
    private void AccessPrivateRepo()
    {
        if (string.IsNullOrWhiteSpace(SecretCodeEntry))
        {
            StatusText = "Enter a secret code to access a private hosted repo.";
            return;
        }

        if (_gitService.ValidateSecretCode(SecretCodeEntry))
        {
            StatusText = "Access granted! You can now clone this repository.";
            AddLog($"Secret code validated: access granted.");
        }
        else
        {
            StatusText = "Invalid secret code. Access denied.";
            AddLog($"Secret code rejected: access denied.");
        }
    }
}

public class HostedRepoInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsPrivate { get; set; }
    public string SecretCode { get; set; } = "";
    public int Port { get; set; }
}
