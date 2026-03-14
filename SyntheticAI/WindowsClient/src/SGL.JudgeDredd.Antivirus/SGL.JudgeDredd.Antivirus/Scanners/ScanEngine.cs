using System.Collections.Concurrent;
using System.Diagnostics;
using PeNet;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;
using SGL.JudgeDredd.Shared.Helpers;

namespace SGL.JudgeDredd.Antivirus.Scanners;

public sealed class ScanEngine : IScanEngine, IDisposable
{
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ILlmService? _llmService;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _watcherLock = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentRealTimeScans = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _scanExclusions = new(StringComparer.OrdinalIgnoreCase);
    private bool _realTimeActive;
    private bool _disposed;

    private static readonly HashSet<string> SuspiciousApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "VirtualAlloc", "VirtualAllocEx", "VirtualProtect", "VirtualProtectEx", "VirtualFree",
        "CreateRemoteThread", "CreateRemoteThreadEx", "WriteProcessMemory", "ReadProcessMemory",
        "NtWriteVirtualMemory", "NtReadVirtualMemory", "NtAllocateVirtualMemory",
        "OpenProcess", "CreateProcessA", "CreateProcessW", "CreateProcessAsUserA", "CreateProcessAsUserW",
        "ShellExecuteA", "ShellExecuteW", "ShellExecuteExA", "ShellExecuteExW", "WinExec",
        "SetWindowsHookExA", "SetWindowsHookExW", "GetProcAddress",
        "LoadLibraryA", "LoadLibraryW", "LoadLibraryExA", "LoadLibraryExW",
        "GetModuleHandleA", "GetModuleHandleW",
        "InternetOpenA", "InternetOpenW", "InternetOpenUrlA", "InternetOpenUrlW",
        "InternetReadFile", "HttpSendRequestA", "HttpSendRequestW",
        "HttpOpenRequestA", "HttpOpenRequestW", "URLDownloadToFileA", "URLDownloadToFileW",
        "RegSetValueExA", "RegSetValueExW", "RegCreateKeyExA", "RegCreateKeyExW",
        "RegOpenKeyExA", "RegOpenKeyExW",
        "CryptEncrypt", "CryptDecrypt", "CryptGenKey", "CryptAcquireContextA", "CryptAcquireContextW",
        "AdjustTokenPrivileges", "OpenProcessToken", "LookupPrivilegeValueA", "LookupPrivilegeValueW",
        "IsDebuggerPresent", "CheckRemoteDebuggerPresent",
        "NtUnmapViewOfSection", "QueueUserAPC", "NtQueueApcThread",
        "MapViewOfFile", "CreateFileMappingA", "CreateFileMappingW",
        "GetAsyncKeyState", "GetKeyState",
        "SetServiceStatus", "CreateServiceA", "CreateServiceW", "StartServiceA", "StartServiceW",
        "CreateToolhelp32Snapshot", "Process32First", "Process32Next",
        "Thread32First", "Thread32Next", "NtSetInformationThread",
        "SuspendThread", "ResumeThread", "TerminateProcess", "NtCreateThreadEx"
    };

    private static readonly HashSet<string> SuspiciousSectionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPX0", "UPX1", "UPX2", ".upx", ".packed", ".aspack", ".adata",
        ".nsp0", ".nsp1", ".enigma1", ".enigma2", ".themida",
        ".vmp0", ".vmp1", ".vmprotect", ".text0", ".text1", ".code0",
        "MPRESS1", "MPRESS2", ".petite", ".shrink", ".yP", ".idata0", ".ndata", ".perplex"
    };

    private static readonly HashSet<string> TrustedPublishers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Corporation", "Microsoft Windows", "Google LLC", "Google Inc.",
        "Mozilla Corporation", "Apple Inc.", "Adobe Inc.", "Adobe Systems",
        "Oracle Corporation", "Intel Corporation", "NVIDIA Corporation",
        "Advanced Micro Devices", "Valve Corp", "Valve Corporation",
        "Epic Games", "Steam", "7-Zip", "Igor Pavlov",
        "Python Software Foundation", "The Git Development Community",
        "Node.js Foundation", "OpenJS Foundation",
        "JetBrains s.r.o.", "Visual Studio Code", "VSCode",
        "Notepad++", "WinRAR GmbH", "RARLAB", "PuTTY",
        "VideoLAN", "VLC", "OBS Project", "Zoom Video Communications",
        "Discord Inc.", "Spotify AB", "Slack Technologies",
        "Docker Inc.", "VMware, Inc.", "Oracle America",
        "Samsung Electronics", "Logitech", "Corsair",
        "ESET", "Malwarebytes", "Kaspersky", "Bitdefender", "Norton", "Avast",
        "WireGuard LLC", "OpenVPN Inc.", "Cloudflare",
    };

    private static readonly HashSet<string> PeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".scr", ".ocx", ".drv", ".cpl", ".com", ".pif"
    };

    private static readonly HashSet<string> RealTimeMonitoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".scr", ".com", ".pif",
        ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse",
        ".wsf", ".wsh", ".msi", ".msp", ".mst",
        ".cpl", ".ocx", ".drv", ".inf", ".hta", ".reg",
        ".lnk", ".jar", ".py", ".rb"
    };

    /// <summary>
    /// Directories to skip during scans - these are huge, OS-protected, contain
    /// redundant copies of system files, or browser/app caches that would cause extreme slowdowns.
    /// </summary>
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "WinSxS", "servicing", "Installer", "assembly", "SoftwareDistribution",
        "$Recycle.Bin", "$RECYCLE.BIN", "System Volume Information",
        "Recovery", "MSOCache", "PerfLogs",
        "node_modules", ".git", ".svn", ".hg",
        "packages", "obj", "bin",
        // Browser and app caches (Quick Scan was getting stuck hashing thousands of cache blobs)
        "Cache", "CacheStorage", "Code Cache", "GPUCache", "GrShaderCache",
        "ShaderCache", "DawnCache", "GraphiteDawnCache",
        "Service Worker", "blob_storage", "IndexedDB",
        "LocalStorage", "Session Storage", "Sessions",
        // Application data caches
        "CachedData", "CachedExtensions", "CachedExtensionVSIXs",
        "Crashpad", "crash_reports", "DawnGraphiteCache",
    };

    /// <summary>
    /// Maximum file size to hash during Quick/Extended scans (50MB).
    /// Files larger than this are skipped for hashing since they are almost never malware executables.
    /// Full Scan uses a higher limit.
    /// </summary>
    private const long QuickScanMaxFileSize = 50 * 1024 * 1024; // 50MB
    private const long FullScanMaxFileSize = 500 * 1024 * 1024; // 500MB

    public bool IsRealTimeProtectionActive => _realTimeActive;

    public event EventHandler<ThreatDetectedEvent>? ThreatDetected;

    public ScanEngine(IKnowledgeBase knowledgeBase, ILlmService? llmService = null)
    {
        _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
        _llmService = llmService;
    }

    /// <summary>
    /// Sets the list of file/directory paths to exclude from scanning.
    /// Paths are matched case-insensitively using StartsWith.
    /// </summary>
    public void SetExclusions(IEnumerable<string> paths)
    {
        _scanExclusions = new HashSet<string>(
            paths.Where(p => !string.IsNullOrWhiteSpace(p)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a file path is excluded from scanning by the configured exclusions.
    /// </summary>
    private bool IsExcluded(string filePath)
    {
        foreach (var ex in _scanExclusions)
        {
            if (filePath.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public async Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
    {
        return await ScanFileAsync(filePath, QuickScanMaxFileSize, ct).ConfigureAwait(false);
    }

    public async Task<ScanResult> ScanFileAsync(string filePath, long maxFileSize, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            ScannedAt = DateTime.UtcNow
        };

        // Skip files matching scan exclusion paths
        if (IsExcluded(filePath))
        {
            result.ScanDuration = stopwatch.Elapsed;
            return result;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                result.ScanDuration = stopwatch.Elapsed;
                return result;
            }

            result.FileSize = fileInfo.Length;

            if (fileInfo.Length == 0)
            {
                result.ScanDuration = stopwatch.Elapsed;
                return result;
            }

            // Skip files larger than the configured max size for this scan type
            // Large media files, disk images, etc. are almost never malware
            if (fileInfo.Length > maxFileSize)
            {
                result.ScanDuration = stopwatch.Elapsed;
                return result;
            }

            result.Sha256Hash = await HashHelper.ComputeSha256Async(filePath, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var signature = await _knowledgeBase.LookupHashAsync(result.Sha256Hash).ConfigureAwait(false);
            if (signature != null)
            {
                result.IsThreat = true;
                result.ThreatName = signature.Name;
                result.Severity = signature.Severity;
                result.DetectionMethod = DetectionMethod.Signature;
                result.ScanDuration = stopwatch.Elapsed;
                return result;
            }

            ct.ThrowIfCancellationRequested();

            string extension = Path.GetExtension(filePath);
            if (PeExtensions.Contains(extension))
            {
                var heuristic = AnalyzePeHeuristics(filePath, fileInfo.Length);
                result.HeuristicScore = heuristic.Score;

                if (heuristic.Score > 75)
                {
                    result.IsThreat = true;
                    result.ThreatName = $"Heuristic.Suspicious.{heuristic.PrimaryReason}";
                    result.Severity = heuristic.Score > 80
                        ? ThreatSeverity.High
                        : ThreatSeverity.Medium;
                    result.DetectionMethod = DetectionMethod.Heuristic;
                }
                else if (heuristic.Score > 50 && _llmService?.IsModelLoaded == true)
                {
                    // Suspicious but inconclusive heuristic — ask the LLM for second opinion
                    try
                    {
                        var threatInfo = new ThreatInfo
                        {
                            Name = $"Suspicious: {result.FileName}",
                            Description = $"File: {result.FileName}, Size: {fileInfo.Length} bytes, " +
                                          $"Heuristic Score: {heuristic.Score}/100, " +
                                          $"Primary Indicator: {heuristic.PrimaryReason}, " +
                                          $"SHA256: {result.Sha256Hash}"
                        };

                        var llmResult = await _llmService.AnalyzeThreatAsync(threatInfo, ct).ConfigureAwait(false);

                        if (llmResult.Confidence > 70 &&
                            llmResult.Verdict.Contains("malicious", StringComparison.OrdinalIgnoreCase))
                        {
                            result.IsThreat = true;
                            result.ThreatName = $"AI.{llmResult.ThreatType}";
                            result.Severity = llmResult.Confidence > 90
                                ? ThreatSeverity.High
                                : ThreatSeverity.Medium;
                            result.DetectionMethod = DetectionMethod.LlmAnalysis;
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        // LLM analysis failed — continue without it
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        result.ScanDuration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ScanSession> ScanDirectoryAsync(
        string path,
        ScanType type,
        IProgress<ScanProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        var session = new ScanSession
        {
            SessionId = Guid.NewGuid(),
            Type = type,
            Status = ScanStatus.Scanning,
            StartedAt = DateTime.UtcNow
        };

        var files = EnumerateFilesSafe(path);
        session.TotalFiles = files.Count;
        return await ExecuteScanAsync(session, files, progress, ct).ConfigureAwait(false);
    }

    public async Task<ScanSession> QuickScanAsync(
        IProgress<ScanProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        var session = new ScanSession
        {
            SessionId = Guid.NewGuid(),
            Type = ScanType.Quick,
            Status = ScanStatus.Scanning,
            StartedAt = DateTime.UtcNow
        };

        var quickScanPaths = GetQuickScanPaths();
        var allFiles = new List<string>();

        foreach (string dir in quickScanPaths)
        {
            ct.ThrowIfCancellationRequested();
            allFiles.AddRange(EnumerateFilesSafe(dir));
        }

        session.TotalFiles = allFiles.Count;
        return await ExecuteScanAsync(session, allFiles, progress, ct).ConfigureAwait(false);
    }

    public async Task<ScanSession> ExtendedScanAsync(
        IEnumerable<string>? additionalPaths = null,
        IProgress<ScanProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        var session = new ScanSession
        {
            SessionId = Guid.NewGuid(),
            Type = ScanType.Extended,
            Status = ScanStatus.Scanning,
            StartedAt = DateTime.UtcNow
        };

        // Start with Quick Scan paths (proven stable)
        var scanPaths = GetQuickScanPaths();

        // Add any user-selected additional folders
        if (additionalPaths != null)
        {
            foreach (var path in additionalPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    scanPaths.Add(path);
            }
        }

        // Deduplicate paths
        var uniquePaths = new HashSet<string>(scanPaths, StringComparer.OrdinalIgnoreCase);
        var allFiles = new List<string>();

        foreach (string dir in uniquePaths)
        {
            ct.ThrowIfCancellationRequested();
            allFiles.AddRange(EnumerateFilesSafe(dir));
        }

        session.TotalFiles = allFiles.Count;
        return await ExecuteScanAsync(session, allFiles, progress, ct).ConfigureAwait(false);
    }

    public async Task<ScanSession> FullScanAsync(
        IProgress<ScanProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        var session = new ScanSession
        {
            SessionId = Guid.NewGuid(),
            Type = ScanType.Full,
            Status = ScanStatus.Scanning,
            StartedAt = DateTime.UtcNow
        };

        // Phase-based full scan to prevent RAM exhaustion (was crashing by loading all files into memory):
        // Phase 1 (0-15%): User directories first (easy, small) - heavy throttle (10% resource usage)
        // Phase 2 (15-50%): Program Files, AppData - moderate throttle, gradually removed
        // Phase 3 (50-100%): System dirs, other drives - no throttle, full speed
        var phase1Paths = GetFullScanPhase1Paths();
        var phase2Paths = GetFullScanPhase2Paths();
        var phase3Paths = GetFullScanPhase3Paths();

        var allPhasePaths = new List<(List<string> paths, string label)>
        {
            (phase1Paths, "Phase 1: User directories"),
            (phase2Paths, "Phase 2: Application directories"),
            (phase3Paths, "Phase 3: System & other drives")
        };

        return await ExecuteFullScanPhasedAsync(session, allPhasePaths, progress, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Phased full scan: enumerates and scans one directory at a time instead of
    /// loading all file paths into a single list. Applies RAM throttling that
    /// gradually eases as the scan progresses.
    /// </summary>
    private async Task<ScanSession> ExecuteFullScanPhasedAsync(
        ScanSession session,
        List<(List<string> paths, string label)> phases,
        IProgress<ScanProgressEvent>? progress,
        CancellationToken ct)
    {
        var scanStopwatch = Stopwatch.StartNew();
        int globalScanned = 0;
        int globalTotal = 0;

        progress?.Report(new ScanProgressEvent
        {
            TotalFiles = 0,
            ScannedFiles = 0,
            ThreatsFound = 0,
            CurrentFile = "Estimating scan size..."
        });

        // Lightweight count pass: count files per directory without storing paths
        var directoryCounts = new List<(string dir, int estimatedCount, int phaseIndex)>();
        for (int p = 0; p < phases.Count; p++)
        {
            foreach (string dir in phases[p].paths)
            {
                ct.ThrowIfCancellationRequested();
                int count = CountFilesSafe(dir);
                if (count > 0)
                {
                    directoryCounts.Add((dir, count, p));
                    globalTotal += count;
                }

                progress?.Report(new ScanProgressEvent
                {
                    TotalFiles = globalTotal,
                    ScannedFiles = 0,
                    ThreatsFound = 0,
                    CurrentFile = $"Counting files in {dir}... ({globalTotal:N0} found so far)"
                });
            }
        }

        session.TotalFiles = globalTotal;

        if (globalTotal == 0)
        {
            session.Status = ScanStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            return session;
        }

        try
        {
            // Scan each directory one at a time (bounded memory per directory)
            foreach (var (dir, estimatedCount, phaseIndex) in directoryCounts)
            {
                ct.ThrowIfCancellationRequested();

                // Enumerate files for ONLY this directory (not the entire system)
                var dirFiles = EnumerateFilesSafe(dir);

                // Adjust total if actual count differs from estimate
                int diff = dirFiles.Count - estimatedCount;
                if (diff != 0)
                {
                    globalTotal += diff;
                    session.TotalFiles = globalTotal;
                }

                for (int i = 0; i < dirFiles.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    string file = dirFiles[i];
                    bool isThreat = false;

                    try
                    {
                        var result = await ScanFileAsync(file, ct).ConfigureAwait(false);
                        session.ScannedFiles++;
                        globalScanned++;

                        if (result.IsThreat)
                        {
                            isThreat = true;
                            session.ThreatsFound++;
                            session.Results.Add(result);
                            RaiseThreatDetected(result);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        session.ErrorCount++;
                        session.ScannedFiles++;
                        globalScanned++;
                    }

                    // RAM throttling based on overall scan progress percentage
                    double progressPct = globalTotal > 0 ? (double)globalScanned / globalTotal * 100.0 : 0;
                    if (progressPct < 15.0)
                    {
                        // Heavy throttle: pause every 3rd file (limits CPU/RAM to ~10%)
                        if (i % 3 == 0)
                            await Task.Delay(50, ct).ConfigureAwait(false);
                    }
                    else if (progressPct < 50.0)
                    {
                        // Moderate throttle: pause every 20th file (gradually ease off)
                        if (i % 20 == 0)
                            await Task.Delay(10, ct).ConfigureAwait(false);
                    }
                    // >= 50%: no throttle, full speed

                    // Periodic GC to release file path strings from prior directories
                    if (globalScanned % 5000 == 0)
                    {
                        GC.Collect(0, GCCollectionMode.Optimized, false);
                    }

                    // Progress report every 25 files, on threats, or on last file
                    if (i % 25 == 0 || isThreat || i == dirFiles.Count - 1)
                    {
                        TimeSpan? eta = null;
                        if (globalScanned > 10)
                        {
                            var elapsed = scanStopwatch.Elapsed;
                            var avgPerFile = elapsed.TotalMilliseconds / globalScanned;
                            var remaining = (globalTotal - globalScanned) * avgPerFile;
                            eta = TimeSpan.FromMilliseconds(remaining);
                        }

                        progress?.Report(new ScanProgressEvent
                        {
                            TotalFiles = globalTotal,
                            ScannedFiles = globalScanned,
                            ThreatsFound = session.ThreatsFound,
                            CurrentFile = file,
                            ErrorCount = session.ErrorCount,
                            EstimatedTimeRemaining = eta
                        });
                    }
                }

                // Free directory file list memory before moving to next directory
                dirFiles.Clear();
            }

            session.Status = ScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            session.Status = ScanStatus.Cancelled;
        }
        catch
        {
            session.Status = ScanStatus.Error;
        }

        session.CompletedAt = DateTime.UtcNow;
        return session;
    }

    /// <summary>
    /// Counts files in a directory tree without storing paths in memory.
    /// </summary>
    private static int CountFilesSafe(string rootPath)
    {
        int count = 0;
        var directoryStack = new Stack<string>();

        if (!Directory.Exists(rootPath)) return 0;
        directoryStack.Push(rootPath);

        while (directoryStack.Count > 0)
        {
            var currentDir = directoryStack.Pop();

            try
            {
                count += Directory.EnumerateFiles(currentDir).Count();
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                {
                    try
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (SkippedDirectories.Contains(dirName))
                            continue;
                        directoryStack.Push(subDir);
                    }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return count;
    }

    public async Task<ScanSession> ScanRemovableDriveAsync(
        string driveLetter,
        IProgress<ScanProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        string normalized = driveLetter.TrimEnd('\\', ':');
        if (normalized.Length != 1 || !char.IsLetter(normalized[0]))
            throw new ArgumentException($"Invalid drive letter: {driveLetter}", nameof(driveLetter));

        string drivePath = $"{normalized}:\\";
        var driveInfo = new DriveInfo(normalized);

        if (driveInfo.DriveType != DriveType.Removable)
            throw new InvalidOperationException(
                $"Drive {normalized}: is not a removable drive (detected type: {driveInfo.DriveType}).");

        if (!driveInfo.IsReady)
            throw new InvalidOperationException($"Drive {normalized}: is not ready.");

        var session = new ScanSession
        {
            SessionId = Guid.NewGuid(),
            Type = ScanType.Removable,
            Status = ScanStatus.Scanning,
            StartedAt = DateTime.UtcNow
        };

        var files = EnumerateFilesSafe(drivePath);
        session.TotalFiles = files.Count;
        return await ExecuteScanAsync(session, files, progress, ct).ConfigureAwait(false);
    }

    public void StartRealTimeProtection()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScanEngine));
        if (_realTimeActive) return;

        lock (_watcherLock)
        {
            if (_realTimeActive) return;

            var watchPaths = GetRealTimeProtectionPaths();

            foreach (string path in watchPaths)
            {
                if (!Directory.Exists(path)) continue;

                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    InternalBufferSize = 65536
                };

                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Renamed += OnFileRenamedEvent;

                _watchers.Add(watcher);
            }

            _realTimeActive = true;
        }
    }

    public void StopRealTimeProtection()
    {
        if (!_realTimeActive) return;

        lock (_watcherLock)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileSystemEvent;
                watcher.Changed -= OnFileSystemEvent;
                watcher.Renamed -= OnFileRenamedEvent;
                watcher.Dispose();
            }

            _watchers.Clear();
            _recentRealTimeScans.Clear();
            _realTimeActive = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopRealTimeProtection();
        _disposed = true;
    }

    private async Task<ScanSession> ExecuteScanAsync(
        ScanSession session,
        List<string> files,
        IProgress<ScanProgressEvent>? progress,
        CancellationToken ct,
        long maxFileSize = 0)
    {
        if (maxFileSize <= 0) maxFileSize = QuickScanMaxFileSize;
        var scanStopwatch = Stopwatch.StartNew();

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                string file = files[i];
                bool isThreat = false;

                try
                {
                    var result = await ScanFileAsync(file, maxFileSize, ct).ConfigureAwait(false);
                    session.ScannedFiles++;

                    if (result.IsThreat)
                    {
                        isThreat = true;
                        session.ThreatsFound++;
                        session.Results.Add(result);
                        RaiseThreatDetected(result);
                    }
                    // Clean results are NOT stored to save memory during large scans
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    session.ErrorCount++;
                    session.ScannedFiles++;
                }

                // Throttle: yield to UI thread every 5 files to prevent freezing
                if (i % 5 == 0)
                    await Task.Delay(1, ct).ConfigureAwait(false);

                // Periodic GC hint every 2000 files to prevent memory pressure
                if (i % 2000 == 0 && i > 0)
                {
                    GC.Collect(0, GCCollectionMode.Optimized, false);
                }

                // Report progress every 5 files, on threats, or on the last file
                if (i % 5 == 0 || isThreat || i == files.Count - 1)
                {
                    // Calculate ETA based on elapsed time and progress
                    TimeSpan? eta = null;
                    if (session.ScannedFiles > 10)
                    {
                        var elapsed = scanStopwatch.Elapsed;
                        var avgPerFile = elapsed.TotalMilliseconds / session.ScannedFiles;
                        var remaining = (session.TotalFiles - session.ScannedFiles) * avgPerFile;
                        eta = TimeSpan.FromMilliseconds(remaining);
                    }

                    progress?.Report(new ScanProgressEvent
                    {
                        TotalFiles = session.TotalFiles,
                        ScannedFiles = session.ScannedFiles,
                        ThreatsFound = session.ThreatsFound,
                        CurrentFile = file,
                        ErrorCount = session.ErrorCount,
                        EstimatedTimeRemaining = eta
                    });
                }
            }

            session.Status = ScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            session.Status = ScanStatus.Cancelled;
        }
        catch
        {
            session.Status = ScanStatus.Error;
        }

        session.CompletedAt = DateTime.UtcNow;
        return session;
    }

    private static (double Score, string PrimaryReason) AnalyzePeHeuristics(string filePath, long fileSize)
    {
        double score = 0;
        string primaryReason = "Generic";
        double highestContribution = 0;

        try
        {
            var peFile = new PeFile(filePath);

            // Skip heuristic scoring for trusted-signed binaries
            try
            {
                var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
                if (cert != null)
                {
                    var subject = cert.Subject;
                    foreach (var publisher in TrustedPublishers)
                    {
                        if (subject.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                        {
                            return (0, "TrustedPublisher");
                        }
                    }
                }
            }
            catch { /* No valid signature or unsigned - continue with heuristics */ }

            double importScore = ScoreSuspiciousImports(peFile);
            score += importScore;
            if (importScore > highestContribution)
            {
                highestContribution = importScore;
                primaryReason = "SuspiciousImports";
            }

            var securityDir = peFile.ImageNtHeaders?.OptionalHeader?.DataDirectory;
            var isSigned = securityDir != null && securityDir.Length > 4
                           && securityDir[4].VirtualAddress != 0 && securityDir[4].Size != 0;
            if (!isSigned)
            {
                const double unsignedPenalty = 15;
                score += unsignedPenalty;
                if (unsignedPenalty > highestContribution)
                {
                    highestContribution = unsignedPenalty;
                    primaryReason = "UnsignedBinary";
                }
            }

            if (peFile.ImageSectionHeaders is { Length: > 0 })
            {
                double entropyScore = 0;
                foreach (var section in peFile.ImageSectionHeaders)
                {
                    if (section.SizeOfRawData == 0) continue;
                    double entropy = ComputeSectionEntropy(filePath, section.PointerToRawData, section.SizeOfRawData);
                    if (entropy > 7.5)
                        entropyScore += 15;
                }

                entropyScore = Math.Min(entropyScore, 30);
                score += entropyScore;
                if (entropyScore > highestContribution)
                {
                    highestContribution = entropyScore;
                    primaryReason = "PackedOrEncrypted";
                }

                double sectionNameScore = 0;
                foreach (var section in peFile.ImageSectionHeaders)
                {
                    string sectionName = (section.Name ?? string.Empty).TrimEnd('\0');
                    if (sectionName.Length > 0 && SuspiciousSectionNames.Contains(sectionName))
                        sectionNameScore += 10;
                }

                sectionNameScore = Math.Min(sectionNameScore, 20);
                score += sectionNameScore;
                if (sectionNameScore > highestContribution)
                {
                    highestContribution = sectionNameScore;
                    primaryReason = "SuspiciousSectionName";
                }
            }

            if (fileSize < 102400 && peFile.ImportedFunctions is { Length: > 20 })
            {
                const double smallFilePenalty = 10;
                score += smallFilePenalty;
                if (smallFilePenalty > highestContribution)
                    primaryReason = "SmallFileHighImports";
            }

            double ransomwareScore = ScoreRansomwareIndicators(peFile);
            score += ransomwareScore;
            if (ransomwareScore > highestContribution)
            {
                highestContribution = ransomwareScore;
                primaryReason = "RansomwareBehavior";
            }
        }
        catch
        {
            // File is not a valid PE or could not be parsed
        }

        // Large legitimate applications naturally have many APIs
        if (fileSize > 10_000_000) // >10MB - likely legitimate app
            score = (int)(score * 0.5);
        else if (fileSize > 1_000_000) // >1MB
            score = (int)(score * 0.7);

        return (Math.Min(score, 100), primaryReason);
    }

    private static double ScoreSuspiciousImports(PeFile peFile)
    {
        if (peFile.ImportedFunctions is not { Length: > 0 })
            return 0;

        int suspiciousCount = 0;
        foreach (var import in peFile.ImportedFunctions)
        {
            if (import.Name != null && SuspiciousApis.Contains(import.Name))
                suspiciousCount++;
        }

        return Math.Min(suspiciousCount * 2.5, 20.0);
    }

    private static readonly HashSet<string> RansomwareCryptoApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "CryptEncrypt", "CryptDecrypt", "CryptGenKey", "CryptAcquireContextA", "CryptAcquireContextW",
        "CryptImportKey", "CryptExportKey", "CryptDestroyKey", "CryptReleaseContext",
        "BCryptEncrypt", "BCryptDecrypt", "BCryptGenerateSymmetricKey", "BCryptOpenAlgorithmProvider",
        "BCryptGenRandom", "BCryptDeriveKeyPBKDF2",
    };

    private static readonly HashSet<string> RansomwareFileApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "FindFirstFileA", "FindFirstFileW", "FindNextFileA", "FindNextFileW", "FindFirstFileExW",
        "FindClose", "GetLogicalDriveStringsA", "GetLogicalDriveStringsW", "GetDriveTypeA", "GetDriveTypeW",
        "MoveFileA", "MoveFileW", "MoveFileExA", "MoveFileExW",
        "DeleteFileA", "DeleteFileW", "RemoveDirectoryA", "RemoveDirectoryW",
    };

    private static readonly HashSet<string> RansomwareShadowApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateProcessA", "CreateProcessW", "ShellExecuteA", "ShellExecuteW",
        "ShellExecuteExA", "ShellExecuteExW", "WinExec", "system",
    };

    private static readonly HashSet<string> RansomwareFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".encrypted", ".locked", ".crypt", ".crypted", ".enc", ".ransom",
        ".WNCRY", ".wcry", ".wncrypt", ".wncryt",
        ".locky", ".zepto", ".odin", ".aesir",
        ".cerber", ".cerber2", ".cerber3",
        ".dharma", ".wallet", ".onion",
        ".lockbit", ".lockbit3",
        ".basta", ".black",
        ".akira", ".royal",
        ".hive", ".HiVe",
        ".play", ".PLAY",
        ".clop", ".Cl0p",
        ".conti", ".ryuk",
        ".revil", ".sodinokibi",
        ".medusa",
    };

    private static double ScoreRansomwareIndicators(PeFile peFile)
    {
        if (peFile.ImportedFunctions is not { Length: > 0 })
            return 0;

        int cryptoApiCount = 0;
        int fileEnumCount = 0;
        int processApiCount = 0;

        foreach (var import in peFile.ImportedFunctions)
        {
            if (import.Name == null) continue;
            if (RansomwareCryptoApis.Contains(import.Name)) cryptoApiCount++;
            if (RansomwareFileApis.Contains(import.Name)) fileEnumCount++;
            if (RansomwareShadowApis.Contains(import.Name)) processApiCount++;
        }

        double score = 0;

        if (cryptoApiCount >= 2 && fileEnumCount >= 2 && processApiCount >= 1)
            score += 30;
        else if (cryptoApiCount >= 2 && fileEnumCount >= 2)
            score += 20;
        else if (cryptoApiCount >= 3)
            score += 10;

        return Math.Min(score, 35);
    }

    private static double ComputeSectionEntropy(string filePath, uint offset, uint size)
    {
        if (size == 0) return 0;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (offset + size > stream.Length) return 0;

            stream.Seek(offset, SeekOrigin.Begin);

            int readSize = (int)Math.Min(size, 262144);
            byte[] buffer = new byte[readSize];
            int bytesRead = stream.Read(buffer, 0, readSize);

            if (bytesRead == 0) return 0;

            int[] frequency = new int[256];
            for (int i = 0; i < bytesRead; i++)
                frequency[buffer[i]]++;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (frequency[i] == 0) continue;
                double probability = (double)frequency[i] / bytesRead;
                entropy -= probability * Math.Log2(probability);
            }

            return entropy;
        }
        catch { return 0; }
    }

    private async void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        await HandleRealTimeScanAsync(e.FullPath);
    }

    private async void OnFileRenamedEvent(object sender, RenamedEventArgs e)
    {
        await HandleRealTimeScanAsync(e.FullPath);
    }

    private async Task HandleRealTimeScanAsync(string filePath)
    {
        try
        {
            string extension = Path.GetExtension(filePath);

            if (RansomwareFileExtensions.Contains(extension))
            {
                var ransomResult = new ScanResult
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    IsThreat = true,
                    ThreatName = "Ransomware.FileRename",
                    Severity = ThreatSeverity.Critical,
                    DetectionMethod = DetectionMethod.Behavioral,
                    HeuristicScore = 95.0,
                    ScannedAt = DateTime.UtcNow
                };
                RaiseThreatDetected(ransomResult);
                return;
            }

            if (!RealTimeMonitoredExtensions.Contains(extension))
                return;

            if (_recentRealTimeScans.TryGetValue(filePath, out DateTime lastScan)
                && (DateTime.UtcNow - lastScan).TotalSeconds < 5)
                return;

            _recentRealTimeScans[filePath] = DateTime.UtcNow;

            await Task.Delay(500).ConfigureAwait(false);

            if (!File.Exists(filePath)) return;

            var result = await ScanFileAsync(filePath).ConfigureAwait(false);

            if (result.IsThreat)
                RaiseThreatDetected(result);

            CleanupStaleRealTimeEntries();
        }
        catch { }
    }

    private void CleanupStaleRealTimeEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var staleKeys = _recentRealTimeScans
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in staleKeys)
            _recentRealTimeScans.TryRemove(key, out _);
    }

    private void RaiseThreatDetected(ScanResult result)
    {
        ThreatDetected?.Invoke(this, new ThreatDetectedEvent
        {
            Result = result,
            DetectedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Iterative (stack-based) file enumeration that avoids stack overflow on deeply nested directories.
    /// Skips known problematic directories (WinSxS, $Recycle.Bin, etc.) during enumeration.
    /// </summary>
    private static List<string> EnumerateFilesSafe(string rootPath)
    {
        var files = new List<string>();
        var directoryStack = new Stack<string>();

        if (!Directory.Exists(rootPath)) return files;
        directoryStack.Push(rootPath);

        while (directoryStack.Count > 0)
        {
            var currentDir = directoryStack.Pop();

            // Enumerate files in current directory
            try
            {
                foreach (string file in Directory.EnumerateFiles(currentDir))
                    files.Add(file);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            // Push subdirectories onto stack (skip known problematic dirs)
            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                {
                    try
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (SkippedDirectories.Contains(dirName))
                            continue;
                        directoryStack.Push(subDir);
                    }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return files;
    }

    private static List<string> GetQuickScanPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AddIfValid(paths, Path.Combine(userProfile, "Downloads"));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddIfValid(paths, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.Startup));

        // Only scan specific risky AppData subfolders instead of the entire Roaming directory
        // (Roaming has browser caches, VS Code extensions, etc. — hundreds of thousands of files)
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            AddIfValid(paths, Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup"));
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            AddIfValid(paths, Path.Combine(localAppData, "Temp"));

        string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        AddIfValid(paths, commonStartup);

        return paths.ToList();
    }

    /// <summary>
    /// Phase 1: User profile directories (smallest, safest to start with).
    /// Scanned first with heavy throttling.
    /// </summary>
    private static List<string> GetFullScanPhase1Paths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AddIfValid(paths, Path.Combine(userProfile, "Downloads"));
        AddIfValid(paths, Path.Combine(userProfile, "Documents"));
        AddIfValid(paths, Path.Combine(userProfile, "Desktop"));
        AddIfValid(paths, Path.Combine(userProfile, "Music"));
        AddIfValid(paths, Path.Combine(userProfile, "Videos"));
        AddIfValid(paths, Path.Combine(userProfile, "Pictures"));

        // Startup locations (small but important)
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

        return paths.ToList();
    }

    /// <summary>
    /// Phase 2: Application directories (medium size, moderate risk).
    /// Scanned with moderate throttling that gradually eases.
    /// </summary>
    private static List<string> GetFullScanPhase2Paths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86));
        AddIfValid(paths, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));

        return paths.ToList();
    }

    /// <summary>
    /// Phase 3: System directories and other drives (largest, scanned at full speed).
    /// </summary>
    private static List<string> GetFullScanPhase3Paths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windowsDir))
        {
            AddIfValid(paths, Path.Combine(windowsDir, "Temp"));
            AddIfValid(paths, Path.Combine(windowsDir, "System32", "drivers"));
            AddIfValid(paths, Path.Combine(windowsDir, "System32", "Tasks"));
            AddIfValid(paths, Path.Combine(windowsDir, "SysWOW64"));
            AddIfValid(paths, Path.Combine(windowsDir, "Prefetch"));
        }

        // All non-system fixed drives
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    string root = drive.RootDirectory.FullName;
                    if (!root.Equals("C:\\", StringComparison.OrdinalIgnoreCase))
                        AddIfValid(paths, root);
                }
            }
        }
        catch { }

        return paths.ToList();
    }

    private static List<string> GetRealTimeProtectionPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AddIfValid(paths, Path.Combine(userProfile, "Downloads"));
        AddIfValid(paths, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddIfValid(paths, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            AddIfValid(paths, Path.Combine(localAppData, "Temp"));

        return paths.ToList();
    }

    private static void AddIfValid(HashSet<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            paths.Add(path);
    }
}
