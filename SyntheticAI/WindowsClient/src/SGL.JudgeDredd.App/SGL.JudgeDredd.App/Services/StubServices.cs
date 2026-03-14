using System.Runtime.CompilerServices;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Events;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.Services;

/// <summary>
/// Stub scan engine that provides safe defaults without crashing on construction.
/// Will be replaced by the real ScanEngine in a later phase.
/// </summary>
public sealed class StubScanEngine : IScanEngine
{
    public bool IsRealTimeProtectionActive => false;

#pragma warning disable CS0067 // Event is never used (required by IScanEngine interface)
    public event EventHandler<ThreatDetectedEvent>? ThreatDetected;
#pragma warning restore CS0067

    public Task<ScanSession> ScanDirectoryAsync(string path, ScanType type,
        IProgress<ScanProgressEvent>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered. This stub will be replaced in a later phase.");

    public Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered.");

    public Task<ScanSession> QuickScanAsync(IProgress<ScanProgressEvent>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered.");

    public Task<ScanSession> FullScanAsync(IProgress<ScanProgressEvent>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered.");

    public Task<ScanSession> ExtendedScanAsync(IEnumerable<string>? additionalPaths = null,
        IProgress<ScanProgressEvent>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered.");

    public Task<ScanSession> ScanRemovableDriveAsync(string driveLetter,
        IProgress<ScanProgressEvent>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException("ScanEngine not yet registered.");

    public void StartRealTimeProtection() { /* no-op */ }
    public void StopRealTimeProtection() { /* no-op */ }
}

/// <summary>
/// Stub firewall manager that returns empty collections without crashing on construction.
/// Will be replaced by the real FirewallManager in a later phase.
/// </summary>
public sealed class StubFirewallManager : IFirewallManager
{
    private readonly List<FirewallRule> _rules = [];

    public IReadOnlyList<FirewallRule> GetAllRules() => _rules.AsReadOnly();

    public void AddRule(FirewallRule rule) => _rules.Add(rule);

    public void RemoveRule(string name) => _rules.RemoveAll(r => r.Name == name);

    public void EnableRule(string name)
    {
        var rule = _rules.FirstOrDefault(r => r.Name == name);
        if (rule is not null) rule.Enabled = true;
    }

    public void DisableRule(string name)
    {
        var rule = _rules.FirstOrDefault(r => r.Name == name);
        if (rule is not null) rule.Enabled = false;
    }

    public void ApplyPreset(FirewallPreset preset) { /* no-op stub */ }
    public void RevertPreset(FirewallPreset preset) { /* no-op stub */ }

    public IReadOnlyList<NetworkConnection> GetActiveConnections() => [];
}

/// <summary>
/// Stub security monitor that provides empty alert lists without crashing on construction.
/// Will be replaced by the real SecurityMonitor in a later phase.
/// </summary>
public sealed class StubSecurityMonitor : ISecurityMonitor
{
#pragma warning disable CS0067 // Event is never used (required by ISecurityMonitor interface)
    public event EventHandler<SecurityAlertEvent>? AlertRaised;
#pragma warning restore CS0067

    public void StartAllMonitors() { /* no-op stub */ }
    public void StopAllMonitors() { /* no-op stub */ }

    public IReadOnlyList<SecurityAlert> GetActiveAlerts() => [];
}

/// <summary>
/// Stub LLM service that reports model as not loaded and throws on generation calls.
/// Will be replaced by the real LlmService in a later phase.
/// </summary>
public sealed class StubLlmService : ILlmService
{
    public bool IsModelLoaded => false;
    public string? ModelName => null;

    public async IAsyncEnumerable<string> ChatAsync(string userMessage, string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "[LLM service not yet initialized. Model will be loaded in a future phase.]";
        await Task.CompletedTask;
    }

    public Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult("[LLM analysis not available - stub service]");

    public Task<ThreatAnalysisResult> AnalyzeThreatAsync(ThreatInfo threat, CancellationToken ct = default)
        => Task.FromResult(new ThreatAnalysisResult
        {
            Verdict = "Unknown",
            Reasoning = "Stub LLM service - no analysis performed.",
            Confidence = 0,
            ThreatType = string.Empty,
            Recommendation = "LLM service not initialized."
        });

    public async IAsyncEnumerable<string> GenerateCodeAsync(string language, string description,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "// Code generation not available - LLM stub service";
        await Task.CompletedTask;
    }

    public Task LoadModelAsync(IProgress<double>? progress = null, CancellationToken ct = default)
        => Task.CompletedTask;
}
