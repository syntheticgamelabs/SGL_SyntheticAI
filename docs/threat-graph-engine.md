# Threat Graph Engine

> SyntheticAI Research Documentation | Synthetic Game Labs

## Overview

The Threat Graph Engine is the core intelligence layer of SyntheticAI. It models cybersecurity threats as a directed graph where nodes represent entities (hosts, processes, files, IPs, domains) and edges represent observed relationships (spawned, connected-to, wrote-file, loaded-dll).

## Concept

Traditional security tools treat events in isolation. A process spawning cmd.exe is one alert. A DNS query to a suspicious domain is another. A file being written to a temp directory is a third.

The Threat Graph Engine connects these events into a coherent attack narrative:

```
phishing.docx
     │
     ▼ (spawned)
  winword.exe
     │
     ├──▶ (spawned) powershell.exe
     │         │
     │         ├──▶ (connected) 45.33.12.7:443
     │         │
     │         └──▶ (wrote) C:\Temp\payload.exe
     │                    │
     │                    └──▶ (spawned) payload.exe
     │                              │
     │                              ├──▶ (connected) 185.22.11.3:8080
     │                              └──▶ (modified) HKLM\...\Run
     │
     └──▶ (dns-query) evil-domain.com
```

This graph representation enables:
- **Kill chain reconstruction** — Trace the full attack from initial access to persistence
- **Lateral movement detection** — Identify when attacks spread across hosts
- **Campaign correlation** — Link seemingly unrelated events across endpoints
- **Risk propagation** — Calculate cascading risk through connected nodes

## Graph Schema

### Node Types
| Type | Description | Attributes |
|------|-------------|------------|
| Host | Endpoint device | hostname, OS, IP, risk_score |
| Process | Running process | name, PID, hash, command_line |
| File | File on disk | path, hash, size, signature |
| NetworkEndpoint | IP:Port | address, port, country, ASN |
| Domain | DNS name | name, registrar, first_seen |
| User | User account | name, SID, privilege_level |
| Registry | Registry key | path, value, modification_time |

### Edge Types
| Type | From → To | Description |
|------|-----------|-------------|
| spawned | Process → Process | Parent created child process |
| connected | Process → NetworkEndpoint | Outbound network connection |
| wrote | Process → File | File creation or modification |
| loaded | Process → File | DLL/module loading |
| dns_query | Process → Domain | DNS resolution request |
| modified_reg | Process → Registry | Registry modification |
| belongs_to | Process → Host | Process running on host |
| logged_in | User → Host | User authentication event |

## Temporal Analysis

Every edge carries a timestamp, enabling time-aware graph traversal:

- **Time-windowed queries** — "Show all connections from Host A in the last 4 hours"
- **Decay-weighted risk** — Recent edges contribute more to risk scores than old ones
- **Temporal attack paths** — Find sequences of events that follow the attack timeline
- **Campaign detection** — Cluster events that occur within temporal proximity

## Risk Propagation

Risk flows through the graph using belief propagation:

```
Initial Risk Assignment
        │
        ▼
For each propagation round (5 rounds):
    For each node:
        new_risk = (self_weight × own_risk) +
                   (neighbor_weight × avg(neighbor_risks))

        Apply decay factor per hop distance
```

This causes risk to cascade: a high-risk process makes its parent and child processes more suspicious, its connected IPs more suspicious, and its host more suspicious.

## Use Cases

1. **Incident Investigation** — Given an alert, traverse the graph to understand the full attack scope
2. **Threat Hunting** — Query the graph for suspicious patterns (e.g., "processes that connected to >5 unique external IPs")
3. **Campaign Detection** — Find clusters of related threats across multiple hosts
4. **Impact Assessment** — Determine how far an attacker has progressed through the environment

---

> **Note:** This document describes the architectural concepts. The proprietary graph implementation, scoring algorithms, and traversal optimizations are maintained in private repositories.

---

<p align="center">
  <sub>Copyright 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
