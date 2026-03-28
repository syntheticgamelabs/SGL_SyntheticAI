<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Android-blue" alt="Platforms" />
  <img src="https://img.shields.io/badge/Version-1.1.49%20Beta-green" alt="Version" />
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET" />
  <img src="https://img.shields.io/badge/License-Proprietary-red" alt="License" />
  <img src="https://img.shields.io/badge/Status-Active%20Development-orange" alt="Status" />
</p>

# SyntheticAI — Autonomous AI Cyber Defense Platform

**SyntheticAI** is an experimental research platform exploring autonomous cyber defense systems powered by distributed telemetry analysis, threat graph intelligence, and AI-driven investigation engines.

Built by [Synthetic Game Labs](https://syntheticgamelabs.dpdns.org), the platform represents a new approach to endpoint security — where AI agents autonomously detect, investigate, and respond to threats across distributed environments without human intervention.

---

## The Problem

Traditional security tools are reactive. They rely on static signatures, manual investigation, and human-speed response times. Modern adversaries use AI-assisted attacks, living-off-the-land techniques, and multi-stage campaigns that evade conventional defenses.

**The gap is growing between attack speed and defense speed.**

SyntheticAI closes that gap.

---

## Core Concepts

| Concept | Description |
|---------|-------------|
| **Distributed Telemetry Network** | Real-time endpoint telemetry collection across heterogeneous environments |
| **Security Data Lake** | Hot/warm/cold tiered event storage with sub-second query capability |
| **Threat Graph Intelligence** | Evidence-based graph modeling of attack relationships and kill chains |
| **Autonomous Investigation AI** | LLM-powered threat analysis with evidence chain reasoning |
| **Temporal Attack Path Analysis** | Time-aware traversal of attack graphs with decay-weighted risk scoring |
| **Cross-Endpoint Campaign Detection** | Behavioral clustering to identify coordinated multi-host attacks |
| **ML Threat Classification** | Ensemble learning combining gradient boosting, graph ML, and logistic regression |
| **Self-Evolving Detection Pipeline** | Autonomous model retraining and detection rule generation |
| **Deep Packet Inspection** | Protocol-level network analysis with DNS exfiltration detection |
| **3D Threat Visualization** | Real-time spatial mapping of threat landscapes |

---

## System Architecture

```
                    ┌─────────────────────────────────────┐
                    │        SyntheticAI Platform          │
                    └─────────────────────────────────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────┐
          │                         │                         │
   ┌──────▼──────┐          ┌──────▼──────┐          ┌──────▼──────┐
   │  Endpoint    │          │   Server    │          │   Mobile    │
   │  Agents      │          │   Platform  │          │   Agents    │
   │ (Win/Linux)  │          │  (Kestrel)  │          │  (Android)  │
   └──────┬──────┘          └──────┬──────┘          └──────┬──────┘
          │                         │                         │
          └─────────────────────────┼─────────────────────────┘
                                    │
                    ┌───────────────▼───────────────┐
                    │     Telemetry Pipeline         │
                    │  ┌─────────┐  ┌────────────┐  │
                    │  │ EventBus│  │ Normalizer  │  │
                    │  └────┬────┘  └─────┬──────┘  │
                    └───────┼─────────────┼─────────┘
                            │             │
                    ┌───────▼─────────────▼─────────┐
                    │     Security Data Lake          │
                    │  ┌──────┐ ┌──────┐ ┌────────┐  │
                    │  │ Hot  │ │ Warm │ │  Cold  │  │
                    │  │Cache │ │Index │ │Storage │  │
                    │  └──┬───┘ └──┬───┘ └───┬────┘  │
                    └─────┼────────┼─────────┼───────┘
                          │        │         │
                    ┌─────▼────────▼─────────▼───────┐
                    │     Threat Intelligence          │
                    │  ┌──────────────────────────┐   │
                    │  │   Evidence Graph Engine   │   │
                    │  │   Threat Query Engine     │   │
                    │  │   Timeline Reconstructor  │   │
                    │  │   Campaign Detector       │   │
                    │  └──────────────────────────┘   │
                    └──────────────┬──────────────────┘
                                   │
                    ┌──────────────▼──────────────────┐
                    │       AI Analysis Layer          │
                    │  ┌──────────────────────────┐   │
                    │  │  ML Ensemble Engine       │   │
                    │  │  Graph Risk Propagation   │   │
                    │  │  LLM Investigation AI     │   │
                    │  │  Autonomous Research (ASRE)│  │
                    │  └──────────────────────────┘   │
                    └──────────────┬──────────────────┘
                                   │
                    ┌──────────────▼──────────────────┐
                    │     Response & Visualization     │
                    │  ┌──────────────────────────┐   │
                    │  │  Automated Response       │   │
                    │  │  3D Threat Universe        │   │
                    │  │  Admin Dashboard           │   │
                    │  │  Push Notifications        │   │
                    │  └──────────────────────────┘   │
                    └─────────────────────────────────┘
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed component documentation.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Desktop Client | C# / .NET 8 / WPF |
| Server Platform | ASP.NET Core / Kestrel Minimal API |
| Mobile Client | .NET MAUI (Android) |
| Local AI Inference | LLamaSharp / GGUF Models |
| ML Pipeline | Pure C# (no Python dependency) |
| Network Analysis | Raw socket inspection / DNS analysis |
| Data Storage | Append-only JSONL with in-memory indexes |
| Real-time Comms | WebSocket / Server-Sent Events |
| Installer | Inno Setup 6 (Windows) / tar.gz (Linux) |

---

## Research Modules

This repository contains documentation and safe demonstration code for the following research areas:

- **Threat Graph Engine** — Graph-based modeling of attack relationships ([docs](docs/threat-graph-engine.md))
- **Telemetry Pipeline** — Distributed event collection and normalization ([docs](docs/telemetry-pipeline.md))
- **AI SOC Automation** — Autonomous security operations concepts ([docs](docs/ai-soc-automation.md))
- **Campaign Detection** — Cross-endpoint behavioral clustering ([docs](docs/campaign-detection.md))
- **Deep Packet Analysis** — Network-level threat detection concepts ([docs](docs/deep-packet-analysis.md))

---

## Demo Modules

The `/examples` and `/demo` directories contain simplified, safe demonstration code showing system concepts:

- **Synthetic Telemetry Generator** — Generates realistic endpoint telemetry for testing
- **Demo Threat Graph** — Builds and visualizes threat relationship graphs
- **Sample Detection Pipeline** — Simplified rule-based detection (not production algorithms)
- **Threat Visualization Dashboard** — Interactive D3.js threat landscape viewer

```bash
# Run the demo API
cd demo
dotnet run

# Generate sample telemetry
curl http://localhost:5100/demo/events

# View threat graph
curl http://localhost:5100/demo/threats

# Open dashboard
open dashboard/threat-visualization.html
```

---

## SDK & Integration

The `/sdk` directory contains schemas and integration examples:

- **Telemetry Schema** — Event format specification for agent integration
- **API Client** — Reference client for the SyntheticAI server API
- **Sample Integrations** — SIEM forwarding, webhook, and Syslog examples

---

## Platform Support

| Platform | Type | Status |
|----------|------|--------|
| Windows 10/11/Server | Full Client + Server | Production |
| Linux (Ubuntu/Debian/RHEL) | Client + Server | Production |
| Android 10+ | Mobile Agent | Production |
| macOS | Planned | Roadmap |
| iOS | Planned | Roadmap |

---

## Project Scale

| Metric | Value |
|--------|-------|
| C# Source Files | 1,200+ |
| Lines of Code | 133,000+ |
| .NET Projects | 15 |
| API Endpoints | 44+ |
| Security Engines | 12 |
| ML Models | 3 (ensemble) |

---

## Important Notice

> This repository contains documentation, architecture, research concepts, and safe demonstration modules related to the SyntheticAI platform.
>
> **Core engine implementations, detection algorithms, ML training pipelines, threat classification models, and proprietary infrastructure are NOT included in this public repository.**
>
> The production platform is maintained in private repositories.

---

## Status

Private prototype under active development. Currently in **Beta v1.1.49**.

Parts of the system are not open-source. This repository serves as a technical portfolio and research showcase.

---

## Contact

For research collaboration, partnership inquiries, or investor information:

- **Email:** syntheticgamelabs@gmail.com
- **Website:** [syntheticgamelabs.dpdns.org](https://syntheticgamelabs.dpdns.org)
- **Organization:** Synthetic Game Labs

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs. All rights reserved.</sub><br/>
  <sub>SyntheticAI, Judge Dredd AI, and related architectures are proprietary technologies of Synthetic Game Labs.</sub>
</p>
