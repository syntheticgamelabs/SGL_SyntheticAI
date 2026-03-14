using System.Diagnostics;

namespace SGL.JudgeDredd.Shared.Helpers;

/// <summary>
/// Cleans browser cookies, trackers, cache files, temp files, and system junk.
/// Provides real cleanup functionality for major browsers and Windows temp locations.
/// </summary>
public static class SystemCleaner
{
    public class CleanResult
    {
        public long BytesFreed { get; set; }
        public int FilesDeleted { get; set; }
        public int FilesFailed { get; set; }
        public List<string> Details { get; } = new();
    }

    /// <summary>
    /// Runs a full system cleanup: browser data, Windows temp, and OS caches.
    /// </summary>
    public static async Task<CleanResult> CleanAllAsync(
        bool cleanCookies = true,
        bool cleanCache = true,
        bool cleanTrackers = true,
        bool cleanTempFiles = true,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new CleanResult();

        if (cleanCache)
        {
            progress?.Report("Cleaning browser cache...");
            await Task.Run(() => CleanBrowserCache(result), ct);
        }

        if (cleanCookies)
        {
            progress?.Report("Cleaning browser cookies...");
            await Task.Run(() => CleanBrowserCookies(result), ct);
        }

        if (cleanTrackers)
        {
            progress?.Report("Cleaning tracking data...");
            await Task.Run(() => CleanTrackers(result), ct);
        }

        if (cleanTempFiles)
        {
            progress?.Report("Cleaning Windows temp files...");
            await Task.Run(() => CleanWindowsTemp(result), ct);
        }

        progress?.Report($"Cleanup complete: {result.FilesDeleted} files removed, {FormatBytes(result.BytesFreed)} freed.");
        return result;
    }

    private static void CleanBrowserCache(CleanResult result)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Chrome cache
        CleanDirectory(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"), result);
        CleanDirectory(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache"), result);
        CleanDirectory(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Service Worker\CacheStorage"), result);

        // Edge cache
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"), result);
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache"), result);

        // Firefox cache
        var firefoxProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                CleanDirectory(Path.Combine(profile, "cache2"), result);
            }
        }

        // Brave cache
        CleanDirectory(Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"), result);

        // Opera cache
        CleanDirectory(Path.Combine(localAppData, @"Opera Software\Opera Stable\Cache"), result);

        result.Details.Add("Browser cache cleaned.");
    }

    private static void CleanBrowserCookies(CleanResult result)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Chrome cookies
        DeleteFile(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cookies"), result);
        DeleteFile(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cookies-journal"), result);

        // Edge cookies
        DeleteFile(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cookies"), result);
        DeleteFile(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cookies-journal"), result);

        // Firefox cookies
        var firefoxProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                DeleteFile(Path.Combine(profile, "cookies.sqlite"), result);
                DeleteFile(Path.Combine(profile, "cookies.sqlite-wal"), result);
            }
        }

        // Brave cookies
        DeleteFile(Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cookies"), result);

        result.Details.Add("Browser cookies cleaned.");
    }

    private static void CleanTrackers(CleanResult result)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Chrome tracking data
        DeleteFile(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\History"), result);
        DeleteFile(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\History-journal"), result);
        CleanDirectory(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Session Storage"), result);
        CleanDirectory(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Local Storage\leveldb"), result);

        // Edge tracking data
        DeleteFile(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\History"), result);
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Session Storage"), result);
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Local Storage\leveldb"), result);

        // Firefox tracking data
        var firefoxProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                DeleteFile(Path.Combine(profile, "places.sqlite"), result);
                DeleteFile(Path.Combine(profile, "formhistory.sqlite"), result);
                CleanDirectory(Path.Combine(profile, "storage", "default"), result);
            }
        }

        // Windows activity history
        CleanDirectory(Path.Combine(localAppData, @"ConnectedDevicesPlatform"), result);

        result.Details.Add("Tracking data cleaned.");
    }

    private static void CleanWindowsTemp(CleanResult result)
    {
        var tempPath = Path.GetTempPath();
        var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // User temp folder
        CleanDirectory(tempPath, result, deleteRoot: false);

        // Windows temp folder
        CleanDirectory(windowsTemp, result, deleteRoot: false);

        // Prefetch
        CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"), result, deleteRoot: false);

        // Recent files shortcuts
        CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Recent)), result, deleteRoot: false);

        // Thumbnail cache
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Windows\Explorer"), result, deleteRoot: false, pattern: "thumbcache_*.db");

        // Windows Error Reports
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportQueue"), result);

        // Windows Update cleanup cache
        CleanDirectory(Path.Combine(localAppData, @"Microsoft\Windows\DeliveryOptimization\Cache"), result);

        result.Details.Add("Windows temporary files cleaned.");
    }

    private static void CleanDirectory(string path, CleanResult result, bool deleteRoot = true, string? pattern = null)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            var files = pattern != null
                ? Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly)
                : Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                DeleteFile(file, result);
            }

            if (deleteRoot && pattern == null)
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        try { Directory.Delete(dir, true); }
                        catch { /* in use */ }
                    }
                }
                catch { /* access denied */ }
            }
        }
        catch { /* access denied to directory */ }
    }

    private static void DeleteFile(string path, CleanResult result)
    {
        if (!File.Exists(path)) return;

        try
        {
            var info = new FileInfo(path);
            var size = info.Length;
            info.Delete();
            result.FilesDeleted++;
            result.BytesFreed += size;
        }
        catch
        {
            result.FilesFailed++;
        }
    }

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
