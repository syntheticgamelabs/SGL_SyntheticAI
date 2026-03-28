// Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.
// NOTE: This is a simplified demonstration module. Production detection algorithms are proprietary.

using System;

namespace SyntheticAI.Demo;

/// <summary>
/// Simplified rule-based threat detector for demonstration only.
/// The production system uses proprietary ML models and behavioral analysis.
/// These rules are intentionally trivial and public-knowledge heuristics.
/// </summary>
public class DemoThreatDetector
{
    /// <summary>
    /// Checks if a telemetry event matches simple demo rules.
    /// This is NOT representative of actual detection capabilities.
    /// </summary>
    public bool IsSuspicious(TelemetryEvent telemetryEvent)
    {
        // Rule 1: powershell.exe spawned by cmd.exe
        if (telemetryEvent.ProcessName == "powershell.exe"
            && telemetryEvent.ParentProcess == "cmd.exe")
            return true;

        // Rule 2: cmd.exe making network connections
        if (telemetryEvent.ProcessName == "cmd.exe"
            && telemetryEvent.EventType == "NetworkConnection")
            return true;

        // Rule 3: Connections to uncommon ports (not 80, 443, 53)
        if (telemetryEvent.EventType == "NetworkConnection"
            && telemetryEvent.DestinationPort != 80
            && telemetryEvent.DestinationPort != 443
            && telemetryEvent.DestinationPort != 53)
            return true;

        return false;
    }

    /// <summary>
    /// Returns a simple threat score between 0.0 and 1.0.
    /// Production scoring uses multi-layered ensemble models.
    /// </summary>
    public double GetThreatScore(TelemetryEvent telemetryEvent)
    {
        double score = 0.0;

        if (telemetryEvent.ProcessName == "powershell.exe"
            && telemetryEvent.ParentProcess == "cmd.exe")
            score += 0.4;

        if (telemetryEvent.ProcessName == "cmd.exe"
            && telemetryEvent.EventType == "NetworkConnection")
            score += 0.3;

        if (telemetryEvent.EventType == "NetworkConnection"
            && telemetryEvent.DestinationPort != 80
            && telemetryEvent.DestinationPort != 443
            && telemetryEvent.DestinationPort != 53)
            score += 0.3;

        return Math.Min(score, 1.0);
    }
}
