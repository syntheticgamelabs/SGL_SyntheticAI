# Technology Deep Dive — SGL SyntheticAI

**Confidential — For Technical Due Diligence**

---

## Technology Stack Overview

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Core Language | C# 13 / .NET 9.0 | Cross-platform, high performance, strong typing, vast ecosystem |
| Desktop UI | WPF | Native Windows rendering, MVVM, rich data binding |
| Mobile UI | .NET MAUI | Single codebase for Android (iOS planned) |
| Web API | ASP.NET Core Minimal API | High throughput, low ceremony, middleware pipeline |
| LLM Runtime | LLamaSharp (llama.cpp) | Industry-standard quantized model inference, CPU + GPU |
| Database | SQLite + EF Core | Zero-config embedded database, LINQ queries |
| Malware Scanning | dnYara | .NET YARA binding for pattern matching |
| Email | MailKit | Full IMAP/SMTP/MIME support |
| TTS | Windows SAPI + Qwen3-TTS | Offline voice synthesis |
| Image Gen | Stable Diffusion WebUI | Research-grade image generation via API |

---

## AI/ML Architecture

### 1. SecurityBrain — Composite Threat Scoring

Our proprietary threat assessment pipeline combines 4 independent scoring engines:

```
Score = 0.3 × Behavioral + 0.4 × Graph + 0.2 × LLM + 0.1 × ThreatIntel
```

**Why 4 engines?** Defense in depth. Each engine has different failure modes:
- **Behavioral** catches process-level anomalies (process tree, syscalls)
- **Graph** catches relational patterns (file→process→network→domain)
- **LLM** provides natural language reasoning about novel threats
- **ThreatIntel** catches known IOCs from external feeds

No single engine's failure compromises the overall assessment.

### 2. Isolation Forest — Self-Evolving Anomaly Detection

**Pure C# implementation** — no external ML framework dependency.

```
Architecture:
  - 100 isolation trees
  - 256-sample subsets per tree
  - 7-element feature vectors
  - 6-hour retraining cycle
  - <5% false-positive validation gate

Anomaly Score:
  s(x) = 2^(-E[h(x)] / c(n))
  where h(x) = path length to isolate x
  and c(n) = expected path length for n samples

Interpretation:
  s → 1.0: anomaly (short isolation path)
  s → 0.5: normal (average isolation path)
  s → 0.0: very normal (long isolation path)
```

**Key innovation:** The training and deployment pipeline is fully autonomous:
1. Events accumulate for 6 hours
2. New Isolation Forest trained on 24-hour window
3. Validated against known-good baseline (reject >5% FP)
4. Promoted to active model registry
5. Previous model retained for rollback
6. Detection strategies auto-generated from anomaly clusters

### 3. Malware Reverse-Engineering Engine

Autonomous PE binary analysis using `System.Reflection.PortableExecutable` (built-in .NET):

| Analysis Stage | Method | Output |
|---------------|--------|--------|
| PE Header Parsing | PEReader API | DOS/COFF/Optional headers |
| Section Analysis | Per-section entropy | High entropy = packed/encrypted |
| Import Table | Manual ILT/IAT parsing | Suspicious API imports |
| String Extraction | Binary string scanning | Embedded URLs, commands |
| Packer Detection | 30+ signature matching | UPX, Themida, VMProtect, etc. |
| Attack Graph | Directed graph construction | Capability→technique mapping |
| Classification | Weighted heuristic scoring | Trojan/Ransomware/Worm/Spyware |

**Weighted Classification:**
```
0.25 × entropy_score +
0.25 × import_score +
0.20 × packer_score +
0.15 × string_score +
0.15 × attack_graph_score
```

### 4. YARA Rule Auto-Generation

**Innovation:** Automatically synthesizes detection rules from malware samples.

```
Pipeline:
  1. Extract discriminating strings (scored by length, uniqueness, entropy)
  2. Exclude known benign strings
  3. Generate valid YARA syntax with meta tags and PE magic check
  4. Test against clean file corpus
  5. REJECT if false-positive rate > 1%
  6. Deploy to data/yara_rules/auto_*.yar
  7. EVOLVE: Add patterns from new samples, tighten conditions
```

### 5. Campaign Detection

Global threat pattern recognition via connected-component graph clustering:

```
Input: Telemetry events from multiple endpoints

Graph Construction:
  Nodes: endpoints, processes, files, domains, IPs
  Edges: observed relationships (concurrent dict, thread-safe)

Correlation:
  Group events by: shared IPs, shared hashes, shared domains
  Time window: 4 hours

Clustering:
  Algorithm: BFS connected components
  Campaign threshold: >2 endpoints involved

Scoring:
  0.3 × endpoint_count +
  0.3 × unique_hashes +
  0.2 × unique_IPs +
  0.2 × time_correlation
```

### 6. Gossip Protocol

Decentralized threat intelligence sharing:

```
Transport: UDP (low overhead)
Fan-out: 3 random peers per broadcast
Dedup: GUID-based seen cache, 30-min TTL
Heartbeat: 30-second interval, TTL=2

Reputation System:
  Initial: 0.5
  Confirmed threat: +0.05
  False positive: -0.10
  Consistent: +0.02

Consensus:
  weight = peer.reputation
  consensus = Σ(vote × weight) / Σ(weight)
  Accept if consensus > 0.6
```

---

## Performance Characteristics

### LLM Inference

| Configuration | Performance |
|--------------|-------------|
| CPU-only (8 cores) | 5-30 tokens/sec |
| GPU offload (RTX 3060) | 30-60 tokens/sec |
| GPU offload (RTX 4090) | 60-100+ tokens/sec |

### Scan Performance

| Engine | Time per File | Memory |
|--------|--------------|--------|
| SHA-256 Hash | <1ms | ~50 MB (bloom filter) |
| YARA Rules | 5-50ms | ~200 MB (compiled rules) |
| Heuristic | 10-100ms | ~10 MB |
| Full SecurityBrain | 50-500ms | Depends on LLM |

---

## Code Quality Metrics

| Metric | Value |
|--------|-------|
| Build status | 0 Errors, 0 Warnings |
| Solution projects | 14 |
| Source files | 1,044+ |
| Estimated LOC | 125,000+ |
| Architecture pattern | MVVM + DI + Event-driven |
| Thread safety | ConcurrentDictionary, SemaphoreSlim, async/await |
| Logging | Custom SglLogger + structured logging |

---

## Intellectual Property Summary

| IP Asset | Type | Status |
|----------|------|--------|
| SecurityBrain composite scoring | Trade secret | Protected |
| Isolation Forest C# implementation | Trade secret | Protected |
| Malware RE engine | Trade secret | Protected |
| YARA rule auto-generation | Trade secret | Protected |
| Campaign detection algorithm | Trade secret | Protected |
| Gossip protocol with reputation | Trade secret | Protected |
| Multi-LLM slot orchestration | Trade secret | Protected |
| SGL SyntheticAI trademark | Trademark | Claimed |

---

*This document contains confidential and proprietary information. Distribution without written consent is prohibited.*
