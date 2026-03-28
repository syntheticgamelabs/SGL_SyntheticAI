# Example: Simulated Telemetry

**Synthetic Game Labs -- SyntheticAI**

---

> **NOTE**: This example illustrates the *concept* of telemetry simulation.
> Production telemetry agents and data formats are proprietary.

## What Is Telemetry Simulation?

Telemetry simulation generates realistic-looking endpoint event data
without requiring a live environment. This is useful for testing detection
logic, training analysts, and demonstrating system capabilities to
stakeholders.

## Event Types

The demo simulator generates these event types:

| Event Type             | Description                                |
|------------------------|--------------------------------------------|
| `ProcessStart`         | A new process was launched                 |
| `NetworkConnection`    | A process opened a network connection      |
| `FileWrite`            | A process wrote data to disk               |
| `RegistryModification` | A process modified a registry key          |
| `DnsQuery`             | A process performed a DNS lookup           |

## Event Fields

Each simulated event contains:

- **DeviceId** -- Randomized device identifier (e.g., `DEVICE-4821`)
- **ProcessName** -- One of several common Windows processes
- **ParentProcess** -- The process that spawned this one
- **FileHash** -- A randomized hex string simulating a file hash
- **DestinationIP** -- A randomized IP address
- **DestinationPort** -- A port number from a predefined set
- **Timestamp** -- A UTC timestamp within the last 24 hours
- **EventType** -- One of the event types listed above

## Usage

See `demo/TelemetrySimulator.cs` for the generator code and
`demo_data/telemetry_sample.json` for 20 pre-generated sample events.

```csharp
var simulator = new TelemetrySimulator();
var singleEvent = simulator.GenerateEvent();
var batch = simulator.GenerateBatch(50);
```

---

*Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.*
