<p align="center">
  <img src="screenshots/logo.png" alt="SGL SyntheticAI" width="200"/>
</p>

<h1 align="center">SGL SyntheticAI</h1>
<p align="center">
  <strong>Autonomous AI-Powered Cybersecurity Platform</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.1.46-blue" alt="Version"/>
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build"/>
  <img src="https://img.shields.io/badge/platforms-5-orange" alt="Platforms"/>
  <img src="https://img.shields.io/badge/license-proprietary-red" alt="License"/>
  <img src="https://img.shields.io/badge/.NET-9.0-purple" alt=".NET"/>
  <img src="https://img.shields.io/badge/C%23-13-blueviolet" alt="C#"/>
</p>

<p align="center">
  <a href="#features">Features</a> |
  <a href="#architecture">Architecture</a> |
  <a href="#platforms">Platforms</a> |
  <a href="#installation">Installation</a> |
  <a href="ARCHITECTURE.md">Deep Dive</a> |
  <a href="ROADMAP.md">Roadmap</a> |
  <a href="SECURITY.md">Security</a>
</p>

---

## What is SGL SyntheticAI?

SGL SyntheticAI is an **enterprise-grade, AI-native cybersecurity platform** that combines traditional antivirus capabilities with autonomous AI threat detection, self-evolving defense models, and distributed swarm intelligence.

Unlike conventional antivirus software that relies solely on signature databases, SyntheticAI deploys **multiple local LLM models** running entirely on-device for real-time threat analysis, behavioral scoring, and autonomous incident response — with **zero cloud dependency** for core security functions.

### The Problem We Solve

| Traditional Antivirus | SGL SyntheticAI |
|----------------------|-----------------|
| Static signature matching | AI behavioral analysis + signatures |
| Cloud-dependent scanning | 100% local LLM inference |
| Manual rule updates | Self-mutating YARA rules |
| Reactive threat response | Proactive campaign detection |
| Single-device protection | Swarm intelligence across fleet |
| Human analyst required | Autonomous malware reverse-engineering |

---

## Features

### Core Security Engine
- **Multi-Engine Scanning** — YARA rules + SHA-256 hash matching + 14-point heuristic analysis
- **Real-Time File Monitor** — Instant scan on file create/change/rename via OS-level hooks
- **Quarantine Vault** — Encrypted isolation with restore capability
- **File Integrity Monitoring** — SHA-256 baseline snapshots with change alerting
- **Email Phishing Scanner** — IMAP integration via MailKit with URL and attachment analysis
- **Dark Web Monitor** — HIBP k-anonymity API for breach detection

### AI/ML Threat Intelligence
- **SecurityBrain** — Composite threat scoring pipeline: `0.3*Behavioral + 0.4*Graph + 0.2*LlmConfidence + 0.1*ThreatIntel`
- **Multi-LLM Engine** — 4 concurrent model slots via LLamaSharp (llama.cpp backend)
- **Autonomous Malware Reverse-Engineering** — PE analysis, import table parsing, packer detection (30+ signatures), attack graph construction, ML classification
- **Self-Mutating YARA Rule Generator** — Automated feature extraction, rule synthesis, false-positive validation (<1% FP threshold), and rule evolution
- **Self-Evolving AI Defense** — Isolation Forest anomaly detection, 6-hour retraining cycles, automated model validation and deployment
- **Global Campaign Detection** — Connected-component graph clustering across endpoints with temporal correlation

### Network Security
- **Windows Firewall Integration** — COM interop with `INetFwPolicy2`, rule management, import/export
- **Network Intrusion Detection** — Connection monitoring, suspicious port flagging, IP geolocation
- **Gossip Protocol** — UDP peer-to-peer threat intelligence sharing with reputation-weighted voting
- **Threat Intelligence Feeds** — abuse.ch URLhaus, Feodo Tracker, MalwareBazaar with 6-hour refresh

### Platform Features
- **Text-to-Speech** — Dual backend: Qwen3-TTS + Windows SAPI
- **AI Image Generation** — Stable Diffusion WebUI integration (API mode)
- **28 REST API Endpoints** — ASP.NET Core minimal API with JWT authentication
- **WebSocket Gateway** — Real-time bidirectional communication
- **Admin Panel** — Server metrics, hardware monitoring, user management, tunnel management
- **Localization** — 700+ strings across 6 languages

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    SGL SyntheticAI v1.1.46                    │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│   CLIENTS           SECURITY ENGINE        AI/ML LAYER       │
│  ┌─────────┐       ┌──────────────┐      ┌──────────────┐   │
│  │ Windows  │──────▶│ YARA+Hash    │      │ Multi-LLM    │   │
│  │ Desktop  │       │ +Heuristic   │◀────▶│ 4-Slot Mgr   │   │
│  ├─────────┤       ├──────────────┤      ├──────────────┤   │
│  │ Linux   │──────▶│ SecurityBrain │◀────▶│ Evolution    │   │
│  │ CLI     │       │ Threat Graph │      │ Engine       │   │
│  ├─────────┤       ├──────────────┤      ├──────────────┤   │
│  │ Android │──────▶│ Campaign     │◀────▶│ Malware RE   │   │
│  │ MAUI    │       │ Detection    │      │ PE Analysis  │   │
│  └─────────┘       └──────────────┘      └──────────────┘   │
│                           │                                    │
│                    ┌──────▼───────┐                            │
│                    │ ASP.NET Core │                            │
│                    │ 28 Endpoints │                            │
│                    │ JWT + WSS    │                            │
│                    └──────────────┘                            │
│                           │                                    │
│              ┌────────────┼────────────┐                      │
│              ▼            ▼            ▼                       │
│         ┌────────┐  ┌─────────┐  ┌─────────┐                │
│         │ SQLite │  │  JSON   │  │  YARA   │                │
│         │   DB   │  │ Configs │  │  Rules  │                │
│         └────────┘  └─────────┘  └─────────┘                │
└──────────────────────────────────────────────────────────────┘
```

For detailed architecture documentation, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Platforms

| Platform | Technology | Status |
|----------|-----------|--------|
| **Windows Desktop** | WPF + .NET 9 | Production |
| **Windows Server** | ASP.NET Core + Self-Contained | Production |
| **Linux Client** | CLI + .NET 9 | Production |
| **Linux Server** | ASP.NET Core + Self-Contained | Production |
| **Android** | .NET MAUI + Android 14 | Production |

---

## Installation

### Windows Client
Download and run `SyntheticAI_ClientSetup_v1.1.46.exe` from the [binaries/windows-client](binaries/windows-client/) folder.

### Windows Server
Download and run `SyntheticAI_ServerSetup_v1.1.46.exe` from the [binaries/windows-server](binaries/windows-server/) folder. Includes bundled LLM models, SD WebUI, and Python runtime.

### Linux
```bash
chmod +x install.sh
sudo ./install.sh
```

### Android
Install `SyntheticAI_v3.6.0.apk` on any Android 8.0+ device.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Language** | C# 13 / .NET 9.0 |
| **Desktop UI** | WPF (Windows Presentation Foundation) |
| **Mobile UI** | .NET MAUI (Android) |
| **Web API** | ASP.NET Core Minimal API |
| **LLM Runtime** | LLamaSharp (llama.cpp wrapper) |
| **Malware Scanning** | dnYara (.NET YARA binding) |
| **Email** | MailKit (IMAP/SMTP) |
| **Database** | SQLite via Entity Framework Core |
| **Auth** | JWT (HMAC-SHA256) + bcrypt |
| **TTS** | Windows SAPI + Qwen3-TTS |
| **Image Gen** | Stable Diffusion WebUI (API) |
| **Installer** | Inno Setup (Windows) |
| **Real-time** | WebSocket |

---

## Project Scale

| Metric | Value |
|--------|-------|
| Source Files | 1,044+ |
| Projects in Solution | 14 |
| Estimated Lines of Code | 125,000+ |
| API Endpoints | 28 |
| Supported Languages | 6 |
| Translation Strings | 700+ |
| Heuristic Checks | 14+ |
| Packer Signatures | 30+ |
| Platform Builds | 5 |

---

## SDK & Integration

SyntheticAI exposes a RESTful API for third-party integration. See the [sdk/](sdk/) directory for client libraries in:

- **.NET** — Native C# client with full type safety
- **Python** — Lightweight wrapper for scan and threat APIs
- **TypeScript** — Browser and Node.js compatible client

API documentation: [docs/api-reference/](docs/api-reference/)

---

## Security

We take security seriously. See [SECURITY.md](SECURITY.md) for:
- Responsible disclosure policy
- Security architecture overview
- Vulnerability reporting process

---

## License

SGL SyntheticAI is proprietary software. See [LICENSE](LICENSE) for full terms.

**Evaluation copies** are available for qualified investors and enterprise customers. Contact: security@syntheticgamelabs.com

---

## About Synthetic Game Labs

Synthetic Game Labs is a cybersecurity technology company building the next generation of AI-native security platforms. Our mission is to make enterprise-grade threat detection accessible and autonomous.

**Website:** [syntheticgamelabs.com](https://syntheticgamelabs.com)

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
