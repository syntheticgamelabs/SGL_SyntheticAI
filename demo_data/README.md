# Demo Data

**Synthetic Game Labs -- SyntheticAI**

---

> **DISCLAIMER**: All data in this directory is **synthetically generated** for
> demonstration purposes. No real telemetry, threat data, or production
> artifacts are included.

## Files

| File                        | Description                                    |
|-----------------------------|------------------------------------------------|
| `telemetry_sample.json`     | 20 simulated telemetry events with randomized   |
|                             | process names, IPs, ports, and timestamps.      |
| `threat_graph_sample.json`  | A sample threat graph with 8 nodes and 10 edges |
|                             | showing relationships between entities.         |

## Data Format

- **Telemetry events** follow the `TelemetryEvent` schema defined in
  `demo/TelemetrySimulator.cs`.
- **Threat graph data** follows the `ThreatNode` schema defined in
  `demo/ThreatGraphBuilder.cs`, with an additional edges array.

These files can be used for testing, UI prototyping, or demonstration
without running the live demo API.

---

*Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.*
