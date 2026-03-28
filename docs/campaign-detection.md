# Cross-Endpoint Campaign Detection

> SyntheticAI Research Documentation | Synthetic Game Labs

---

## 1. Overview

Advanced persistent threats do not operate on a single machine. Adversaries move laterally,
stage data across hosts, and coordinate multi-endpoint activity. SyntheticAI's Campaign
Detection engine correlates behavioral signals across the entire fleet and groups related
activity into unified **campaign objects**, giving defenders a coherent intrusion view
rather than hundreds of isolated alerts.

## 2. Architecture

```
  Endpoint A         Endpoint B         Endpoint C
  ┌─────────┐        ┌─────────┐        ┌─────────┐
  │Behavioral│        │Behavioral│        │Behavioral│
  │ Signals  │        │ Signals  │        │ Signals  │
  └────┬─────┘        └────┬─────┘        └────┬─────┘
       │                   │                   │
       ▼                   ▼                   ▼
  ┌────────────────────────────────────────────────┐
  │          Behavioral Clustering Engine           │
  │  Feature Extraction ──► Similarity Scoring      │
  │                    ┌─────────▼──────────┐       │
  │                    │ Union-Find Campaign │       │
  │                    │    Grouping         │       │
  │                    └─────────┬──────────┘       │
  │               ┌──────────────┼──────────┐       │
  │               ▼              ▼          ▼       │
  │         ┌──────────┐  ┌──────────┐ ┌────────┐  │
  │         │ Temporal  │  │  MITRE   │ │Campaign│  │
  │         │Correlation│  │ Mapping  │ │ Object │  │
  │         └──────────┘  └──────────┘ └────────┘  │
  └────────────────────────────────────────────────┘
```

## 3. Behavioral Clustering

SyntheticAI clusters on **behavioral features** rather than IOCs that attackers rotate:

| Feature Category       | Examples                                            |
|------------------------|-----------------------------------------------------|
| Process lineage shape  | Child-process tree depth, rare parent-child pairs   |
| Network profile        | Destination diversity, protocol mix, timing patterns |
| File system mutations  | Writes to sensitive directories, extension changes  |
| Authentication signals | Logon type distribution, failed-to-success ratios   |
| Persistence mechanisms | Registry modifications, scheduled task creation     |

Feature vectors computed over sliding windows are compared across endpoints using distance
metrics robust to minor variations in attacker tooling.

## 4. Union-Find Campaign Grouping

Related activity clusters are merged into campaigns using a Union-Find (disjoint-set)
strategy that provides critical properties:

```
  Initial: each cluster is its own set

  Cluster A ─── Cluster B     Cluster C ─── Cluster D
  (Host 1)      (Host 3)      (Host 2)      (Host 5)

  Similarity edge between B and C triggers merge:

  ┌──────────────────────────────────────────────┐
  │              Campaign #0047                   │
  │  Cluster A ── Cluster B ── Cluster C ── D     │
  │  (Host 1)     (Host 3)     (Host 2)  (Host 5) │
  └──────────────────────────────────────────────┘
```

- **Transitive Closure** — A relates to B, B relates to C: all three merge even without
  direct A-C indicators, capturing multi-hop lateral movement.
- **Incremental Merging** — New telemetry merges into existing campaigns in near-constant
  time without reprocessing historical data.
- **Path Compression** — Membership lookups remain efficient across hundreds of endpoints.

## 5. Temporal Correlation

Behavioral similarity alone is insufficient. The temporal layer validates plausible timelines:

- **Causal Ordering** — Reconnaissance precedes exploitation precedes lateral movement.
- **Time Window Constraints** — Related cross-endpoint activity must fall within
  configurable bounds (e.g., 48 hours between compromise and lateral movement).
- **Burst Detection** — Simultaneous credential harvesting across multiple hosts is
  flagged as high-confidence campaign evidence.

```
  Campaign #0047 Timeline:

  Hour 0     Hour 2      Hour 6        Hour 8       Hour 14
    │          │           │             │             │
    ▼          ▼           ▼             ▼             ▼
  Host 1:   Host 1:     Host 3:       Host 2:      Host 5:
  Phishing  Credential  Lateral       Discovery    Data
  payload   dump        movement      commands     staging
```

## 6. MITRE ATT&CK Mapping

Every campaign is automatically mapped to ATT&CK techniques:

```
  Campaign #0047 ATT&CK Coverage:
  ┌──────────────────┬────────────────────────┬────────────┐
  │ Tactic           │ Technique              │ Confidence │
  ├──────────────────┼────────────────────────┼────────────┤
  │ Initial Access   │ T1566.001 Spearphishing│ HIGH       │
  │ Credential Acc.  │ T1003.001 LSASS Memory │ HIGH       │
  │ Lateral Movement │ T1021.002 SMB/Admin$   │ MEDIUM     │
  │ Discovery        │ T1018 Remote Sys Disc. │ HIGH       │
  │ Collection       │ T1560.001 Archive Data │ MEDIUM     │
  └──────────────────┴────────────────────────┴────────────┘
```

Mapping enables common communication language, visibility gap analysis, and threat
intelligence correlation against known adversary profiles for attribution hypotheses.

## 7. Campaign Object Output

The final campaign object contains: unique identifier, constituent endpoint clusters with
behavioral summaries, temporal timeline with causal ordering, ATT&CK mapping with
per-technique confidence, affected asset inventory, severity score, and recommended
containment actions. Objects are surfaced in dashboards and exported as structured JSON
for SIEM/SOAR integration.

---

**Contact:** Synthetic Game Labs — syntheticgamelabs@gmail.com | syntheticgamelabs.dpdns.org

> **Note:** This document describes architectural concepts. Proprietary implementations are maintained in private repositories.

*Copyright 2025-2026 Synthetic Game Labs. All rights reserved.*
