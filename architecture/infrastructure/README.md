# Infrastructure Architecture

> SyntheticAI Deployment & Infrastructure

## Server Deployment Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    SERVER HOST                                │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  SyntheticAI Server Application                        │  │
│  │  (.NET 8 Self-Contained, Single Binary)                │  │
│  │                                                        │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Kestrel Web Server                              │  │  │
│  │  │  ├── REST API (HTTPS, Port 5443)                 │  │  │
│  │  │  ├── WebSocket (Real-time events)                │  │  │
│  │  │  ├── Static Files (Admin UI, Client Downloads)   │  │  │
│  │  │  └── Health Check (/api/system/health)           │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                                                        │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Security Engines                                │  │  │
│  │  │  ├── Evidence Graph Engine                       │  │  │
│  │  │  ├── ML Ensemble (LR + GBDT + GraphML)          │  │  │
│  │  │  ├── ASRE Pipeline                               │  │  │
│  │  │  ├── Deep Packet Inspector                       │  │  │
│  │  │  └── Temporal Attack Path Analyzer               │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                                                        │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  AI Inference                                    │  │  │
│  │  │  ├── LLamaSharp Runtime                          │  │  │
│  │  │  └── GGUF Model Files (4-70B parameters)         │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Data Layer                                            │  │
│  │  ├── Security Data Lake (JSONL)                        │  │
│  │  ├── Threat Signatures                                 │  │
│  │  ├── Knowledge Base                                    │  │
│  │  └── Configuration (settings.json)                     │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Bundled Services                                      │  │
│  │  ├── Client Installers (Win/Linux/Android)             │  │
│  │  ├── Static Website                                    │  │
│  │  ├── TTS Engine                                        │  │
│  │  └── SD WebUI                                          │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  Total Deployment Size: ~60GB (with all LLM models)          │
│  Installer: Inno Setup 6 (DiskSpanning, 2GB slices)          │
└──────────────────────────────────────────────────────────────┘
```

## Client Deployment Architecture

```
┌──────────────────────────────────────┐
│          CLIENT ENDPOINT             │
│                                      │
│  ┌────────────────────────────────┐  │
│  │  SyntheticAI Client            │  │
│  │  (.NET 8 Self-Contained)       │  │
│  │                                │  │
│  │  ├── Process Monitor           │  │
│  │  ├── File Scanner              │  │
│  │  ├── Network Monitor           │  │
│  │  ├── Firewall Manager          │  │
│  │  ├── Local Threat Scorer       │  │
│  │  └── Server Sync Agent         │  │
│  └────────────────────────────────┘  │
│                                      │
│  Client Size: ~250MB                 │
│  Installer: Inno Setup 6 (~82MB)     │
└──────────────────────────────────────┘
```

## Network Topology

```
                    ┌──────────────┐
                    │   Internet   │
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │   Firewall   │
                    └──────┬───────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
       ┌──────▼──────┐    │     ┌──────▼──────┐
       │  Server      │    │     │  Server     │
       │  (Primary)   │    │     │  (Future    │
       │  Port 5443   │    │     │   HA node)  │
       └──────────────┘    │     └─────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
  ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐
  │  Endpoint 1 │  │  Endpoint 2 │  │  Endpoint N │
  │  (Windows)  │  │  (Linux)    │  │  (Android)  │
  └─────────────┘  └─────────────┘  └─────────────┘
```

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs</sub>
</p>
