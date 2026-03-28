# Network-Level Threat Detection & Deep Packet Analysis

> SyntheticAI Research Documentation | Synthetic Game Labs

---

## 1. Overview

Endpoint telemetry captures what happens on the host. Network telemetry captures what
happens between hosts and the outside world. SyntheticAI's Deep Packet Analysis layer
provides network-level threat detection covering DNS exfiltration, TLS abuse, protocol
anomalies, and C2 beacon patterns. It operates on metadata and flow records—no full
packet capture required—making it deployable at enterprise scale.

## 2. Architecture

```
  ┌───────────────────────────────────────────────────────┐
  │              Network Sensor Plane                     │
  │  ┌──────────┐  ┌──────────┐  ┌──────────┐            │
  │  │  Tap /   │  │ DNS Log  │  │ Proxy /  │  ... N     │
  │  │  Span    │  │ Resolver │  │ Firewall │  sources    │
  │  └────┬─────┘  └────┬─────┘  └────┬─────┘            │
  └───────┼──────────────┼──────────────┼─────────────────┘
          ▼              ▼              ▼
  ┌───────────────────────────────────────────────────────┐
  │              Analysis Engine Layer                    │
  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐  │
  │  │     DNS      │ │   TLS/SNI    │ │   Protocol   │  │
  │  │ Exfiltration │ │  Inspection  │ │   Anomaly    │  │
  │  │  Detector    │ │   Engine     │ │   Detector   │  │
  │  └──────────────┘ └──────────────┘ └──────────────┘  │
  │  ┌──────────────────────────────────────────────────┐ │
  │  │           Beacon Interval Detector               │ │
  │  └──────────────────────────────────────────────────┘ │
  └──────────────────────┬────────────────────────────────┘
                         ▼
  ┌───────────────────────────────────────────────────────┐
  │  Correlation with Endpoint Telemetry & Campaign Engine │
  └───────────────────────────────────────────────────────┘
```

## 3. DNS Exfiltration Detection via Entropy Analysis

Attackers encode stolen data into subdomain labels of domains they control. Because DNS
traffic is rarely inspected, it is one of the most reliable exfiltration channels.

```
  Shannon Entropy Distribution:

  Legitimate DNS:    ████░░░░░░░░░░░░░░  avg 2.8 bits/char
  DGA Domains:       ██████████░░░░░░░░  avg 4.2 bits/char
  Exfil-Encoded DNS: ████████████████░░  avg 5.1 bits/char
                     ──────────────────
                     0   1   2   3   4   5   6  (entropy)
```

Entropy scoring is combined with supporting signals: abnormal query volume to a single
domain, maximized 63-character label lengths, 4+ subdomain depth, TXT record abuse for
bidirectional tunnels, and high unique-subdomain cardinality under one parent domain.

## 4. TLS/SNI Inspection

SyntheticAI inspects TLS handshake metadata without breaking encryption:

```
  Client ─── ClientHello ───► Server
         SNI: suspicious.xyz
         JA3: e7d705a3286e19ea...

  Client ◄── ServerHello ──── Server
         JA3S: b32309a26951912...
         Cert: CN=suspicious.xyz
               Valid: 7 days (!)
```

- **SNI Reputation** — Flags connections to recently registered, algorithmically
  generated, or known-malicious domains.
- **JA3/JA3S Fingerprinting** — TLS client/server fingerprints from cipher suite
  ordering and extensions. Malware families produce distinctive persistent fingerprints.
- **Certificate Analysis** — Self-signed, short-lived, or anomalous-subject certificates.
- **Version Anomalies** — Deprecated TLS versions or unusual cipher suites.

## 5. Protocol Anomaly Detection

Threat actors tunnel C2 through standard protocols but exhibit subtle deviations:

- **HTTP Header Ordering** — Custom implants deviate from browser/library patterns.
- **State Machine Violations** — Protocol transitions violating RFC specifications.
- **Payload-Protocol Mismatch** — Non-TLS on port 443 or non-DNS on port 53 indicates
  protocol tunneling.
- **Request-Response Ratio** — Abnormal upload-to-download ratios indicate exfiltration.

## 6. Beacon Interval Detection

C2 implants periodically call home, producing distinctive timing patterns:

```
  Regular beacon:   ──┤60s├──┤60s├──┤60s├──┤60s├──►
  Jittered beacon:  ──┤58s├──┤63s├──┤57s├──┤61s├──►  (±5% jitter)
```

- **Frequency Domain Analysis** — Reveals dominant periodicities even with jitter.
- **Jitter-Tolerant Clustering** — Tolerance bands calibrated to common jitter
  percentages (1%, 5%, 10%, 25%).
- **Long-Duration Observation** — Rolling connection histories over configurable
  retention periods catch slow-interval beacons (hours, days).
- **Multi-Stage Detection** — Identifies compound periodic patterns when implants
  use multiple beacon intervals (fast check-in, slow exfiltration).

## 7. Cross-Layer Correlation

Every network detection is correlated with endpoint telemetry:

| Network Signal             | Endpoint Correlation                          |
|----------------------------|-----------------------------------------------|
| DNS exfiltration detected  | Which process initiated the queries?          |
| Suspicious JA3 fingerprint | What binary produced the TLS connection?      |
| Beacon pattern identified  | Is the source process a known LOLBin?         |
| Protocol anomaly flagged   | Does the process have legitimate network need? |

This cross-layer approach dramatically reduces false positives and provides complete
attack context spanning network and host dimensions.

---

**Contact:** Synthetic Game Labs — syntheticgamelabs@gmail.com | syntheticgamelabs.dpdns.org

> **Note:** This document describes architectural concepts. Proprietary implementations are maintained in private repositories.

*Copyright 2025-2026 Synthetic Game Labs. All rights reserved.*
