// Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.
// NOTE: This is a simplified demonstration module. Production detection algorithms are proprietary.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SyntheticAI.Demo;

/// <summary>
/// Generates simulated telemetry events for demonstration purposes.
/// Real telemetry ingestion uses proprietary collection agents.
/// </summary>
public class TelemetryEvent
{
    public string DeviceId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ParentProcess { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string DestinationIP { get; set; } = string.Empty;
    public int DestinationPort { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
}

public class TelemetrySimulator
{
    private static readonly string[] ProcessNames =
    {
        "explorer.exe", "chrome.exe", "powershell.exe",
        "cmd.exe", "svchost.exe", "notepad.exe"
    };

    private static readonly string[] EventTypes =
    {
        "ProcessStart", "NetworkConnection", "FileWrite",
        "RegistryModification", "DnsQuery"
    };

    private static readonly int[] CommonPorts = { 80, 443, 8080, 53, 22, 3389, 445, 8443, 9090, 4444 };

    private readonly Random _random = new();

    /// <summary>
    /// Generates a single random telemetry event for demonstration.
    /// </summary>
    public TelemetryEvent GenerateEvent()
    {
        var process = ProcessNames[_random.Next(ProcessNames.Length)];
        var parent = ProcessNames[_random.Next(ProcessNames.Length)];

        return new TelemetryEvent
        {
            DeviceId = $"DEVICE-{_random.Next(1000, 9999)}",
            ProcessName = process,
            ParentProcess = parent,
            FileHash = GenerateFakeHash(),
            DestinationIP = $"{_random.Next(10, 220)}.{_random.Next(0, 255)}.{_random.Next(0, 255)}.{_random.Next(1, 254)}",
            DestinationPort = CommonPorts[_random.Next(CommonPorts.Length)],
            Timestamp = DateTime.UtcNow.AddSeconds(-_random.Next(0, 86400)),
            EventType = EventTypes[_random.Next(EventTypes.Length)]
        };
    }

    /// <summary>
    /// Generates a batch of random telemetry events.
    /// </summary>
    public List<TelemetryEvent> GenerateBatch(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateEvent())
            .ToList();
    }

    private string GenerateFakeHash()
    {
        var bytes = new byte[16];
        _random.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
