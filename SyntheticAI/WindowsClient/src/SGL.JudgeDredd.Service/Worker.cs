using SGL.JudgeDredd.Antivirus.Scanners;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.KnowledgeBase;
using SGL.JudgeDredd.Security.Monitors;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Service;

/// <summary>
/// Background worker service that provides continuous security protection.
/// Runs scan engine real-time protection, security monitors, and scheduled scans.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IScanEngine _scanEngine;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly AppSettings _settings;
    private readonly IKnowledgeBase _knowledgeBase;
    private Timer? _scheduledScanTimer;

    public Worker(
        ILogger<Worker> logger,
        IScanEngine scanEngine,
        ISecurityMonitor securityMonitor,
        AppSettings settings,
        IKnowledgeBase knowledgeBase)
    {
        _logger = logger;
        _scanEngine = scanEngine;
        _securityMonitor = securityMonitor;
        _settings = settings;
        _knowledgeBase = knowledgeBase;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SGL SyntheticAI Service starting...");
        SglLogger.Initialize(Path.Combine(AppContext.BaseDirectory, "logs"));
        SglLogger.Information("Background service worker started.");

        // Subscribe to threat events
        _scanEngine.ThreatDetected += OnThreatDetected;
        _securityMonitor.AlertRaised += OnAlertRaised;

        // Start real-time file protection
        if (_settings.Scanner.RealTimeProtection)
        {
            _logger.LogInformation("Starting real-time file protection...");
            _scanEngine.StartRealTimeProtection();
        }

        // Start security monitors
        _logger.LogInformation("Starting security monitors...");
        _securityMonitor.StartAllMonitors();

        // Setup scheduled scan if configured
        if (_settings.Scanner.ScheduledScanTime.HasValue)
        {
            SetupScheduledScan(_settings.Scanner.ScheduledScanTime.Value);
        }

        _logger.LogInformation("SGL SyntheticAI Service is running. Protection active.");

        // Main service loop - periodic health check
        while (!stoppingToken.IsCancellationRequested)
        {
            // Log health status every 5 minutes
            var alerts = _securityMonitor.GetActiveAlerts();
            var unacknowledged = alerts.Count(a => !a.IsAcknowledged);

            _logger.LogInformation(
                "Health check: Real-time protection: {RtStatus}, Active alerts: {AlertCount}, Unacknowledged: {Unack}",
                _scanEngine.IsRealTimeProtectionActive ? "ON" : "OFF",
                alerts.Count,
                unacknowledged);

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private void SetupScheduledScan(TimeSpan scheduledTime)
    {
        var now = DateTime.Now;
        var nextRun = now.Date.Add(scheduledTime);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        var delay = nextRun - now;
        _logger.LogInformation("Scheduled scan set for {Time} (in {Delay})", nextRun, delay);

        _scheduledScanTimer = new Timer(async _ =>
        {
            _logger.LogInformation("Running scheduled quick scan...");
            try
            {
                var progress = new Progress<ScanProgressEvent>(p =>
                    _logger.LogInformation("Scan progress: {Percent}% - {File}",
                        p.ProgressPercent.ToString("F1"), p.CurrentFile));

                var session = await _scanEngine.QuickScanAsync(progress);

                _logger.LogInformation(
                    "Scheduled scan complete: {Total} files, {Threats} threats found.",
                    session.ScannedFiles, session.ThreatsFound);

                await _knowledgeBase.LogScanResultAsync(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled scan failed.");
            }

            // Schedule next run in 24 hours
            _scheduledScanTimer?.Change(TimeSpan.FromHours(24), Timeout.InfiniteTimeSpan);

        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private void OnThreatDetected(object? sender, ThreatDetectedEvent e)
    {
        _logger.LogWarning(
            "THREAT DETECTED: {ThreatName} in file {File} (Severity: {Severity})",
            e.Result.ThreatName ?? "Unknown",
            e.Result.FilePath,
            e.Result.Severity);

        SglLogger.Warning(
            $"Threat detected: {e.Result.ThreatName} at {e.Result.FilePath}");
    }

    private void OnAlertRaised(object? sender, SecurityAlertEvent e)
    {
        _logger.LogWarning(
            "SECURITY ALERT: [{Category}] {Title} - {Description}",
            e.Alert.Category,
            e.Alert.Title,
            e.Alert.Description);

        SglLogger.Warning(
            $"Security alert: [{e.Alert.Category}] {e.Alert.Title}");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SGL SyntheticAI Service stopping...");

        _scanEngine.StopRealTimeProtection();
        _securityMonitor.StopAllMonitors();
        _scheduledScanTimer?.Dispose();

        _scanEngine.ThreatDetected -= OnThreatDetected;
        _securityMonitor.AlertRaised -= OnAlertRaised;

        SglLogger.Information("Background service worker stopped.");
        SglLogger.CloseAndFlush();

        await base.StopAsync(cancellationToken);
    }
}
