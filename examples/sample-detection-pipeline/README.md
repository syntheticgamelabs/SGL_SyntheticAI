# Example: Detection Pipeline Concept

**Synthetic Game Labs -- SyntheticAI**

---

> **NOTE**: This example illustrates the *concept* of a detection pipeline.
> Production detection uses proprietary ML models and is not included here.

## What Is a Detection Pipeline?

A detection pipeline is a series of processing stages that telemetry events
pass through to determine whether they represent a threat. Each stage adds
context, applies analysis, and either escalates or clears the event.

## Pipeline Stages (Conceptual)

```
  Telemetry Event
       |
       v
  +--------------------+
  | 1. INGESTION       |  Normalize and validate raw event data
  +--------------------+
       |
       v
  +--------------------+
  | 2. ENRICHMENT      |  Add context: process reputation, geo-IP,
  +--------------------+  file hash lookups, historical frequency
       |
       v
  +--------------------+
  | 3. RULE ENGINE     |  Apply deterministic rules for known-bad patterns
  +--------------------+
       |
       v
  +--------------------+
  | 4. SCORING         |  Assign a threat score (0.0 to 1.0) based on
  +--------------------+  combined signals from all prior stages
       |
       v
  +--------------------+
  | 5. GRAPH UPDATE    |  Insert event into the threat graph and
  +--------------------+  propagate risk to connected nodes
       |
       v
  +--------------------+
  | 6. ALERTING        |  Generate alerts for events above threshold
  +--------------------+
```

## How the Demo Implements This

The demo module provides a simplified version of stages 1, 3, 4, and 5:

- **TelemetrySimulator** generates synthetic events (stage 1).
- **DemoThreatDetector** applies trivial rules and scoring (stages 3-4).
- **ThreatGraphBuilder** constructs a basic graph (stage 5).

Stages 2 and 6 (enrichment and alerting) are omitted from the demo for simplicity.

See `demo/DemoThreatDetector.cs` and `demo/DemoApi.cs` for the simplified code.

---

*Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.*
