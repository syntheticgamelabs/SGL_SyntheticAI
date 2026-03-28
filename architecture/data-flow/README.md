# Data Flow Architecture

> SyntheticAI Event Processing Pipeline

## End-to-End Data Flow

```
┌──────────────────────────────────────────────────────────────────────┐
│                        DATA SOURCES                                  │
│                                                                      │
│  Process Events   File Events   Network Events   Registry Events     │
│       │               │              │                │              │
│       └───────────────┼──────────────┼────────────────┘              │
│                       │              │                                │
│                       ▼              ▼                                │
│              ┌─────────────────────────────┐                         │
│              │    Telemetry Collector       │                         │
│              │    (Agent-side batching)     │                         │
│              └────────────┬────────────────┘                         │
└───────────────────────────┼──────────────────────────────────────────┘
                            │
                    HTTPS + JWT Token
                            │
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     SERVER INGESTION                                 │
│                                                                      │
│  ┌──────────┐    ┌──────────────┐    ┌───────────────────────────┐   │
│  │ API      │──▶ │  Validation  │──▶ │  EventBus                 │   │
│  │ Endpoint │    │  & Auth      │    │  (BlockingCollection)     │   │
│  └──────────┘    └──────────────┘    │  10K capacity             │   │
│                                      │  Dedicated dispatch thread│   │
│                                      └───────────┬───────────────┘   │
└──────────────────────────────────────────────────┼───────────────────┘
                                                   │
                                                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     NORMALIZATION LAYER                               │
│                                                                      │
│  ┌────────────────┐  ┌─────────────────┐  ┌───────────────────────┐  │
│  │ Schema Map     │  │ Field Enrichment│  │ Deduplication         │  │
│  │ (canonical     │  │ (geo-IP, ASN,   │  │ (hash-based           │  │
│  │  event model)  │  │  threat intel)  │  │  event dedup)         │  │
│  └───────┬────────┘  └───────┬─────────┘  └──────────┬────────────┘  │
│          └───────────────────┼────────────────────────┘              │
│                              │                                       │
└──────────────────────────────┼───────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     SECURITY DATA LAKE                                │
│                                                                      │
│  ┌─────────────────────┐                                             │
│  │      HOT TIER       │  ← Today's events, in-memory               │
│  │  (ConcurrentQueue)  │  ← Sub-millisecond query                   │
│  │  5-sec flush cycle  │  ← Auto-eviction at 100K                   │
│  ├─────────────────────┤                                             │
│  │     WARM TIER       │  ← Secondary indexes                       │
│  │  (ConcurrentDict)   │  ← By hash, IP, host, process             │
│  │  In-memory indexes  │  ← Fast lookup                             │
│  ├─────────────────────┤                                             │
│  │     COLD TIER       │  ← Append-only JSONL files                 │
│  │  (Disk Storage)     │  ← Daily rotation                          │
│  │  Compressed archive │  ← Full historical record                  │
│  └─────────────────────┘                                             │
│                                                                      │
└──────────────────────────────┬───────────────────────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
┌────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  ML Pipeline   │ │ Threat Intel     │ │ AI Investigation │
│                │ │                  │ │                  │
│  Feature       │ │ Evidence Graph   │ │ LLM Analysis     │
│  Extraction    │ │ Query Engine     │ │ ASRE Pipeline    │
│  ↓             │ │ Timeline Recon   │ │ Report Gen       │
│  Ensemble      │ │ Campaign Det.    │ │ Response Orch.   │
│  Classification│ │ Threat Hunter    │ │                  │
└───────┬────────┘ └───────┬──────────┘ └───────┬──────────┘
        │                  │                     │
        └──────────────────┼─────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────────────┐
│                        OUTPUT LAYER                                  │
│                                                                      │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌────────────────────┐  │
│  │Dashboard │  │ Alerts &  │  │ 3D Viz   │  │ Push Notifications │  │
│  │ (Admin)  │  │ Reports   │  │ Universe │  │ (FCM)              │  │
│  └──────────┘  └───────────┘  └──────────┘  └────────────────────┘  │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  Automated Response Actions                                  │    │
│  │  ├── Quarantine File                                         │    │
│  │  ├── Block IP (Firewall Rule)                                │    │
│  │  ├── Kill Process                                            │    │
│  │  └── Isolate Host                                            │    │
│  └──────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs</sub>
</p>
