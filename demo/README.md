# SyntheticAI Demo Module

**Synthetic Game Labs -- SyntheticAI v1.1.49 Demonstration Build**

---

> **DISCLAIMER**: This is a **simplified demonstration** of SyntheticAI system
> concepts. It does **NOT** contain production detection algorithms, trained models,
> or proprietary threat intelligence. All detection logic shown here uses trivial
> rule-based heuristics for illustration purposes only.

## What This Demo Shows

- **Telemetry Simulation** -- Generates realistic-looking endpoint telemetry events
  (process starts, network connections, file writes) using randomized data.
- **Rule-Based Detection** -- A handful of simple, publicly-known heuristic rules
  that flag obviously suspicious patterns (e.g., `cmd.exe` making network calls).
- **Threat Graph Construction** -- Builds a basic in-memory graph linking processes,
  devices, and network connections to visualize relationships.
- **REST API** -- Four endpoints that let you interact with the demo system.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## How to Run

```bash
cd demo
dotnet run
```

The API will start on `http://localhost:5000` by default.

## API Endpoints

| Method | Endpoint           | Description                         |
|--------|--------------------|-------------------------------------|
| GET    | `/demo/events`     | Generate a random telemetry event   |
| GET    | `/demo/threats`    | Return the current threat graph     |
| POST   | `/demo/analyze`    | Analyze a batch of generated events |
| GET    | `/demo/status`     | System info and graph statistics    |

### Example Usage

```bash
# Generate a random event
curl http://localhost:5000/demo/events

# Analyze a batch of 20 events
curl -X POST http://localhost:5000/demo/analyze \
  -H "Content-Type: application/json" \
  -d '{"count": 20}'

# View the threat graph
curl http://localhost:5000/demo/threats
```

## What This Is NOT

- This is **not** the production SyntheticAI engine.
- Detection rules here are intentionally simplistic and publicly documented patterns.
- No trained ML models, behavioral signatures, or proprietary data are included.
- Performance characteristics do not represent the production system.

---

*Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.*
