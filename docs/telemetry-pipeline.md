# Distributed Telemetry Collection & Normalization Pipeline

> SyntheticAI Research Documentation | Synthetic Game Labs

---

## 1. Overview

SyntheticAI ingests endpoint telemetry at scale across heterogeneous environments—Windows,
Linux, macOS, and containerized workloads. The Telemetry Pipeline moves raw events from
millions of sensors into a unified, query-ready data lake with sub-second latency.

---

## 2. High-Level Architecture

```
 ┌──────────┐  ┌──────────┐  ┌──────────┐
 │ Endpoint  │  │ Endpoint  │  │ Endpoint  │   ... x N
 │  Agent    │  │  Agent    │  │  Agent    │
 └────┬─────┘  └────┬─────┘  └────┬─────┘
      │              │              │
      ▼              ▼              ▼
 ┌─────────────────────────────────────────┐
 │           EventBus (Pub/Sub)            │
 │  Topics: process | network | file | reg │
 └────────────────┬────────────────────────┘
                  │
      ┌───────────┼───────────┐
      ▼           ▼           ▼
 ┌──────────┐ ┌──────────┐ ┌──────────┐
 │Normalizer│ │Enrichment│ │ Filter / │
 │  Stage   │ │  Stage   │ │  Dedup   │
 └────┬─────┘ └────┬─────┘ └────┬─────┘
      │             │             │
      ▼             ▼             ▼
 ┌─────────────────────────────────────────┐
 │         Data Lake Ingestion Layer       │
 │  ┌──────────┬──────────┬──────────┐     │
 │  │   HOT    │   WARM   │   COLD   │     │
 │  │ (0-24h)  │ (1-30d)  │ (30d+)   │     │
 │  └──────────┴──────────┴──────────┘     │
 └─────────────────────────────────────────┘
```

## 3. EventBus Pub/Sub Layer

The EventBus is a durable, partitioned message bus decoupling producers from consumers.

- **Topic Isolation** — Discrete topics (`process.create`, `network.connection`,
  `file.write`, `registry.modify`) let consumers subscribe to only relevant event classes.
- **At-Least-Once Delivery** — Globally unique event IDs enable downstream deduplication
  for exactly-once semantics without expensive transactional guarantees at the bus level.
- **Back-Pressure Propagation** — Agents buffer locally when consumers lag, preventing
  data loss during burst periods.
- **Partition Strategy** — Events partitioned by endpoint identity preserve per-host
  ordering while enabling horizontal consumer scaling.

## 4. Normalizer Stage

Raw telemetry arrives in platform-specific formats (ETW on Windows, eBPF on Linux, ESF
on macOS). The Normalizer maps every event into the **Canonical Event Schema (CES)**:

| CES Field        | Description                                    |
|------------------|------------------------------------------------|
| `event_id`       | UUID v7 (time-sortable)                        |
| `timestamp_utc`  | Microsecond-precision UTC timestamp            |
| `endpoint_id`    | Persistent agent identity                      |
| `event_class`    | Enum: process, network, file, registry, auth   |
| `subject`        | Entity performing the action                   |
| `object`         | Entity being acted upon                        |
| `metadata`       | Platform-specific fields preserved as key-value |

## 5. Enrichment Stage

After normalization, events pass through multi-phase enrichment:

1. **Geo-IP Resolution** — Network events gain geographic context (country, ASN, hosting
   provider) for identifying traffic to high-risk regions.
2. **Threat Intelligence Join** — Hashes, domains, and IPs matched against continuously
   updated STIX/TAXII threat intelligence feeds.
3. **Asset Context Injection** — Events annotated with business unit, criticality tier,
   and network zone from the organization's CMDB.
4. **Parent-Chain Linkage** — Process events linked to full ancestry chains for
   execution lineage reasoning.

## 6. Hot / Warm / Cold Storage Tiering

```
  Query Latency      Storage Cost
      ▲                   ▲
 <10ms│  ┌───────┐        │  $$$   HOT  — In-memory columnar
      │  └───┬───┘        │
<500ms│  ┌───▼───┐        │  $$    WARM — Compressed SSD columnar
      │  └───┬───┘        │
  <30s│  ┌───▼───┐        │  $     COLD — Parquet on object storage
      │  └───────┘        │
      └──────────────────►└──────────────────►
```

- **Hot (0–24h)** — Powers real-time detection, dashboards, and AI inference.
- **Warm (1–30d)** — Serves analyst investigation and threat hunting queries.
- **Cold (30d+)** — Compliance retention (90 days to 7 years). Queryable via federated SQL.

Tier migration is automated and policy-driven per event class.

## 7. Scalability

- **Horizontal Scaling** — All stages are stateless and independently scalable.
- **Exactly-Once Semantics** — Idempotent writes keyed on `event_id`.
- **Throughput** — 500,000+ events/sec/tenant, p99 latency under 3 seconds end-to-end.

---

**Contact:** Synthetic Game Labs — syntheticgamelabs@gmail.com | syntheticgamelabs.dpdns.org

> **Note:** This document describes architectural concepts. Proprietary implementations are maintained in private repositories.

*Copyright 2025-2026 Synthetic Game Labs. All rights reserved.*
