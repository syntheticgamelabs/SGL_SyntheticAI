// Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.
// NOTE: This is a simplified demonstration module. Production detection algorithms are proprietary.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntheticAI.Demo;

/// <summary>
/// Represents a node in a simplified threat graph.
/// Production graphs use proprietary weighted-edge models with temporal decay.
/// </summary>
public class ThreatNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Connections { get; set; } = new();
    public double RiskScore { get; set; }
}

/// <summary>
/// Builds an in-memory graph from telemetry events for demonstration.
/// The production graph engine is a proprietary distributed system.
/// </summary>
public class ThreatGraphBuilder
{
    private readonly Dictionary<string, ThreatNode> _nodes = new();
    private readonly DemoThreatDetector _detector = new();

    /// <summary>
    /// Adds a telemetry event to the graph, creating nodes and edges.
    /// </summary>
    public void AddEvent(TelemetryEvent telemetryEvent)
    {
        var processNode = GetOrCreateNode(telemetryEvent.ProcessName, "Process");
        var parentNode = GetOrCreateNode(telemetryEvent.ParentProcess, "Process");
        var deviceNode = GetOrCreateNode(telemetryEvent.DeviceId, "Device");

        AddConnection(parentNode, processNode);
        AddConnection(deviceNode, processNode);

        if (telemetryEvent.EventType == "NetworkConnection")
        {
            var ipNode = GetOrCreateNode(telemetryEvent.DestinationIP, "Network");
            AddConnection(processNode, ipNode);
        }

        processNode.RiskScore = Math.Max(
            processNode.RiskScore,
            _detector.GetThreatScore(telemetryEvent));
    }

    /// <summary>
    /// Returns all nodes currently in the graph.
    /// </summary>
    public List<ThreatNode> GetGraph() => _nodes.Values.ToList();

    /// <summary>
    /// Returns all connections for a given node ID.
    /// </summary>
    public List<string> GetConnections(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node)
            ? node.Connections
            : new List<string>();
    }

    private ThreatNode GetOrCreateNode(string id, string type)
    {
        if (!_nodes.TryGetValue(id, out var node))
        {
            node = new ThreatNode { Id = id, Type = type };
            _nodes[id] = node;
        }
        return node;
    }

    private static void AddConnection(ThreatNode from, ThreatNode to)
    {
        if (!from.Connections.Contains(to.Id))
            from.Connections.Add(to.Id);
    }
}
