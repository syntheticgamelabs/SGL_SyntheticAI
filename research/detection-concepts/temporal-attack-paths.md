# Temporal Attack Path Analysis

> SyntheticAI Research | Synthetic Game Labs | 2026
> Contact: syntheticgamelabs@gmail.com
> https://syntheticgamelabs.dpdns.org

---

## Abstract

Static attack graph analysis treats all edges as equally viable regardless of
when the underlying events occurred. In practice, an attacker's window of
opportunity decays over time -- credentials rotate, sessions expire, and
defensive actions close pathways. This paper introduces the concept of temporal
attack path analysis, a time-aware graph traversal methodology that applies
exponential decay weighting to edges in an endpoint activity graph. The result
is a risk scoring model that prioritizes active, temporally coherent attack
paths over stale or fragmented ones, significantly reducing false positive
rates in alert prioritization.

**Keywords:** attack graphs, temporal analysis, decay weighting, risk scoring,
graph traversal, alert prioritization

---

## 1. Limitations of Static Attack Graphs

Traditional attack graphs model the space of possible attacker movements:
nodes represent system states or assets, and edges represent transitions
(exploits, credential use, lateral movement). However, static graphs suffer
from combinatorial explosion -- the number of theoretical paths grows
exponentially with network size, and the vast majority of these paths are
not temporally viable at any given moment.

## 2. Time-Aware Edge Weighting

The core concept is to assign each edge a weight that decays exponentially
from the time the underlying event was observed:

```
  Edge Weight Function:

  w(e, t) = w_0 * exp(-lambda * (t_now - t_event))

  where:
    w_0      = base weight (derived from event severity)
    lambda   = decay constant (tunable per edge type)
    t_event  = timestamp of the observed event
    t_now    = current evaluation time
```

This ensures that recently observed attacker actions contribute strongly
to path risk, while historical events gracefully fade unless refreshed by
new observations.

## 3. Decay Constants by Edge Type

Different attacker actions have different temporal relevance windows:

```
  +-------------------------+------------+------------------+
  | Edge Type               | Half-Life  | Rationale        |
  +-------------------------+------------+------------------+
  | Active C2 connection    | 5 min      | Highly volatile  |
  | Credential exposure     | 4 hours    | Until rotation   |
  | Persistence mechanism   | 7 days     | Survives reboot  |
  | Vulnerability presence  | 30 days    | Until patching   |
  | Configuration weakness  | 90 days    | Slow remediation |
  +-------------------------+------------+------------------+
```

## 4. Temporal Path Traversal Algorithm

The traversal proceeds as a modified Dijkstra's algorithm over the
time-weighted graph:

```
  +-------------+       decay-weighted        +-------------+
  |  Initial    | --------edge_1----------->  | Intermediate|
  |  Access     |    w=0.9 (2 min ago)        |   Host      |
  +-------------+                             +------+------+
                                                     |
                                          edge_2     |  w=0.7
                                        (15 min ago) |
                                                     v
                                              +------+------+
                                              |   Target    |
                                              |   Asset     |
                                              +-------------+

  Path Risk = product(edge weights) along highest-risk path
```

Unlike traditional shortest-path algorithms that minimize cost, the temporal
traversal seeks the path with the maximum cumulative risk product, representing
the most viable current attack vector.

## 5. Composite Node Risk Scoring

Each node accumulates risk from all temporally viable inbound paths:

- Paths are enumerated using depth-bounded traversal (default depth: 8).
- Each path's composite risk is the product of its decay-weighted edges.
- A node's aggregate risk is the sum of its top-K path risks, preventing
  a single high-risk path from being diluted by many low-risk ones.

This produces a risk score that naturally elevates nodes at the convergence
of multiple active attack indicators.

## 6. Practical Applications

- **Alert prioritization**: Alerts associated with high temporal-risk nodes
  are promoted in the analyst queue ahead of alerts on stale paths.
- **Proactive defense**: Nodes with rising temporal risk scores can trigger
  automated containment actions before full compromise materializes.
- **Hunt guidance**: The highest-risk temporal paths serve as starting
  hypotheses for human threat hunting investigations.

## 7. Calibration and Tuning

Decay constants are calibrated against historical incident data: for each edge
type, the system measures the empirical distribution of time-to-exploitation
and fits the decay curve to the 90th percentile. This data-driven calibration
replaces manual tuning and adapts to organizational-specific attacker dwell
times.

## 8. Future Directions

Research continues into non-exponential decay models (e.g., step functions
for credential rotation events), integration of threat intelligence freshness
into edge weights, and GPU-accelerated traversal for large-scale enterprise
graphs with millions of nodes.

---

## References

1. Ou, X. et al., "MulVAL: A Logic-based Network Security Analyzer," USENIX Security, 2005.
2. Noel, S. and Jajodia, S., "Optimal IDS Sensor Placement and Alert Prioritization Using Attack Graphs," JCS, 2008.
3. Mitre ATT&CK Framework, https://attack.mitre.org

---

> This paper describes research concepts. Proprietary implementations are maintained in private repositories.
>
> Copyright (c) 2026 Synthetic Game Labs. All rights reserved.
