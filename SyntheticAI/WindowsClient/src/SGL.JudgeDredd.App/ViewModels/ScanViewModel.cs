using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.App.Services;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ScanViewModel : ViewModelBase
{
    private readonly IScanEngine _scanEngine;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ILlmService? _llmService;
    private readonly QuarantineService _quarantineService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private ScanStatus _scanStatus = ScanStatus.Idle;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _scannedFiles;

    [ObservableProperty]
    private int _threatsFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExtendedScanSelected))]
    private ScanType _selectedScanType = ScanType.Quick;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private bool _useLlmScan;

    // ── Post-scan action window ───────────────────────────────────

    /// <summary>
    /// True when the post-scan action overlay should be displayed.
    /// </summary>
    [ObservableProperty]
    private bool _isPostScanWindowVisible;

    // ── Threat-action background image ───────────────────────────────

    /// <summary>
    /// True when the most recent scan found one or more threats.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreatBackgroundImage))]
    private bool _hasActiveThreats;

    /// <summary>
    /// Returns the attack background image path when active threats exist.
    /// </summary>
    public string ThreatBackgroundImage => HasActiveThreats ? "Assets/Attack.png" : string.Empty;

    // ── AI Watch properties ──────────────────────────────────────────

    /// <summary>
    /// True while the "Watch with AI" feature is actively monitoring a threat.
    /// </summary>
    [ObservableProperty]
    private bool _isAiWatching;

    /// <summary>
    /// The LLM-generated analysis produced after the AI watch period completes.
    /// </summary>
    [ObservableProperty]
    private string _aiWatchAnalysis = string.Empty;

    /// <summary>
    /// Countdown progress (60 → 0) during AI watch monitoring.
    /// </summary>
    [ObservableProperty]
    private int _aiWatchProgress;

    public bool IsExtendedScanSelected => SelectedScanType == ScanType.Extended;

    public ObservableCollection<ScanResult> Results { get; } = [];

    /// <summary>
    /// User-selected additional folders for Extended scan.
    /// </summary>
    public ObservableCollection<string> ExtendedScanFolders { get; } = [];

    /// <summary>
    /// Real-time list of file paths being scanned, shown during active scan.
    /// </summary>
    public ObservableCollection<string> RecentScannedFiles { get; } = [];

    private const int MaxRecentFiles = 200;

    private readonly HashSet<string> _ignoredFiles = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<string> IgnoredFiles { get; } = [];

    public ScanViewModel(IScanEngine scanEngine, IKnowledgeBase knowledgeBase)
        : this(scanEngine, knowledgeBase, null, null)
    {
    }

    public ScanViewModel(IScanEngine scanEngine, IKnowledgeBase knowledgeBase, ILlmService? llmService = null, QuarantineService? quarantineService = null)
    {
        _scanEngine = scanEngine;
        _knowledgeBase = knowledgeBase;
        _llmService = llmService;
        _quarantineService = quarantineService ?? new QuarantineService();
        Title = "Scan";
    }

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScanAsync()
    {
        _scanCts = new CancellationTokenSource();
        IsScanning = true;
        ScanStatus = ScanStatus.Scanning;
        Results.Clear();
        RecentScannedFiles.Clear();
        ProgressPercent = 0;
        ScannedFiles = 0;
        TotalFiles = 0;
        ThreatsFound = 0;
        CurrentFile = string.Empty;
        HasActiveThreats = false;

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Scanning for threats...");

        var progress = new Progress<ScanProgressEvent>(e =>
        {
            TotalFiles = e.TotalFiles;
            ScannedFiles = e.ScannedFiles;
            ThreatsFound = e.ThreatsFound;
            ProgressPercent = e.ProgressPercent;
            CurrentFile = e.CurrentFile;

            // Add to real-time file list (throttled - only every 10th file to reduce UI pressure)
            if (!string.IsNullOrEmpty(e.CurrentFile) && e.ScannedFiles % 10 == 0)
            {
                RecentScannedFiles.Insert(0, e.CurrentFile);
                while (RecentScannedFiles.Count > MaxRecentFiles)
                    RecentScannedFiles.RemoveAt(RecentScannedFiles.Count - 1);
            }
        });

        try
        {
            ScanSession session;

            switch (SelectedScanType)
            {
                case ScanType.Quick:
                    session = await _scanEngine.QuickScanAsync(progress, _scanCts.Token);
                    break;
                case ScanType.Full:
                case ScanType.Extended:
                    session = await _scanEngine.ExtendedScanAsync(ExtendedScanFolders, progress, _scanCts.Token);
                    break;
                case ScanType.Custom:
                    session = await _scanEngine.ScanDirectoryAsync(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ScanType.Custom, progress, _scanCts.Token);
                    break;
                default:
                    session = await _scanEngine.QuickScanAsync(progress, _scanCts.Token);
                    break;
            }

            foreach (var result in session.Results)
            {
                // Only show threats that aren't in the ignore list
                if (result.IsThreat
                    && !_ignoredFiles.Contains(result.FilePath)
                    && !_quarantineService.IsIgnored(result.FilePath))
                    Results.Add(result);
            }

            // Update active-threat state for the Attack background image
            HasActiveThreats = Results.Any(r => r.IsThreat);

            ScanStatus = ScanStatus.Completed;

            // Show post-scan action window when threats are found
            if (Results.Count > 0)
                IsPostScanWindowVisible = true;

            AvatarViewModel.Instance.SetExpression(
                ThreatsFound > 0 ? AvatarExpression.FoundMalware : AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                ThreatsFound > 0
                    ? $"Scan complete. Found {ThreatsFound} threat(s)!"
                    : "Scan complete. No threats found.");
        }
        catch (OperationCanceledException)
        {
            ScanStatus = ScanStatus.Cancelled;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Scan was cancelled.");
        }
        catch (Exception)
        {
            ScanStatus = ScanStatus.Error;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("Scan encountered an error.");
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private bool CanStartScan() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        _scanCts?.Cancel();
    }

    private bool CanStopScan() => IsScanning;

    [RelayCommand]
    private void DismissPostScanWindow()
    {
        IsPostScanWindowVisible = false;
    }

    // ── Threat action commands ───────────────────────────────────────

    /// <summary>
    /// Quarantines a detected threat using the <see cref="QuarantineService"/> vault
    /// and also persists metadata through the <see cref="IKnowledgeBase"/>.
    /// </summary>
    [RelayCommand]
    private async Task QuarantineThreatAsync(ScanResult result)
    {
        if (result is null) return;

        try
        {
            var severity = result.Severity switch
            {
                ThreatSeverity.Critical => "Critical",
                ThreatSeverity.High => "High",
                ThreatSeverity.Medium => "Medium",
                ThreatSeverity.Low => "Low",
                _ => "Medium"
            };

            // Move file into the QuarantineService vault
            await _quarantineService.QuarantineAsync(
                result.FilePath,
                result.ThreatName ?? result.FileName,
                severity);

            // Also log through the knowledge base
            try
            {
                var threatInfo = new ThreatInfo
                {
                    Name = result.ThreatName ?? result.FileName,
                    Severity = result.Severity,
                    Description = $"Detected by {result.DetectionMethod} with score {result.HeuristicScore:F1}"
                };
                await _knowledgeBase.QuarantineFileAsync(result.FilePath, threatInfo);
            }
            catch { /* KB logging is non-critical */ }

            Results.Remove(result);
            ThreatsFound = Results.Count(r => r.IsThreat);
            HasActiveThreats = Results.Any(r => r.IsThreat);

            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Quarantined: {result.FileName}");
        }
        catch (Exception ex)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Quarantine failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Backwards-compatible quarantine command (delegates to QuarantineThreat).
    /// </summary>
    [RelayCommand]
    private async Task QuarantineAsync(ScanResult result)
    {
        await QuarantineThreatAsync(result);
    }

    [RelayCommand]
    private async Task DeleteThreatAsync(ScanResult result)
    {
        if (result is null) return;

        try
        {
            if (System.IO.File.Exists(result.FilePath))
            {
                System.IO.File.Delete(result.FilePath);
            }

            Results.Remove(result);
            ThreatsFound = Results.Count(r => r.IsThreat);
            HasActiveThreats = Results.Any(r => r.IsThreat);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Deleted threat: {result.FileName}");
        }
        catch (Exception ex)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks a threat as ignored using both the local ignore set and the
    /// <see cref="QuarantineService"/> persistent ignore list.
    /// </summary>
    [RelayCommand]
    private async Task IgnoreThreatAsync(ScanResult result)
    {
        if (result is null) return;

        _ignoredFiles.Add(result.FilePath);
        IgnoredFiles.Add(result.FilePath);
        Results.Remove(result);
        ThreatsFound = Results.Count(r => r.IsThreat);
        HasActiveThreats = Results.Any(r => r.IsThreat);

        // Persist via QuarantineService ignore list
        try
        {
            await _quarantineService.IgnoreThreatAsync(result.FilePath);
        }
        catch { /* Non-critical */ }

        // Also persist to legacy ignore file
        try
        {
            var ignorePath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "scan_ignore.txt");
            var dir = System.IO.Path.GetDirectoryName(ignorePath);
            if (dir != null) System.IO.Directory.CreateDirectory(dir);
            await System.IO.File.AppendAllTextAsync(ignorePath, result.FilePath + Environment.NewLine);
        }
        catch { /* Non-critical */ }

        await AvatarViewModel.Instance.ShowSpeechBubble(
            $"Ignored: {result.FileName} - Will be skipped in future scans.");
    }

    /// <summary>
    /// "Watch with AI" - monitors a threat for 60 seconds, collecting system context,
    /// then sends the data to the LLM for a detailed threat analysis. The analysis
    /// result is shown in a floating popup via <see cref="AiWatchAnalysis"/>.
    /// </summary>
    [RelayCommand]
    private async Task WatchWithAiAsync(ScanResult result)
    {
        if (result is null) return;
        if (IsAiWatching) return; // Only one AI watch session at a time

        IsAiWatching = true;
        AiWatchAnalysis = string.Empty;
        AiWatchProgress = 60;

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble(
            $"Watching threat with AI: {result.FileName} — monitoring for 60 seconds...");

        // Collect initial file information
        var fileExists = System.IO.File.Exists(result.FilePath);
        var fileInfoText = $"File: {result.FileName}\n" +
                           $"Path: {result.FilePath}\n" +
                           $"Exists: {fileExists}\n" +
                           $"Size: {result.FileSize} bytes\n" +
                           $"SHA-256: {result.Sha256Hash}\n" +
                           $"Threat Name: {result.ThreatName ?? "Unknown"}\n" +
                           $"Severity: {result.Severity}\n" +
                           $"Detection Method: {result.DetectionMethod}\n" +
                           $"Heuristic Score: {result.HeuristicScore:F1}\n" +
                           $"LLM Verdict: {result.LlmVerdict ?? "N/A"}\n" +
                           $"LLM Reasoning: {result.LlmReasoning ?? "N/A"}\n" +
                           $"Scanned At: {result.ScannedAt:O}\n";

        try
        {
            // Countdown timer: 60 seconds of monitoring
            for (var i = 60; i > 0; i--)
            {
                AiWatchProgress = i;
                await Task.Delay(1000);
            }
            AiWatchProgress = 0;

            // After the monitoring period, send to LLM for analysis
            if (_llmService != null)
            {
                var prompt =
                    "You are SyntheticAI, an advanced AI-powered antivirus security analyst.\n\n" +
                    "A potential threat was detected during a system scan. After a 60-second observation period, " +
                    "provide a detailed security analysis of this threat.\n\n" +
                    "== THREAT DATA ==\n" +
                    fileInfoText + "\n" +
                    "== ANALYSIS REQUEST ==\n" +
                    "1. Threat Classification: What type of malware or threat is this? (trojan, ransomware, PUP, adware, worm, rootkit, etc.)\n" +
                    "2. Risk Assessment: Rate the risk level (Critical / High / Medium / Low / False Positive) and explain why.\n" +
                    "3. Behavioral Indicators: Based on the file properties and detection method, what behaviors might this threat exhibit?\n" +
                    "4. Recommended Action: Should the user quarantine, delete, or ignore this file? Explain your reasoning.\n" +
                    "5. Additional Context: Any other relevant information about this threat family or detection.\n\n" +
                    "Provide a clear, detailed, and actionable analysis.";

                var analysis = await _llmService.AnalyzeAsync(prompt);
                AiWatchAnalysis = analysis;

                AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
                await AvatarViewModel.Instance.ShowSpeechBubble(
                    $"AI analysis complete for: {result.FileName}. Check the analysis panel for details.");
            }
            else
            {
                // No LLM service available - provide a fallback analysis based on scan data
                AiWatchAnalysis =
                    $"== AI Watch Analysis ==\n\n" +
                    $"Threat: {result.ThreatName ?? "Unknown"}\n" +
                    $"File: {result.FileName}\n" +
                    $"Severity: {result.Severity}\n" +
                    $"Detection: {result.DetectionMethod}\n" +
                    $"Heuristic Score: {result.HeuristicScore:F1}\n\n" +
                    $"Note: LLM service is not available. Connect to the SyntheticAI server " +
                    $"for full AI-powered threat analysis.\n\n" +
                    $"Based on the heuristic score of {result.HeuristicScore:F1} and detection method " +
                    $"({result.DetectionMethod}), this file was flagged as {result.Severity} severity.\n\n" +
                    $"Recommendation: Consider quarantining this file until a full AI analysis can be performed.";

                AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
                await AvatarViewModel.Instance.ShowSpeechBubble(
                    $"AI watch complete for {result.FileName}. LLM unavailable - basic analysis provided.");
            }
        }
        catch (Exception ex)
        {
            AiWatchAnalysis = $"AI analysis failed: {ex.Message}\n\nPlease try again or use manual threat inspection.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"AI watch failed for {result.FileName}: {ex.Message}");
        }
        finally
        {
            IsAiWatching = false;
        }
    }

    [RelayCommand]
    private void AddExtendedFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder to include in Extended Scan",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            if (!ExtendedScanFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
                ExtendedScanFolders.Add(dialog.FolderName);
        }
    }

    [RelayCommand]
    private void RemoveExtendedFolder(string folder)
    {
        if (!string.IsNullOrEmpty(folder))
            ExtendedScanFolders.Remove(folder);
    }
}
