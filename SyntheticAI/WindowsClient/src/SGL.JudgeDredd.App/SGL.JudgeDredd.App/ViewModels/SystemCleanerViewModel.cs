using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Shared.Helpers;

namespace SGL.JudgeDredd.App.ViewModels;

/// <summary>
/// ViewModel for the System Cleaner view. Provides analysis and cleanup of
/// browser data, Windows temp files, prefetch, thumbnail cache, recent files,
/// and error reports through toggleable categories.
/// </summary>
public partial class SystemCleanerViewModel : ViewModelBase
{
    private CancellationTokenSource? _cleanCts;

    // ── Category toggles ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _browserCookiesSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _browserCacheSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _browserHistorySelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _windowsTempSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _prefetchSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _thumbnailCacheSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _recentFilesSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCleanable))]
    private bool _errorReportsSelected = true;

    // ── Estimated sizes per category ────────────────────────────────────

    [ObservableProperty]
    private long _browserCookiesSize;

    [ObservableProperty]
    private long _browserCacheSize;

    [ObservableProperty]
    private long _browserHistorySize;

    [ObservableProperty]
    private long _windowsTempSize;

    [ObservableProperty]
    private long _prefetchSize;

    [ObservableProperty]
    private long _thumbnailCacheSize;

    [ObservableProperty]
    private long _recentFilesSize;

    [ObservableProperty]
    private long _errorReportsSize;

    // ── Formatted size display strings ──────────────────────────────────

    public string BrowserCookiesSizeDisplay => FormatBytes(BrowserCookiesSize);
    public string BrowserCacheSizeDisplay => FormatBytes(BrowserCacheSize);
    public string BrowserHistorySizeDisplay => FormatBytes(BrowserHistorySize);
    public string WindowsTempSizeDisplay => FormatBytes(WindowsTempSize);
    public string PrefetchSizeDisplay => FormatBytes(PrefetchSize);
    public string ThumbnailCacheSizeDisplay => FormatBytes(ThumbnailCacheSize);
    public string RecentFilesSizeDisplay => FormatBytes(RecentFilesSize);
    public string ErrorReportsSizeDisplay => FormatBytes(ErrorReportsSize);

    // ── Progress / state ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasAnalyzed;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _currentOperation = string.Empty;

    // ── Results ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private long _bytesFreed;

    [ObservableProperty]
    private int _filesDeleted;

    [ObservableProperty]
    private int _filesFailed;

    [ObservableProperty]
    private bool _hasResults;

    public ObservableCollection<string> Details { get; } = [];

    public string BytesFreedDisplay => FormatBytes(BytesFreed);

    /// <summary>
    /// Sum of estimated sizes for all currently selected categories.
    /// </summary>
    public long TotalCleanable
    {
        get
        {
            long total = 0;
            if (BrowserCookiesSelected) total += BrowserCookiesSize;
            if (BrowserCacheSelected) total += BrowserCacheSize;
            if (BrowserHistorySelected) total += BrowserHistorySize;
            if (WindowsTempSelected) total += WindowsTempSize;
            if (PrefetchSelected) total += PrefetchSize;
            if (ThumbnailCacheSelected) total += ThumbnailCacheSize;
            if (RecentFilesSelected) total += RecentFilesSize;
            if (ErrorReportsSelected) total += ErrorReportsSize;
            return total;
        }
    }

    public string TotalCleanableDisplay => FormatBytes(TotalCleanable);

    // ── Constructor ─────────────────────────────────────────────────────

    public SystemCleanerViewModel()
    {
        Title = "System Cleaner";
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        HasResults = false;
        ProgressPercent = 0;
        CurrentOperation = "Analyzing system...";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        await AvatarViewModel.Instance.ShowSpeechBubble("Analyzing junk files...");

        try
        {
            await Task.Run(() =>
            {
                BrowserCookiesSize = CalculateDirectorySize(GetBrowserCookiePaths());
                OnPropertyChanged(nameof(BrowserCookiesSizeDisplay));
                ProgressPercent = 12.5;

                BrowserCacheSize = CalculateDirectorySize(GetBrowserCachePaths());
                OnPropertyChanged(nameof(BrowserCacheSizeDisplay));
                ProgressPercent = 25;

                BrowserHistorySize = CalculateDirectorySize(GetBrowserHistoryPaths());
                OnPropertyChanged(nameof(BrowserHistorySizeDisplay));
                ProgressPercent = 37.5;

                WindowsTempSize = CalculateDirectorySize(GetWindowsTempPaths());
                OnPropertyChanged(nameof(WindowsTempSizeDisplay));
                ProgressPercent = 50;

                PrefetchSize = CalculateDirectorySize(GetPrefetchPaths());
                OnPropertyChanged(nameof(PrefetchSizeDisplay));
                ProgressPercent = 62.5;

                ThumbnailCacheSize = CalculateDirectorySize(GetThumbnailCachePaths());
                OnPropertyChanged(nameof(ThumbnailCacheSizeDisplay));
                ProgressPercent = 75;

                RecentFilesSize = CalculateDirectorySize(GetRecentFilesPaths());
                OnPropertyChanged(nameof(RecentFilesSizeDisplay));
                ProgressPercent = 87.5;

                ErrorReportsSize = CalculateDirectorySize(GetErrorReportsPaths());
                OnPropertyChanged(nameof(ErrorReportsSizeDisplay));
                ProgressPercent = 100;
            });

            OnPropertyChanged(nameof(TotalCleanable));
            OnPropertyChanged(nameof(TotalCleanableDisplay));
            HasAnalyzed = true;
            CurrentOperation = "Analysis complete.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Found {FormatBytes(TotalCleanable)} of cleanable files.");
        }
        catch (Exception)
        {
            CurrentOperation = "Analysis failed.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Analysis encountered an error.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task CleanNowAsync()
    {
        _cleanCts = new CancellationTokenSource();
        IsBusy = true;
        HasResults = false;
        Details.Clear();
        ProgressPercent = 0;
        BytesFreed = 0;
        FilesDeleted = 0;
        FilesFailed = 0;

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Cleaning system junk...");

        var progress = new Progress<string>(message =>
        {
            CurrentOperation = message;
        });

        try
        {
            var result = await SystemCleaner.CleanAllAsync(
                cleanCookies: BrowserCookiesSelected,
                cleanCache: BrowserCacheSelected,
                cleanTrackers: BrowserHistorySelected,
                cleanTempFiles: WindowsTempSelected || PrefetchSelected ||
                                ThumbnailCacheSelected || RecentFilesSelected ||
                                ErrorReportsSelected,
                progress: progress,
                ct: _cleanCts.Token);

            BytesFreed = result.BytesFreed;
            FilesDeleted = result.FilesDeleted;
            FilesFailed = result.FilesFailed;

            foreach (var detail in result.Details)
            {
                Details.Add(detail);
            }

            OnPropertyChanged(nameof(BytesFreedDisplay));
            HasResults = true;
            ProgressPercent = 100;
            CurrentOperation = $"Cleanup complete: {FilesDeleted} files removed, {FormatBytes(BytesFreed)} freed.";

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Done! Freed {FormatBytes(BytesFreed)} across {FilesDeleted} files.");

            // Re-analyze to refresh estimated sizes after cleaning.
            HasAnalyzed = false;
        }
        catch (OperationCanceledException)
        {
            CurrentOperation = "Cleaning was cancelled.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Cleaning was cancelled.");
        }
        catch (Exception)
        {
            CurrentOperation = "Cleaning encountered an error.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Cleaning encountered an error.");
        }
        finally
        {
            IsBusy = false;
            _cleanCts?.Dispose();
            _cleanCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cleanCts?.Cancel();
    }

    [RelayCommand]
    private async Task WipeLlmMemoryAsync()
    {
        try
        {
            var memPath = Path.Combine(AppContext.BaseDirectory, "data", "llm_memory.json");
            if (File.Exists(memPath))
            {
                await File.WriteAllTextAsync(memPath, "{\"memories\":[]}");
                CurrentOperation = "LLM memory wiped successfully.";
                AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
                await AvatarViewModel.Instance.ShowSpeechBubble("AI memory has been wiped clean. I won't remember previous conversations.");
            }
            else
            {
                CurrentOperation = "No LLM memory file found.";
            }
        }
        catch (Exception ex)
        {
            CurrentOperation = $"Failed to wipe LLM memory: {ex.Message}";
        }
    }

    private bool CanExecuteAction() => !IsBusy;
    private bool CanCancel() => IsBusy;

    // ── Size estimation helpers ─────────────────────────────────────────

    private static long CalculateDirectorySize(IEnumerable<string> paths)
    {
        long total = 0;
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    total += new FileInfo(path).Length;
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { total += new FileInfo(file).Length; }
                        catch { /* access denied */ }
                    }
                }
            }
            catch { /* access denied */ }
        }
        return total;
    }

    // ── Path builders for each category ─────────────────────────────────

    private static IEnumerable<string> GetBrowserCookiePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new List<string>
        {
            Path.Combine(local, @"Google\Chrome\User Data\Default\Cookies"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Cookies-journal"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cookies"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cookies-journal"),
            Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cookies"),
        };

        var firefoxProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                paths.Add(Path.Combine(profile, "cookies.sqlite"));
                paths.Add(Path.Combine(profile, "cookies.sqlite-wal"));
            }
        }

        return paths;
    }

    private static IEnumerable<string> GetBrowserCachePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new List<string>
        {
            Path.Combine(local, @"Google\Chrome\User Data\Default\Cache"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Service Worker\CacheStorage"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache"),
            Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
            Path.Combine(local, @"Opera Software\Opera Stable\Cache"),
        };

        var firefoxProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                paths.Add(Path.Combine(profile, "cache2"));
            }
        }

        return paths;
    }

    private static IEnumerable<string> GetBrowserHistoryPaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new List<string>
        {
            Path.Combine(local, @"Google\Chrome\User Data\Default\History"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\History-journal"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Session Storage"),
            Path.Combine(local, @"Google\Chrome\User Data\Default\Local Storage\leveldb"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\History"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Session Storage"),
            Path.Combine(local, @"Microsoft\Edge\User Data\Default\Local Storage\leveldb"),
            Path.Combine(local, @"ConnectedDevicesPlatform"),
        };

        var firefoxProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                paths.Add(Path.Combine(profile, "places.sqlite"));
                paths.Add(Path.Combine(profile, "formhistory.sqlite"));
                paths.Add(Path.Combine(profile, "storage", "default"));
            }
        }

        return paths;
    }

    private static IEnumerable<string> GetWindowsTempPaths()
    {
        return new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        };
    }

    private static IEnumerable<string> GetPrefetchPaths()
    {
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"),
        };
    }

    private static IEnumerable<string> GetThumbnailCachePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var explorerDir = Path.Combine(local, @"Microsoft\Windows\Explorer");
        var paths = new List<string>();

        if (Directory.Exists(explorerDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(explorerDir, "thumbcache_*.db"))
                {
                    paths.Add(file);
                }
            }
            catch { /* access denied */ }
        }

        return paths;
    }

    private static IEnumerable<string> GetRecentFilesPaths()
    {
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Recent),
        };
    }

    private static IEnumerable<string> GetErrorReportsPaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Path.Combine(local, @"Microsoft\Windows\WER\ReportQueue"),
            Path.Combine(local, @"Microsoft\Windows\DeliveryOptimization\Cache"),
        };
    }

    // ── Formatting ──────────────────────────────────────────────────────

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
