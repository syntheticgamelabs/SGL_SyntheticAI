# Architecture — SGL SyntheticAI v1.1.46

This document provides a comprehensive overview of the SGL SyntheticAI system architecture, component interactions, and design philosophy.

---

## Table of Contents

- [Design Philosophy](#design-philosophy)
- [System Overview](#system-overview)
- [Solution Structure](#solution-structure)
- [Security Engine Layer](#security-engine-layer)
- [AI/ML Layer](#aiml-layer)
- [Network Layer](#network-layer)
- [Server & API Layer](#server--api-layer)
- [Data Layer](#data-layer)
- [Client Platforms](#client-platforms)
- [Threat Scoring Pipeline](#threat-scoring-pipeline)
- [Self-Evolving AI Architecture](#self-evolving-ai-architecture)
- [Gossip Protocol Design](#gossip-protocol-design)
- [Data Flow Diagrams](#data-flow-diagrams)

---

## Design Philosophy

SGL SyntheticAI is built on four core architectural principles:

1. **AI-Native Security** — LLM inference is a first-class citizen, not a bolt-on. Every threat assessment path can leverage local LLM analysis.

2. **Zero Cloud Dependency** — All security-critical functions operate entirely offline. No telemetry is sent externally. LLM inference runs on-device via llama.cpp.

3. **Defense in Depth** — Multiple independent detection engines (YARA, hash, heuristic, behavioral, graph, ML) provide overlapping coverage. No single engine failure compromises protection.

4. **Autonomous Evolution** — The system trains, validates, and deploys new detection models without human intervention, adapting to emerging threats in real-time.

---

## System Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                    SGL SyntheticAI v1.1.46                        │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                       │
│  │ WPF      │  │ Linux    │  │ Android  │                       │
│  │ Desktop  │  │ CLI      │  │ MAUI     │                       │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘                       │
│       └──────────────┼─────────────┘                              │
│                      ▼                                            │
│  ┌────────────────────────────────────────────────────────┐      │
│  │              SECURITY ENGINE LAYER                      │      │
│  │                                                         │      │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐  │      │
│  │  │  Antivirus  │  │ SecurityBrain │  │ Malware RE   │  │      │
│  │  │  YARA+Hash  │  │ Composite    │  │ PE Analysis  │  │      │
│  │  │  +Heuristic │  │ Scoring      │  │ +Classify    │  │      │
│  │  └─────────────┘  └──────────────┘  └──────────────┘  │      │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐  │      │
│  │  │ YARA Rule   │  │ Campaign     │  │ Evolution    │  │      │
│  │  │ Generator   │  │ Detection    │  │ Engine       │  │      │
│  │  │ (auto-gen)  │  │ (graph ML)   │  │ (IsoForest)  │  │      │
│  │  └─────────────┘  └──────────────┘  └──────────────┘  │      │
│  └────────────────────────────────────────────────────────┘      │
│                      ▼                                            │
│  ┌────────────────────────────────────────────────────────┐      │
│  │              LLM / AI LAYER                             │      │
│  │                                                         │      │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐  │      │
│  │  │ MultiLLM    │  │ Chat Service │  │ TTS Engine   │  │      │
│  │  │ 4-Slot Mgr  │  │ (LlamaSharp) │  │ (SAPI+Qwen) │  │      │
│  │  └─────────────┘  └──────────────┘  └──────────────┘  │      │
│  └────────────────────────────────────────────────────────┘      │
│                      ▼                                            │
│  ┌────────────────────────────────────────────────────────┐      │
│  │              NETWORK LAYER                              │      │
│  │                                                         │      │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐  │      │
│  │  │ Firewall    │  │ Gossip       │  │ Threat Intel │  │      │
│  │  │ (WinFW COM) │  │ Protocol     │  │ (abuse.ch)   │  │      │
│  │  └─────────────┘  └──────────────┘  └──────────────┘  │      │
│  └────────────────────────────────────────────────────────┘      │
│                      ▼                                            │
│  ┌────────────────────────────────────────────────────────┐      │
│  │              SERVER + API LAYER                         │      │
│  │  28 API endpoints | JWT auth | WebSocket                │      │
│  │  Website hosting | SD WebUI proxy | Client sync         │      │
│  └────────────────────────────────────────────────────────┘      │
│                      ▼                                            │
│  ┌────────────────────────────────────────────────────────┐      │
│  │              DATA LAYER                                 │      │
│  │  SQLite (KnowledgeBase) | JSON configs | YARA rules     │      │
│  │  campaigns.json | model_registry.json | threat_sigs     │      │
│  │  EventBus | NotificationService                         │      │
│  └────────────────────────────────────────────────────────┘      │
└──────────────────────────────────────────────────────────────────┘
```

---

## Solution Structure

The solution contains **14 projects** organized by responsibility:

| Project | Type | Purpose |
|---------|------|---------|
| `SGL.JudgeDredd.App` | WPF Application | Windows desktop client |
| `SGL.JudgeDredd.ServerHost` | Console App | Standalone server host |
| `SGL.JudgeDredd.Server` | Class Library | API server, endpoints, middleware |
| `SGL.JudgeDredd.Core` | Class Library | Core services, scanning engine, event bus |
| `SGL.JudgeDredd.Security` | Class Library | SecurityBrain, ML engines, threat analysis |
| `SGL.JudgeDredd.LLM` | Class Library | LLamaSharp integration, multi-model management |
| `SGL.JudgeDredd.Shared` | Class Library | Shared types, version info, configuration |
| `SGL.JudgeDredd.Api.Contracts` | Class Library | API DTOs, request/response models |
| `SGL.JudgeDredd.Mobile` | MAUI App | Android client |
| `SGL.JudgeDredd.LinuxClient` | Console App | Linux CLI client |
| `SGL.JudgeDredd.LinuxServer` | Console App | Linux server host |
| `SGL.JudgeDredd.Installer` | Misc | Inno Setup installer scripts |
| `SGL.JudgeDredd.Website` | Static | React SPA website |
| `SGL.JudgeDredd.Tools` | Console App | Utility tools and scripts |

**Total: 1,044+ source files | 125,000+ lines of code**

---

## Security Engine Layer

### Multi-Engine Scanner

The antivirus scanner runs three independent detection engines in parallel:

```
File Input
    │
    ├──▶ YARA Engine ─────▶ Pattern match against .yar rule files
    │                       (dnYara NuGet binding)
    │
    ├──▶ Hash Engine ──────▶ SHA-256 hash lookup in signature DB
    │                       (SQLite + in-memory bloom filter)
    │
    └──▶ Heuristic Engine ─▶ 14-point behavioral analysis
                             (entropy, imports, strings, packers)
         │
         ▼
    Composite Verdict (weighted scoring)
```

### Heuristic Analysis Points

| # | Check | Weight |
|---|-------|--------|
| 1 | PE file entropy (>7.0 suspicious) | High |
| 2 | Suspicious API imports (VirtualAlloc, CreateRemoteThread, etc.) | High |
| 3 | Suspicious string patterns | Medium |
| 4 | Section name anomalies (.UPX, .themida) | Medium |
| 5 | Packer detection (30+ signatures) | High |
| 6 | Entry point anomalies | Medium |
| 7 | Import table size ratio | Low |
| 8 | Resource section anomalies | Low |
| 9 | Digital signature validation | Medium |
| 10 | File size vs section size mismatch | Low |
| 11 | Overlay data detection | Low |
| 12 | TLS callback presence | Medium |
| 13 | Debug directory anomalies | Low |
| 14 | Compiler/linker artifacts | Low |

### Malware Reverse-Engineering Engine

Autonomous PE binary analysis pipeline:

```
Suspicious File
    │
    ▼
┌─────────────────┐
│  PE Reader       │  System.Reflection.PortableExecutable
│  ├─ Headers      │  DOS, COFF, PE Optional headers
│  ├─ Sections     │  Per-section entropy calculation
│  ├─ Import Table │  Manual ILT/IAT parsing
│  ├─ Strings      │  Embedded string extraction
│  └─ Packers      │  30+ packer signature matching
└────────┬────────┘
         ▼
┌─────────────────┐
│  Attack Graph    │  Directed graph of malicious behaviors
│  Builder         │  Nodes: capabilities, techniques
│                  │  Edges: call/data flow relationships
└────────┬────────┘
         ▼
┌─────────────────┐
│  Classifier      │  Weighted heuristic scoring
│  ├─ Entropy      │  0.25 weight
│  ├─ Imports      │  0.25 weight
│  ├─ Packers      │  0.20 weight
│  ├─ Strings      │  0.15 weight
│  └─ Attack Graph │  0.15 weight
└────────┬────────┘
         ▼
    Classification: Trojan | Ransomware | Worm | Spyware | etc.
    Confidence: 0.0 - 1.0
```

### Self-Mutating YARA Rule Generator

```
New Malware Sample
    │
    ▼
┌─────────────────┐
│ Feature Extract  │  String scoring (length, uniqueness, entropy)
│                  │  Benign string exclusion
└────────┬────────┘
         ▼
┌─────────────────┐
│ Rule Synthesis   │  Valid YARA syntax generation
│ ├─ Meta tags     │  Author, date, description, hash
│ ├─ PE magic      │  MZ header check
│ ├─ Conditions    │  Threshold-based (N of M strings)
│ └─ Strings       │  Discriminating byte patterns
└────────┬────────┘
         ▼
┌─────────────────┐
│ Validation       │  Test against clean file corpus
│                  │  Reject if FP rate > 1%
└────────┬────────┘
         ▼
┌─────────────────┐
│ Evolution        │  Add patterns from new samples
│                  │  Tighten conditions, increment version
└────────┬────────┘
         ▼
    data/yara_rules/auto_*.yar
```

---

## AI/ML Layer

### Multi-LLM Slot Architecture

```
┌─────────────────────────────────────────┐
│          MultiLlmManager                 │
│                                          │
│  Slot 0: MainEngine    [Qwen-7B]  ●     │
│  Slot 1: AiChat        [Llama-3]  ●     │
│  Slot 2: SecurityAI    [Mistral]  ○     │
│  Slot 3: Available     [empty]    ○     │
│                                          │
│  ● = Mounted    ○ = Empty               │
│  Thread-safe via SemaphoreSlim           │
└──────────────┬──────────────────────────┘
               │
               ▼
┌──────────────────────────────┐
│  LlmService Bridge           │
│  ├─ Primary: LlmModelManager │
│  └─ Fallback: MultiLlmManager│
│     .GetMainEngine()         │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│  ChatSessionManager          │
│  Per-user session context    │
│  Token streaming via         │
│  IAsyncEnumerable<string>    │
└──────────────────────────────┘
```

### SecurityBrain Threat Scoring

```
Threat Event
    │
    ├──▶ Behavioral Score  (0.30 weight) ──▶ Process tree analysis,
    │                                         syscall patterns
    │
    ├──▶ Graph Score       (0.40 weight) ──▶ Threat graph connectivity,
    │                                         relationship analysis
    │
    ├──▶ LLM Confidence    (0.20 weight) ──▶ Natural language threat
    │                                         assessment via local LLM
    │
    └──▶ Threat Intel      (0.10 weight) ──▶ IOC matching against
                                              abuse.ch feeds
         │
         ▼
    Composite Score = Σ(weight × score)
    Range: 0.0 (benign) → 1.0 (critical threat)
```

---

## Self-Evolving AI Architecture

The Evolution Engine implements a fully autonomous model training, validation, and deployment pipeline:

```
Telemetry Events (via EventBus)
    │
    ▼
┌─────────────────────┐
│ Feature Extractor    │  7-element vectors:
│ ├─ Process hash      │  [0] binary hash → enum mapping
│ ├─ Tree depth        │  [1] process tree depth / 10
│ ├─ Port risk         │  [2] known risky port score
│ ├─ Cmd entropy       │  [3] Shannon entropy of command line
│ ├─ Network risk      │  [4] geo/blacklist risk score
│ ├─ Time of day       │  [5] hour / 24.0 (cyclical)
│ └─ File entropy      │  [6] associated file entropy / 8.0
└────────┬────────────┘
         │
         ▼ (accumulates events for 6 hours)
┌─────────────────────┐
│ Isolation Forest     │  Pure C# Implementation
│ ├─ 100 trees         │  Each tree: random feature splits
│ ├─ 256 subsamples    │  Per-tree random subsample
│ └─ Anomaly scoring   │  Score = 2^(-avgPath / c(n))
│                      │  Shorter path = more anomalous
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Validation           │  Test against known-good events
│                      │  REJECT if FP rate > 5%
└────────┬────────────┘
         │ (pass)
         ▼
┌─────────────────────┐
│ Model Registry       │  data/models/anomaly_model_vN.json
│ ├─ Version tracking  │  Semantic versioning
│ ├─ Performance stats │  FP rate, detection rate, sample size
│ └─ Rollback support  │  Previous models retained
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ Strategy Generator   │  Auto-generate detection rules
│                      │  from anomaly clusters
│                      │  Human-readable rule descriptions
└─────────────────────┘
```

### Isolation Forest Algorithm Detail

```
BuildTree(data, depth):
    if |data| ≤ 1 or depth ≥ max_depth:
        return LeafNode(size=|data|)

    feature = random(0..6)
    min_val, max_val = range(data[feature])
    split = uniform(min_val, max_val)

    left  = { x ∈ data : x[feature] < split }
    right = { x ∈ data : x[feature] ≥ split }

    return InternalNode(
        feature, split,
        left=BuildTree(left, depth+1),
        right=BuildTree(right, depth+1)
    )

Score(x):
    avgPath = mean(PathLength(x, tree) for tree in forest)
    c_n = 2 * (ln(n-1) + γ) - (2*(n-1)/n)
    return 2^(-avgPath / c_n)
    // Score > 0.5 = anomalous
    // Score < 0.5 = normal
```

---

## Gossip Protocol Design

Peer-to-peer threat intelligence sharing across swarm nodes:

```
Node A                    Node B                    Node C
  │                         │                         │
  │──UDP Broadcast──────▶  │                         │
  │  (fan-out=3)           │──UDP Forward──────────▶│
  │                         │  (fan-out=3)           │
  │                         │                         │
  │◄──Heartbeat (30s)─────│◄──Heartbeat (30s)──────│
  │                         │                         │
  │  Message Dedup:         │                         │
  │  GUID-based seen cache  │                         │
  │  30-min TTL             │                         │
  │                         │                         │
  │  Peer Registry:         │                         │
  │  Active peers tracked   │                         │
  │  10-min timeout         │                         │
```

### Reputation System

```
Initial reputation: 0.5

Events:
  Confirmed threat report:  +0.05
  False positive report:    -0.10
  Consistent with consensus: +0.02

Weighted Voting:
  vote_weight = peer.reputation
  consensus = Σ(vote × weight) / Σ(weight)
  Threshold: consensus > 0.6 → accepted as threat
```

---

## Campaign Detection

Global attack pattern recognition via graph clustering:

```
Telemetry Events
    │
    ▼
┌─────────────────────────────┐
│ Global Threat Graph          │
│ ConcurrentDictionary-backed  │
│                              │
│  endpoint ──▶ process ──▶ file ──▶ domain ──▶ IP
│     │            │          │         │        │
│     └────────────┴──────────┴─────────┴────────┘
│                 Bidirectional edges
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│ Event Correlation            │
│ ├─ Group by shared IPs      │
│ ├─ Group by shared hashes   │
│ ├─ Group by shared domains  │
│ └─ 4-hour time window       │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│ Connected-Component BFS      │
│ Clusters with >2 endpoints   │
│ = Attack Campaign            │
└────────┬────────────────────┘
         │
         ▼
Campaign Score:
  0.3 × endpoint_count +
  0.3 × unique_hashes +
  0.2 × unique_IPs +
  0.2 × time_correlation
```

---

## Data Flow Diagrams

### Scan Flow

```
User initiates scan
    │
    ▼
ScanViewModel.StartScanAsync()
    │
    ├──▶ FileSystemWatcher (real-time)
    │    or manual file/directory selection
    │
    ▼
AntivirusEngine.ScanFileAsync(path)
    │
    ├──▶ YARA: dnYara.CompiledRules.Match(bytes)
    ├──▶ Hash: SHA256 → KnowledgeBase.LookupHash()
    ├──▶ Heuristic: HeuristicEngine.Analyze(path)
    │
    ▼
SecurityBrain.AssessAsync(scanResult)
    │
    ├──▶ Behavioral analysis
    ├──▶ Graph correlation
    ├──▶ LLM assessment (optional)
    ├──▶ Threat intel matching
    │
    ▼
Verdict: Clean | Suspicious | Malicious
    │
    ├── Clean: Log result
    ├── Suspicious: Alert + detailed report
    └── Malicious: Quarantine + alert + EventBus publish
```

### LLM Chat Flow

```
User message → ChatViewModel
    │
    ▼
AiCommandRouter.ExecuteAsync()
    │
    ├── /scan command  → trigger scan
    ├── /status        → system status
    ├── /help          → help text
    └── chat           → LLM inference
         │
         ▼
    LlmService.ChatAsync()
         │
         ├── Primary: LlmModelManager.IsLoaded?
         │   └── Yes → ChatSessionManager → inference
         │
         └── Fallback: MultiLlmManager.GetMainEngine()
             └── ChatSessionManager → inference
                  │
                  ▼
             Token streaming → UI update
             IAsyncEnumerable<string>
```

---

## Data Storage Map

| Data | Location | Format | Access Pattern |
|------|----------|--------|----------------|
| Threat signatures | SQLite + `data/threat_signatures.json` | DB + JSON backup | Read-heavy, periodic write |
| Chat memory | `data/llm_memory_{username}.json` | Per-user JSON | Per-session read/write |
| Attack campaigns | `data/campaigns.json` | JSON | Periodic write, read on query |
| Anomaly models | `data/models/anomaly_model_v{N}.json` | Serialized forest | 6-hour write, constant read |
| Model registry | `data/models/model_registry.json` | Version tracking | Append-only |
| YARA rules | `data/yara_rules/*.yar` | YARA syntax | Read on scan, periodic write |
| Scan history | SQLite KnowledgeBase | Database | Append + query |
| Settings | `data/settings.json` | JSON | Read on startup, write on change |
| Server config | `data/server_settings.json` | JSON | Read on startup |
| User accounts | SQLite + `data/users.json` | DB + JSON | Auth read, admin write |

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
