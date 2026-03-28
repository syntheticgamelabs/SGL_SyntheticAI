# SyntheticAI — System Architecture

> Version 1.1.49 Beta | Synthetic Game Labs | 2026

## Overview

SyntheticAI is a multi-platform autonomous cyber defense system built on .NET 8. The architecture follows a distributed client-server model where lightweight endpoint agents collect telemetry and a centralized server platform performs analysis, investigation, and response orchestration.

---

## High-Level Architecture

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                          SyntheticAI Platform                               ║
╠══════════════════════════════════════════════════════════════════════════════╣
║                                                                             ║
║  ┌────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────────┐     ║
║  │  Windows   │   │   Linux    │   │  Android   │   │  Future:       │     ║
║  │  Client    │   │   Client   │   │  Agent     │   │  macOS / iOS   │     ║
║  │  (WPF)     │   │ (Console)  │   │  (MAUI)    │   │                │     ║
║  └─────┬──────┘   └─────┬──────┘   └─────┬──────┘   └────────────────┘     ║
║        │                │                 │                                  ║
║        └────────────────┼─────────────────┘                                  ║
║                         │                                                    ║
║                  ┌──────▼──────┐                                             ║
║                  │  REST API   │  ← ASP.NET Core Minimal API (Kestrel)      ║
║                  │  WebSocket  │  ← Real-time event streaming               ║
║                  │  JWT Auth   │  ← HMAC-SHA256, 512-bit key                ║
║                  └──────┬──────┘                                             ║
║                         │                                                    ║
║  ╔══════════════════════▼══════════════════════════════════════════════╗     ║
║  ║                   Core Processing Pipeline                         ║     ║
║  ║                                                                    ║     ║
║  ║  ┌──────────────┐  ┌────────────────┐  ┌───────────────────────┐  ║     ║
║  ║  │  EventBus    │→ │  Normalizer    │→ │  Security Data Lake   │  ║     ║
║  ║  │  (Pub/Sub)   │  │  (Enrichment)  │  │  (Hot/Warm/Cold)      │  ║     ║
║  ║  └──────────────┘  └────────────────┘  └───────────┬───────────┘  ║     ║
║  ║                                                     │              ║     ║
║  ║  ┌─────────────────────────────────────────────────┤              ║     ║
║  ║  │                                                  │              ║     ║
║  ║  ▼                                                  ▼              ║     ║
║  ║  ┌──────────────────────┐  ┌───────────────────────────────────┐  ║     ║
║  ║  │  Threat Intelligence │  │  ML Analysis Engine               │  ║     ║
║  ║  │  ┌────────────────┐  │  │  ┌─────────────────────────────┐  │  ║     ║
║  ║  │  │ Evidence Graph │  │  │  │ Gradient Boosting Trees     │  │  ║     ║
║  ║  │  │ Query Engine   │  │  │  │ Graph ML Risk Propagation   │  │  ║     ║
║  ║  │  │ Timeline Recon │  │  │  │ Logistic Regression         │  │  ║     ║
║  ║  │  │ Campaign Det.  │  │  │  │ Feature Store               │  │  ║     ║
║  ║  │  │ Threat Hunter  │  │  │  │ Training Scheduler          │  │  ║     ║
║  ║  │  └────────────────┘  │  │  └─────────────────────────────┘  │  ║     ║
║  ║  └──────────────────────┘  └───────────────────────────────────┘  ║     ║
║  ║                                                                    ║     ║
║  ║  ┌──────────────────────┐  ┌───────────────────────────────────┐  ║     ║
║  ║  │  AI Investigation    │  │  Network Analysis                 │  ║     ║
║  ║  │  ┌────────────────┐  │  │  ┌─────────────────────────────┐  │  ║     ║
║  ║  │  │ LLM Inference  │  │  │  │ Deep Packet Inspection      │  │  ║     ║
║  ║  │  │ ASRE Pipeline  │  │  │  │ DNS Exfiltration Detection  │  │  ║     ║
║  ║  │  │ Response Orch. │  │  │  │ TLS/SNI Analysis            │  │  ║     ║
║  ║  │  │ Threat Sim.    │  │  │  │ Protocol Anomaly Detection  │  │  ║     ║
║  ║  │  └────────────────┘  │  │  └─────────────────────────────┘  │  ║     ║
║  ║  └──────────────────────┘  └───────────────────────────────────┘  ║     ║
║  ║                                                                    ║     ║
║  ╚════════════════════════════════════════════════════════════════════╝     ║
║                         │                                                    ║
║  ┌──────────────────────▼──────────────────────────────────────────────┐    ║
║  │                    Output / Response Layer                          │    ║
║  │  ┌────────────┐ ┌──────────┐ ┌───────────┐ ┌────────────────────┐  │    ║
║  │  │ Admin UI   │ │ Alerts   │ │ 3D Threat │ │ Push Notifications │  │    ║
║  │  │ Dashboard  │ │ & Reports│ │ Universe  │ │ (FCM)              │  │    ║
║  │  └────────────┘ └──────────┘ └───────────┘ └────────────────────┘  │    ║
║  └─────────────────────────────────────────────────────────────────────┘    ║
║                                                                             ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

---

## Component Details

### 1. Endpoint Agents

Lightweight agents deployed on endpoints that collect process telemetry, file system events, network connections, and registry modifications.

| Component | Platform | Framework |
|-----------|----------|-----------|
| Windows Desktop Agent | Windows 10/11/Server | .NET 8 WPF |
| Linux Agent | Ubuntu/Debian/RHEL | .NET 8 Console |
| Mobile Agent | Android 10+ | .NET MAUI |

**Agent Responsibilities:**
- Process creation/termination monitoring
- File hash computation and file system audit
- Network connection tracking
- Registry modification detection (Windows)
- Firewall rule management
- Local threat scoring

### 2. Server Platform

The server is a self-hosted ASP.NET Core application using Kestrel, providing:

- **44+ REST API endpoints** across 12 endpoint groups
- **WebSocket** real-time streaming for live telemetry and alerts
- **JWT Authentication** with HMAC-SHA256 and 512-bit keys
- **CSRF Protection** with Origin/Referer validation
- **Rate Limiting** per-IP WebSocket connection management
- **Static file hosting** for Admin UI and client downloads

### 3. Security Data Lake

A tiered storage architecture for security event data:

```
┌─────────────────────────────────────────────────┐
│                Security Data Lake                │
│                                                  │
│  ┌──────────┐   Millisecond queries              │
│  │ HOT      │   Today's events in-memory         │
│  │ (Memory) │   ConcurrentQueue write buffer     │
│  ├──────────┤                                    │
│  │ WARM     │   Secondary indexes                │
│  │ (Index)  │   Hash, IP, Host, Process lookup   │
│  ├──────────┤                                    │
│  │ COLD     │   Append-only JSONL                │
│  │ (Disk)   │   Daily rotation, compressed       │
│  └──────────┘                                    │
│                                                  │
│  Capacity: 100K events (hot cache)               │
│  Flush: 5-second write buffer cycle              │
│  Maintenance: 10-minute compact cycle            │
└─────────────────────────────────────────────────┘
```

### 4. Threat Intelligence Layer

| Engine | Purpose |
|--------|---------|
| Evidence Graph | Maps relationships between hosts, processes, files, and network indicators |
| Query Engine | Cross-tier search across hot cache and cold storage |
| Timeline Reconstructor | Rebuilds attack timelines with temporal ordering |
| Campaign Detector | Union-Find clustering to identify coordinated attacks |
| Threat Hunter | Retroactive hunting across all stored events |

### 5. ML Analysis Engine

Ensemble machine learning pipeline implemented in pure C# (no Python dependency):

```
Input Telemetry
       │
       ▼
┌──────────────┐
│ Feature Store │  ← Welford's online algorithm
│ (20-dim)     │  ← Z-score normalization
└──────┬───────┘
       │
  ┌────┼─────────────────┐
  │    │                  │
  ▼    ▼                  ▼
┌────┐ ┌──────────┐ ┌──────────┐
│ LR │ │ Gradient │ │ Graph ML │
│    │ │ Boosting │ │ (Belief  │
│    │ │ Trees    │ │  Prop.)  │
└─┬──┘ └────┬─────┘ └────┬────┘
  │         │             │
  └─────────┼─────────────┘
            │
    ┌───────▼────────┐
    │ Ensemble Blend │  ← Adaptive weight adjustment
    │ (Weighted Avg) │  ← 24-hour retraining cycle
    └───────┬────────┘
            │
            ▼
     Risk Score [0.0 - 1.0]
```

### 6. AI Investigation Layer

| Component | Description |
|-----------|-------------|
| LLM Inference | Local GGUF model inference via LLamaSharp for threat analysis |
| ASRE Pipeline | 5-stage autonomous research: Mine → Cluster → Hypothesize → Generate Rules → Report |
| Response Orchestrator | Coordinates automated containment and remediation actions |
| Adversarial Tester | Generates evasion techniques to validate detection pipeline |

### 7. Network Analysis

- **Deep Packet Inspection** — Protocol-level traffic analysis
- **DNS Exfiltration Detection** — Entropy analysis on DNS queries
- **TLS/SNI Analysis** — Certificate and server name inspection
- **Protocol Anomaly Detection** — Deviation from expected protocol behavior

### 8. Visualization & Response

- **Admin Dashboard** — Full management UI with all configuration tabs
- **3D Threat Universe** — Golden-ratio spiral layout with constellation detection
- **Push Notifications** — Firebase Cloud Messaging (FCM HTTP v1)
- **Automated Response** — Quarantine, firewall rule injection, process termination

---

## Data Flow

```
Endpoint Event
    │
    ▼
EventBus (10K capacity BlockingCollection)
    │
    ▼
DataLake Ingestor (normalize + enrich)
    │
    ├──→ EventStore (append JSONL + hot cache)
    ├──→ TelemetryRepository (secondary indexes)
    ├──→ NetworkFlowRepository (flow tracking)
    │
    ▼
Feature Extraction (20-dimensional vector)
    │
    ▼
ML Ensemble Prediction → Risk Score
    │
    ├── score < 0.3 → Log only
    ├── 0.3–0.7 → Alert + Investigation
    └── score > 0.7 → Alert + Auto-Response + Investigation
    │
    ▼
Evidence Graph (add nodes/edges)
    │
    ▼
Campaign Detection (Union-Find clustering)
    │
    ▼
Timeline Reconstruction
    │
    ▼
Report Generation + Push Notification
```

---

## API Architecture

The server exposes a RESTful API organized into endpoint groups:

| Group | Endpoints | Purpose |
|-------|-----------|---------|
| Authentication | 3 | Login, token refresh, registration |
| Telemetry | 4 | Event submission, batch upload, status |
| Threats | 5 | Threat queries, alerts, severity filtering |
| Data Lake | 12 | Hash/IP/host queries, timelines, campaigns, hunts |
| Threat Universe | 5 | 3D visualization snapshots, paths, constellations |
| Push Notifications | 4 | Device registration, send, status |
| DPI Network | 4 | Connection status, alerts, analysis |
| System | 7+ | Health, config, updates, diagnostics |

See [api/api-spec.yaml](api/api-spec.yaml) for the full OpenAPI specification.

---

## Security Architecture

| Layer | Mechanism |
|-------|-----------|
| Authentication | JWT (HMAC-SHA256, 24hr expiry) |
| CSRF Protection | Origin/Referer validation, Bearer token bypass |
| Rate Limiting | Per-IP connection tracking, sliding window |
| Data at Rest | Append-only storage, checksum verification |
| Transport | HTTPS/TLS 1.2+ |
| Client Auth | API key + JWT token |

See [SECURITY_MODEL.md](SECURITY_MODEL.md) for the complete security model.

---

## Deployment Architecture

```
┌─────────────────────────────────────────┐
│           Server Deployment             │
│                                         │
│  ┌─────────┐  ┌────────┐  ┌─────────┐  │
│  │ Kestrel │  │ Admin  │  │  LLM    │  │
│  │ API     │  │ Web UI │  │ Models  │  │
│  │ Server  │  │        │  │ (GGUF)  │  │
│  └─────────┘  └────────┘  └─────────┘  │
│                                         │
│  ┌──────────────────────────────────┐   │
│  │ Bundled Data & Services          │   │
│  │ ├── Threat Signatures            │   │
│  │ ├── ML Models                    │   │
│  │ ├── Knowledge Base               │   │
│  │ ├── Client Downloads (Win/Lin)   │   │
│  │ ├── Website (Static)             │   │
│  │ ├── TTS Engine                   │   │
│  │ └── SD WebUI                     │   │
│  └──────────────────────────────────┘   │
│                                         │
│  Installer: Inno Setup 6               │
│  Size: ~60GB (all data bundled)         │
└─────────────────────────────────────────┘
```

---

## Project Structure

```
SyntheticAI/
├── src/
│   ├── SGL.JudgeDredd.App/          # Windows WPF Desktop Client
│   ├── SGL.JudgeDredd.Server/       # API Server + Endpoints
│   ├── SGL.JudgeDredd.Security/     # Core Security Engines
│   ├── SGL.JudgeDredd.DataLake/     # Security Data Lake
│   ├── SGL.JudgeDredd.Core/         # Shared Core Libraries
│   ├── SGL.JudgeDredd.Shared/       # Cross-project Models
│   ├── SGL.JudgeDredd.Antivirus/    # AV Scanning Engine
│   ├── SGL.JudgeDredd.Firewall/     # Firewall Management
│   ├── SGL.JudgeDredd.LLM/         # LLM Integration Layer
│   ├── SGL.JudgeDredd.KnowledgeBase/# Threat Knowledge Base
│   ├── SGL.JudgeDredd.LinuxClient/  # Linux Console Client
│   ├── SGL.JudgeDredd.MobileApp/    # Android MAUI App
│   └── ...
├── tests/
│   └── SGL.JudgeDredd.Tests/        # xUnit Integration Tests
└── [build outputs]
```

15 .NET projects | 1,200+ source files | 133,000+ lines of C#

---

## Requirements

| Requirement | Server | Client |
|-------------|--------|--------|
| OS | Windows Server 2019+ / Ubuntu 20.04+ | Windows 10+ / Linux / Android 10+ |
| Runtime | .NET 8.0 (bundled) | .NET 8.0 (bundled) |
| RAM | 16GB+ recommended | 4GB minimum |
| Storage | 100GB+ (with LLMs) | 2GB |
| Network | Static IP recommended | Internet access |
| GPU | Optional (LLM acceleration) | Not required |

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
