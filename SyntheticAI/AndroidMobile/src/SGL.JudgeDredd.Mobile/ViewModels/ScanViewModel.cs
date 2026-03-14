using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private readonly ThreatKnowledgeService _knowledgeService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty] private string _selectedScanType = "Quick Scan";
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _scanStatusText = "Ready to scan";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _scanComplete;
    [ObservableProperty] private int _filesScanned;
    [ObservableProperty] private int _threatsFound;
    [ObservableProperty] private string _currentFile = string.Empty;

    public ObservableCollection<ScanResultItem> Results { get; } = new();

    public List<string> ScanTypes { get; } = new()
    {
        "Quick Scan",
        "Full Scan",
        "Custom Scan"
    };

    // Known suspicious package prefixes / signatures
    private static readonly HashSet<string> SuspiciousPermissions = new()
    {
        "android.permission.SEND_SMS",
        "android.permission.READ_SMS",
        "android.permission.RECEIVE_SMS",
        "android.permission.READ_CALL_LOG",
        "android.permission.PROCESS_OUTGOING_CALLS",
        "android.permission.READ_CONTACTS",
        "android.permission.RECORD_AUDIO",
        "android.permission.CAMERA",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_BACKGROUND_LOCATION",
        "android.permission.SYSTEM_ALERT_WINDOW",
        "android.permission.BIND_ACCESSIBILITY_SERVICE",
        "android.permission.BIND_DEVICE_ADMIN",
        "android.permission.REQUEST_INSTALL_PACKAGES"
    };

    private static readonly HashSet<string> KnownMalwarePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.psiphon3", "com.psiphon3.subscription",
        "org.nicksware", "org.teamsik",
        "com.snaptube.premium", "com.tube.video.downloader",
        "com.freetools.cleaner", "com.apps.go.cleaner",
        "com.sec.android.app.verityrogue",
        "com.antiy.avl.fakeav", "com.geinimi",
        "com.svpeng.trojan", "com.acecard.trojan",
        "com.hummingbad.malware"
    };

    public ScanViewModel(ApiClient apiClient, AppPreferences prefs, ThreatKnowledgeService knowledgeService)
    {
        _apiClient = apiClient;
        _prefs = prefs;
        _knowledgeService = knowledgeService;
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanComplete = false;
        ScanProgress = 0;
        FilesScanned = 0;
        ThreatsFound = 0;
        Results.Clear();

        _scanCts = new CancellationTokenSource();

        try
        {
            ScanStatusText = $"Starting {SelectedScanType}...";

#if ANDROID
            await ScanInstalledAppsAsync(_scanCts.Token);

            if (SelectedScanType != "Quick Scan" && !_scanCts.Token.IsCancellationRequested)
            {
                await ScanFileSystemAsync(_scanCts.Token);
            }

            if (SelectedScanType == "Full Scan" && !_scanCts.Token.IsCancellationRequested)
            {
                await ScanRunningServicesAsync(_scanCts.Token);
            }
#else
            ScanStatusText = "Scanning not available on this platform.";
#endif

            if (!_scanCts.Token.IsCancellationRequested)
            {
                ScanProgress = 1.0;
                ScanStatusText = $"Scan complete. {FilesScanned} items scanned, {ThreatsFound} threats found.";
                ScanComplete = true;
                _prefs.LastScanTime = DateTime.Now.ToString("g");

                await _apiClient.SendHeartbeatAsync(
                    true, _prefs.ThreatsBlocked, FilesScanned, 0);
            }
        }
        catch (TaskCanceledException)
        {
            ScanStatusText = "Scan cancelled";
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            CurrentFile = string.Empty;
        }
    }

#if ANDROID
    private async Task ScanInstalledAppsAsync(CancellationToken ct)
    {
        ScanStatusText = "Scanning installed applications...";

        var context = Android.App.Application.Context;
        var pm = context.PackageManager;
        if (pm == null) return;

        var packages = pm.GetInstalledPackages(Android.Content.PM.PackageInfoFlags.Permissions);
        if (packages == null) return;

        int total = packages.Count;
        int processed = 0;

        foreach (var pkg in packages)
        {
            ct.ThrowIfCancellationRequested();

            if (pkg.PackageName == null) continue;

            processed++;
            FilesScanned = processed;
            ScanProgress = SelectedScanType == "Quick Scan"
                ? (double)processed / total
                : (double)processed / total * 0.5; // 50% of progress for app scan in full mode

            CurrentFile = pkg.PackageName;
            ScanStatusText = $"Scanning: {pkg.PackageName}";

            // Check against known malware packages
            if (KnownMalwarePackages.Contains(pkg.PackageName))
            {
                var appName = pkg.ApplicationInfo?.LoadLabel(pm)?.ToString() ?? pkg.PackageName;
                AddThreat(appName, pkg.PackageName, "Android.Malware.KnownBad", "High");
                continue;
            }

            // Analyze permissions for suspicious behavior
            var requestedPerms = pkg.RequestedPermissions ?? Array.Empty<string>();
            var suspiciousPerms = requestedPerms.Where(p => SuspiciousPermissions.Contains(p)).ToList();

            if (suspiciousPerms.Count >= 4)
            {
                var appInfo = pkg.ApplicationInfo;
                bool isSystem = appInfo != null &&
                    (appInfo.Flags & Android.Content.PM.ApplicationInfoFlags.System) != 0;

                if (!isSystem)
                {
                    var appName = appInfo?.LoadLabel(pm)?.ToString() ?? pkg.PackageName;
                    string severity = suspiciousPerms.Count >= 6 ? "High" : "Medium";
                    string threatName = suspiciousPerms.Any(p => p.Contains("SEND_SMS"))
                        ? "Android.PUA.SmsSender"
                        : suspiciousPerms.Any(p => p.Contains("BIND_DEVICE_ADMIN"))
                            ? "Android.PUA.DeviceAdmin"
                            : suspiciousPerms.Any(p => p.Contains("BIND_ACCESSIBILITY"))
                                ? "Android.PUA.AccessibilityAbuse"
                                : "Android.PUA.SuspiciousPerms";

                    AddThreat(appName, pkg.PackageName, threatName, severity);

                    await _knowledgeService.AddThreatEntryAsync(new ThreatKnowledgeEntry
                    {
                        ThreatType = "suspicious_app",
                        ThreatName = threatName,
                        FilePath = pkg.PackageName,
                        Description = $"{appName}: {suspiciousPerms.Count} suspicious permissions",
                        Severity = severity,
                        Permissions = suspiciousPerms,
                        Source = "app_scan"
                    });
                }
            }

            // Small delay to keep UI responsive
            if (processed % 10 == 0)
                await Task.Delay(1, ct);
        }
    }

    private async Task ScanFileSystemAsync(CancellationToken ct)
    {
        ScanStatusText = "Scanning file system...";

        var scanDirs = new List<string>();

        // Get real directories
        var extStorage = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(extStorage))
        {
            var downloadDir = Path.Combine(extStorage, "Download");
            if (Directory.Exists(downloadDir)) scanDirs.Add(downloadDir);

            var dcimDir = Path.Combine(extStorage, "DCIM");
            if (Directory.Exists(dcimDir)) scanDirs.Add(dcimDir);

            var documentsDir = Path.Combine(extStorage, "Documents");
            if (Directory.Exists(documentsDir)) scanDirs.Add(documentsDir);

            var androidDataDir = Path.Combine(extStorage, "Android", "data");
            if (Directory.Exists(androidDataDir)) scanDirs.Add(androidDataDir);
        }

        // App cache directory
        var cacheDir = FileSystem.CacheDirectory;
        if (Directory.Exists(cacheDir)) scanDirs.Add(cacheDir);

        int fileCount = 0;
        foreach (var dir in scanDirs)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 3
                });

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    fileCount++;
                    FilesScanned++;

                    if (fileCount % 5 == 0)
                    {
                        CurrentFile = file;
                        ScanStatusText = $"Scanning: {Path.GetFileName(file)}";
                        ScanProgress = 0.5 + (0.4 * Math.Min(1.0, fileCount / 500.0));
                    }

                    // Check for APK files (potential sideloaded malware)
                    if (file.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        var fi = new FileInfo(file);
                        // Suspicious: APKs in unexpected locations
                        if (!file.Contains("/data/app/") && fi.Length > 0)
                        {
                            AddThreat(Path.GetFileName(file), file,
                                "Android.Suspicious.SideloadedApk", "Low");
                        }
                    }

                    // Check for known malicious file patterns
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    if (fileName.Contains("payload") || fileName.Contains("exploit") ||
                        fileName.Contains("keylog") || fileName.Contains("rat_"))
                    {
                        AddThreat(Path.GetFileName(file), file,
                            "Android.Suspicious.MaliciousFile", "High");
                    }

                    // Check for scripts
                    if (file.EndsWith(".sh") || file.EndsWith(".py") || file.EndsWith(".js"))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(file, ct);
                            if (content.Contains("eval(") || content.Contains("exec(") ||
                                content.Contains("curl ") || content.Contains("wget ") ||
                                content.Contains("rm -rf") || content.Contains("su -c"))
                            {
                                AddThreat(Path.GetFileName(file), file,
                                    "Android.Script.Suspicious", "Medium");
                            }
                        }
                        catch { /* Skip unreadable files */ }
                    }

                    if (fileCount % 20 == 0)
                        await Task.Delay(1, ct);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private async Task ScanRunningServicesAsync(CancellationToken ct)
    {
        ScanStatusText = "Checking running services...";
        ScanProgress = 0.9;

        try
        {
            var context = Android.App.Application.Context;
            var am = (Android.App.ActivityManager?)context.GetSystemService(Android.Content.Context.ActivityService);
            if (am == null) return;

            var services = am.GetRunningServices(200);
            if (services == null) return;

            foreach (var svc in services)
            {
                ct.ThrowIfCancellationRequested();

                var pkgName = svc.Service?.PackageName ?? "";
                if (string.IsNullOrEmpty(pkgName)) continue;

                FilesScanned++;
                CurrentFile = $"service:{pkgName}";

                if (KnownMalwarePackages.Contains(pkgName))
                {
                    AddThreat($"Service: {pkgName}", pkgName,
                        "Android.Service.MaliciousRunning", "High");
                }
            }
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Service scan limited: {ex.Message}";
        }

        await Task.CompletedTask;
    }
#endif

    private void AddThreat(string fileName, string filePath, string threatName, string severity)
    {
        ThreatsFound++;
        _prefs.ThreatsBlocked = _prefs.ThreatsBlocked + 1;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Results.Add(new ScanResultItem
            {
                FileName = fileName,
                FilePath = filePath,
                ThreatName = threatName,
                Severity = severity,
                IsThreat = true,
                DetectedAt = DateTime.Now
            });
        });
    }

    [RelayCommand]
    private void StopScan()
    {
        _scanCts?.Cancel();
        IsScanning = false;
        ScanStatusText = "Scan stopped by user";
    }

    [RelayCommand]
    private void ClearResults()
    {
        Results.Clear();
        ThreatsFound = 0;
        FilesScanned = 0;
        ScanProgress = 0;
        ScanComplete = false;
        ScanStatusText = "Ready to scan";
    }

    [RelayCommand]
    private async Task KillThreatAsync(ScanResultItem? item)
    {
        if (item == null) return;

#if ANDROID
        try
        {
            var packageName = item.FilePath.Contains("/") ? null : item.FilePath;
            if (!string.IsNullOrEmpty(packageName) || item.FilePath.StartsWith("service:"))
            {
                var pkg = packageName ?? item.FilePath.Replace("service:", "");
                var am = (Android.App.ActivityManager?)Android.App.Application.Context
                    .GetSystemService(Android.Content.Context.ActivityService);
                am?.KillBackgroundProcesses(pkg);
                ScanStatusText = $"Killed: {item.FileName}";
                await Shell.Current.DisplayAlert("Process Killed",
                    $"{item.FileName} has been force stopped.", "OK");
            }
            else
            {
                ScanStatusText = "Cannot kill file-based threat. Use Remove.";
            }
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Kill failed: {ex.Message}";
        }
#endif
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RemoveThreatAsync(ScanResultItem? item)
    {
        if (item == null) return;

        var confirm = await Shell.Current.DisplayAlert("Remove Threat",
            $"Remove {item.FileName}?\n\nThreat: {item.ThreatName}\nPath: {item.FilePath}",
            "Remove", "Cancel");
        if (!confirm) return;

#if ANDROID
        try
        {
            var packageName = item.FilePath.Contains("/") ? null : item.FilePath;
            if (!string.IsNullOrEmpty(packageName))
            {
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionDelete);
                intent.SetData(Android.Net.Uri.Parse($"package:{packageName}"));
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                Android.App.Application.Context.StartActivity(intent);
                ScanStatusText = $"Uninstall dialog opened for {item.FileName}";
            }
            else if (File.Exists(item.FilePath))
            {
                File.Delete(item.FilePath);
                Results.Remove(item);
                ThreatsFound = Results.Count;
                ScanStatusText = $"Removed: {item.FileName}";
            }
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Remove failed: {ex.Message}";
        }
#endif
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenAppInfoAsync(ScanResultItem? item)
    {
        if (item == null) return;

#if ANDROID
        try
        {
            var packageName = item.FilePath.Contains("/") ? null : item.FilePath;
            if (packageName != null)
            {
                var intent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionApplicationDetailsSettings);
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                intent.SetData(Android.Net.Uri.Parse($"package:{packageName}"));
                Android.App.Application.Context.StartActivity(intent);
            }
        }
        catch { }
#endif
        await Task.CompletedTask;
    }
}
