# Example: Threat Graph Concept

**Synthetic Game Labs -- SyntheticAI**

---

> **NOTE**: This example illustrates the *concept* of a threat graph.
> The production graph engine uses proprietary algorithms and is not shown here.

## What Is a Threat Graph?

A threat graph maps relationships between entities observed in endpoint
telemetry -- processes, network connections, devices, and files. By
connecting these entities, analysts can trace attack chains and identify
lateral movement across an environment.

## Conceptual Structure

```
                 +-----------+
                 | DEVICE    |
                 | DEVICE-42 |
                 +-----+-----+
                       |
                 spawns|
                       v
  +-----------+  +-----+------+  connects   +--------------+
  | cmd.exe   +->| powershell |------------>| 192.168.1.50 |
  | (parent)  |  | .exe       |             | :4444        |
  +-----------+  +-----+------+             +--------------+
                       |
                 writes|
                       v
                 +-----+------+
                 | payload.dll|
                 | (FileWrite)|
                 +------------+
```

## How the Demo Uses This

The `ThreatGraphBuilder` in the demo module creates an in-memory graph
by processing simulated telemetry events. Each event adds nodes (processes,
devices, IPs) and edges (spawns, connects-to) to the graph. Risk scores
propagate from flagged events to their associated nodes.

## Key Concepts

- **Nodes** represent entities: processes, devices, IP addresses, files.
- **Edges** represent relationships: spawned-by, connected-to, wrote-file.
- **Risk Scores** are assigned based on detection rules and accumulate on nodes.
- **Traversal** allows tracing an attack path from initial access to impact.

See `demo/ThreatGraphBuilder.cs` for the simplified implementation and
`demo_data/threat_graph_sample.json` for sample graph data.

---

*Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.*
