# System Diagrams

> Visual architecture references for SyntheticAI

## High-Level System Architecture

```
╔══════════════════════════════════════════════════════════════╗
║                   SYNTHETICAI PLATFORM                       ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║   ENDPOINTS                    SERVER                        ║
║   ┌─────────┐                  ┌──────────────────────┐      ║
║   │ Windows │──┐               │  Kestrel API Server  │      ║
║   │ Client  │  │    HTTPS      │  ┌────────────────┐  │      ║
║   └─────────┘  ├──────────────▶│  │ 44+ Endpoints  │  │      ║
║   ┌─────────┐  │    JWT Auth   │  └────────┬───────┘  │      ║
║   │  Linux  │──┤               │           │          │      ║
║   │ Client  │  │               │  ┌────────▼───────┐  │      ║
║   └─────────┘  │               │  │  Processing    │  │      ║
║   ┌─────────┐  │               │  │  Pipeline      │  │      ║
║   │ Android │──┘               │  └────────┬───────┘  │      ║
║   │ Agent   │                  │           │          │      ║
║   └─────────┘                  │  ┌────────▼───────┐  │      ║
║                                │  │  Security      │  │      ║
║                                │  │  Data Lake     │  │      ║
║                                │  └────────┬───────┘  │      ║
║                                │           │          │      ║
║                                │  ┌────────▼───────┐  │      ║
║                                │  │  AI + ML       │  │      ║
║                                │  │  Engines       │  │      ║
║                                │  └────────┬───────┘  │      ║
║                                │           │          │      ║
║                                │  ┌────────▼───────┐  │      ║
║                                │  │  Response &    │  │      ║
║                                │  │  Visualization │  │      ║
║                                │  └────────────────┘  │      ║
║                                └──────────────────────┘      ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
```

## Processing Pipeline Detail

```
Endpoint Agent
    │
    │ HTTPS POST /api/telemetry/batch
    ▼
┌─────────────────────────────────────────────┐
│              API Gateway                     │
│  JWT Validation → CSRF Check → Rate Limit   │
└──────────────────┬──────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────┐
│              EventBus                        │
│  BlockingCollection (10K capacity)           │
│  Topic-based routing │ Dedicated thread      │
└──────────────────┬──────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────┐
│           DataLake Ingestor                  │
│  Normalize → Enrich → Index → Store          │
└──────┬───────────┬──────────┬───────────────┘
       │           │          │
       ▼           ▼          ▼
   EventStore  TelemetryRepo  NetworkFlowRepo
   (JSONL)     (In-memory)    (Flow tracking)
```

## ML Analysis Pipeline

```
  Raw Telemetry
       │
       ▼
┌──────────────┐     20-dimensional
│ Feature Store │ ──▶ normalized vector
└──────┬───────┘
       │
  ┌────┼────────────────┐
  │    │                 │
  ▼    ▼                 ▼
┌────┐ ┌─────────┐ ┌─────────┐
│ LR │ │  GBDT   │ │ GraphML │
│    │ │         │ │         │
│ w₁ │ │   w₂    │ │   w₃    │
└─┬──┘ └───┬─────┘ └───┬─────┘
  │        │            │
  └────────┼────────────┘
           │
    ┌──────▼──────┐
    │  Ensemble   │
    │  Weighted   │
    │  Average    │
    └──────┬──────┘
           │
           ▼
    Risk: 0.0 ─ 1.0
```

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs</sub>
</p>
