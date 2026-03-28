# Ensemble Machine Learning for Threat Classification

> SyntheticAI Research | Synthetic Game Labs | 2026
> Contact: syntheticgamelabs@gmail.com
> https://syntheticgamelabs.dpdns.org

---

## Abstract

Single-model approaches to malware and threat classification suffer from
well-documented blind spots: tree-based models struggle with novel polymorphic
variants, while neural approaches lack interpretability for analyst review. This
paper presents the conceptual architecture of SyntheticAI's ensemble
classification system, which combines gradient boosted decision trees (GBDT),
graph-based machine learning risk propagation, and logistic regression into a
unified scoring pipeline. A streaming feature store built on Welford's online
algorithm provides numerically stable, real-time feature statistics without
requiring batch recomputation. Adaptive weighting adjusts each sub-model's
influence based on recent classification accuracy, ensuring the ensemble
self-corrects as threat landscapes evolve.

**Keywords:** ensemble learning, gradient boosting, graph neural networks,
logistic regression, Welford's algorithm, adaptive weighting, malware classification

---

## 1. Motivation

No single ML paradigm dominates across all threat categories. Gradient boosted
trees excel at structured feature classification (file metadata, PE header
fields) but generalize poorly to relationship-aware threats. Graph ML captures
process-to-process and network-to-host relationships but requires expensive
inference. Logistic regression provides a fast, interpretable baseline that
anchors the ensemble. By combining all three, the system achieves robustness
that no individual model provides alone.

## 2. Ensemble Architecture Overview

```
  Telemetry Event
        |
        v
+-------+--------+
|  Feature Store  |  <-- Welford's online stats (mean, variance, skew)
+--+---------+----+
   |         |        |
   v         v        v
+------+  +------+  +------+
| GBDT |  |Graph |  | LR   |
|Model |  |  ML  |  |Model |
+--+---+  +--+---+  +--+---+
   |         |         |
   v         v         v
+--+----+----+----+----+--+
|   Adaptive Weight Mixer  |
+------------+-------------+
             |
             v
    Composite Threat Score
       [0.0 -- 1.0]
```

## 3. Sub-Model Descriptions

### 3.1 Gradient Boosted Decision Trees (GBDT)

The GBDT component consumes structured, tabular features extracted from
endpoint telemetry:

- PE header entropy and section characteristics
- File size, signature status, and compilation timestamp
- Import table anomaly scores
- Registry and filesystem operation frequency vectors

GBDT models are retrained on a rolling window of labeled samples to maintain
currency against emerging threats.

### 3.2 Graph ML Risk Propagation

Endpoint activity naturally forms a directed graph: processes spawn children,
open network connections, read files, and modify registry keys. The graph ML
component models these relationships and propagates risk scores through the
graph using message-passing iterations:

```
   Risk Propagation (conceptual):

   [svchost.exe]---spawns--->[powershell.exe]---connects--->[C2 IP]
       risk: 0.1                risk: 0.4                   risk: 0.95
                   \                        \
                    `-- propagated: 0.3 ---->`-- propagated: 0.7
```

Nodes receive risk from their neighbors weighted by edge type and temporal
proximity. After convergence, each node carries a contextualized risk score
reflecting its full behavioral neighborhood.

### 3.3 Logistic Regression Baseline

A logistic regression model trained on the same feature set as the GBDT
provides a fast, linear baseline. Its primary role is anchoring: when the
GBDT and graph ML models disagree, the LR score acts as a tiebreaker and
provides an interpretable coefficient vector for analyst review.

## 4. Streaming Feature Store with Welford's Algorithm

Real-time classification requires real-time feature statistics. SyntheticAI's
feature store maintains running mean, variance, and higher-order moments using
Welford's online algorithm, which is numerically stable even across billions of
observations:

```
  For each incoming observation x_n:
    delta     = x_n - mean_(n-1)
    mean_n    = mean_(n-1) + delta / n
    delta2    = x_n - mean_n
    M2_n      = M2_(n-1) + delta * delta2
    variance  = M2_n / (n - 1)
```

This eliminates the need for periodic batch recomputation and ensures that
feature normalization reflects the most current telemetry distribution.

## 5. Adaptive Weight Mixing

Static ensemble weights degrade as the threat landscape shifts. SyntheticAI
employs an exponentially weighted accuracy tracker for each sub-model:

- A sliding window of recent predictions is maintained per model.
- Each model's weight is proportional to its recent precision/recall on
  confirmed true positives and true negatives.
- Weight updates occur continuously, not on a retraining schedule.
- A minimum weight floor prevents any single model from being fully silenced,
  preserving ensemble diversity.

The result is an ensemble that self-corrects: if a new malware family evades
the GBDT but is caught by graph ML, the mixer shifts weight toward graph ML
within minutes, not weeks.

## 6. Interpretability and Analyst Trust

Each classification decision is accompanied by:

- Per-model scores and confidence intervals
- Top-5 contributing features from the GBDT (via SHAP values)
- The risk propagation subgraph from the graph ML component
- Logistic regression coefficient magnitudes for key features

This transparency enables analysts to validate machine decisions and builds
the trust necessary for autonomous response actions.

## 7. Future Research

Active areas of investigation include transformer-based sequence models for
command-line argument analysis, federated learning across organizational
boundaries without sharing raw telemetry, and reinforcement learning for
dynamic threshold optimization.

---

## References

1. Welford, B.P., "Note on a Method for Calculating Corrected Sums of Squares," Technometrics, 1962.
2. Lundberg, S. and Lee, S., "A Unified Approach to Interpreting Model Predictions," NeurIPS, 2017.
3. Chen, T. and Guestrin, C., "XGBoost: A Scalable Tree Boosting System," KDD, 2016.
4. Gilmer, J. et al., "Neural Message Passing for Quantum Chemistry," ICML, 2017.

---

> This paper describes research concepts. Proprietary implementations are maintained in private repositories.
>
> Copyright (c) 2026 Synthetic Game Labs. All rights reserved.
