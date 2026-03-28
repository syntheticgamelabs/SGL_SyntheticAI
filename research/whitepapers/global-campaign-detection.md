# Cross-Endpoint Campaign Detection via Behavioral Clustering

> SyntheticAI Research | Synthetic Game Labs | 2026
> Contact: syntheticgamelabs@gmail.com
> https://syntheticgamelabs.dpdns.org

---

## Abstract

Advanced persistent threats and coordinated attack campaigns rarely confine
themselves to a single endpoint. Yet the majority of endpoint detection systems
evaluate hosts in isolation, missing the macro-level patterns that distinguish
a coordinated campaign from unrelated individual compromises. This paper
describes SyntheticAI's research into cross-endpoint campaign detection, which
applies temporal clustering and inter-host behavioral graph analysis to identify
coordinated multi-host attacks. By lifting detection from the single-endpoint
plane to a global behavioral view, the system surfaces campaigns that would
otherwise remain invisible to host-level analytics.

**Keywords:** campaign detection, behavioral clustering, temporal analysis,
graph analytics, lateral movement, multi-host correlation, APT detection

---

## 1. The Isolated-Endpoint Blind Spot

Consider an adversary who compromises Host A via phishing, performs credential
harvesting, moves laterally to Host B, escalates privileges, then exfiltrates
data from Host C. Each host may generate only low-severity alerts individually:

```
  Host A: Suspicious Office macro        --> Severity: Medium
  Host B: LSASS memory access            --> Severity: Low
  Host C: Unusual outbound data volume   --> Severity: Low
```

In isolation, none of these alerts warrant urgent response. Viewed together
with temporal and causal links, they form a textbook intrusion chain.

## 2. Behavioral Feature Extraction

For each endpoint, the system extracts a time-series behavioral fingerprint
consisting of:

- **Process execution patterns** -- command-line token frequency vectors
- **Network behavior** -- destination entropy, port diversity, connection timing
- **File system activity** -- write volume, path entropy, extension distribution
- **Authentication events** -- logon type distribution, credential usage anomalies

These features are computed in rolling 5-minute windows and normalized against
each host's historical baseline.

## 3. Temporal Clustering Algorithm

The core insight is that coordinated attacks produce temporally correlated
behavioral deviations across multiple hosts. The detection pipeline operates
in three phases:

```
  Phase 1: Deviation Detection
  +----------+    +----------+    +----------+
  | Host A   |    | Host B   |    | Host C   |
  | baseline |    | baseline |    | baseline |
  | deviation|    | deviation|    | deviation|
  +----+-----+    +----+-----+    +-----+----+
       |               |                |
       v               v                v
  Phase 2: Temporal Alignment
  +----------------------------------------------+
  |  Sliding Window Correlation Matrix            |
  |  (pairwise deviation co-occurrence scores)    |
  +----------------------+-----------------------+
                         |
                         v
  Phase 3: Cluster Extraction
  +----------------------+-----------------------+
  |  DBSCAN over temporal-behavioral space        |
  |  (hosts with correlated anomalies grouped)    |
  +----------------------------------------------+
```

### 3.1 Deviation Detection

Each host's current behavioral vector is compared to its historical baseline
using Mahalanobis distance, which accounts for feature covariance. A host is
flagged as "deviating" when its distance exceeds a dynamically computed
threshold based on the fleet-wide deviation distribution.

### 3.2 Temporal Alignment

Deviating hosts are projected into a shared temporal space. A sliding window
(default: 30 minutes, step: 5 minutes) computes pairwise co-occurrence scores
measuring how often two hosts deviate in the same or adjacent windows. High
co-occurrence suggests coordination.

### 3.3 Cluster Extraction

DBSCAN (Density-Based Spatial Clustering of Applications with Noise) is applied
over the co-occurrence matrix to group hosts into candidate campaign clusters.
DBSCAN is chosen for its ability to identify clusters of arbitrary shape and
its natural handling of noise points (hosts with coincidental deviations).

## 4. Inter-Host Causal Graph Construction

Once a cluster is identified, the system builds a directed causal graph to
reconstruct the attack narrative:

```
  [Host A]---credential_reuse--->[Host B]---lateral_tool_transfer--->[Host C]
     |                               |                                  |
     +--phishing_email               +--privilege_escalation            +--exfiltration
     (t=0)                           (t=+12min)                        (t=+38min)
```

Edges are inferred from shared credentials, network connections between hosts,
file transfers, and temporal ordering. The graph is annotated with ATT&CK
technique labels where mapping is possible.

## 5. Campaign Confidence Scoring

Each candidate campaign receives a confidence score based on:

- **Cluster density** -- tighter temporal correlation yields higher confidence
- **Causal chain completeness** -- campaigns with clear progression from initial
  access through exfiltration score higher
- **ATT&CK coverage** -- campaigns spanning multiple tactic categories are
  elevated over single-tactic clusters
- **Historical pattern matching** -- similarity to previously confirmed
  campaigns increases confidence

## 6. Operational Considerations

Campaign detection operates on a different timescale than single-host alerting.
The system is designed to surface campaign hypotheses within 1 hour of
sufficient evidence accumulation, balancing detection speed against false
positive rate. Campaign findings are presented as interactive graph
visualizations showing the full multi-host attack narrative.

## 7. Limitations and Future Work

Current limitations include sensitivity to clock synchronization across
endpoints and the assumption that adversary actions produce measurable
behavioral deviations. Future research directions include applying graph
neural networks to the causal graph for automated campaign classification
and incorporating deception-based telemetry (honeytokens) to accelerate
campaign discovery.

---

## References

1. Ester, M. et al., "A Density-Based Algorithm for Discovering Clusters," KDD, 1996.
2. Milajerdi, S. M. et al., "HOLMES: Real-Time APT Detection through Correlation of Suspicious Information Flows," IEEE S&P, 2019.
3. Mitre ATT&CK Framework, https://attack.mitre.org
4. Mahalanobis, P.C., "On the Generalised Distance in Statistics," 1936.

---

> This paper describes research concepts. Proprietary implementations are maintained in private repositories.
>
> Copyright (c) 2026 Synthetic Game Labs. All rights reserved.
