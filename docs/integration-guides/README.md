# Integration Guide — SGL SyntheticAI

This guide covers integrating third-party applications with SGL SyntheticAI's REST API.

---

## Quick Start

### 1. Get an API Token

```bash
curl -X POST http://your-server:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"your-password"}'
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "role": "admin"
}
```

### 2. Scan a File

```bash
curl -X POST http://your-server:5000/api/v1/scan/file \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@suspicious.exe"
```

### 3. Check Threat Intelligence

```bash
curl http://your-server:5000/api/v1/threats \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Integration Patterns

### SIEM Integration

Forward SyntheticAI alerts to your SIEM by subscribing to the WebSocket channel:

```python
import websocket
import json

def on_message(ws, message):
    event = json.loads(message)
    if event["type"] == "threat.alert":
        # Forward to SIEM (Splunk, ELK, etc.)
        forward_to_siem(event["data"])

ws = websocket.WebSocketApp(
    "ws://your-server:5000/ws",
    on_message=on_message,
    header=["Authorization: Bearer YOUR_TOKEN"]
)
ws.run_forever()
```

### CI/CD Pipeline Integration

Scan build artifacts before deployment:

```yaml
# GitHub Actions example
- name: Scan build artifact
  run: |
    TOKEN=$(curl -s -X POST http://scanner:5000/api/v1/auth/login \
      -H "Content-Type: application/json" \
      -d '{"username":"ci-user","password":"${{ secrets.SCANNER_PASS }}"}' \
      | jq -r '.token')

    RESULT=$(curl -s -X POST http://scanner:5000/api/v1/scan/file \
      -H "Authorization: Bearer $TOKEN" \
      -F "file=@build/output.exe")

    VERDICT=$(echo $RESULT | jq -r '.verdict')
    if [ "$VERDICT" != "clean" ]; then
      echo "Security scan failed: $VERDICT"
      exit 1
    fi
```

### Automated Response

Use the scan API with webhooks for automated quarantine:

```python
from syntheticai import SyntheticAIClient

client = SyntheticAIClient("http://your-server:5000", token="YOUR_TOKEN")

# Scan and auto-quarantine
result = client.scan_file("/path/to/file.exe")
if result.verdict == "malicious":
    client.quarantine(result.file_path)
    client.notify_admin(f"Quarantined: {result.file_name}")
```

---

## WebSocket Events

Subscribe to real-time events for monitoring dashboards:

| Event Type | Description | Data |
|-----------|-------------|------|
| `scan.started` | Scan initiated | `{ scanId, path }` |
| `scan.result` | Scan completed | `{ scanId, verdict, score }` |
| `threat.alert` | Threat detected | `{ score, hash, classification }` |
| `notification` | System notification | `{ title, body, severity }` |
| `llm.inference.start` | LLM inference began | `{ slot, model }` |
| `llm.inference.end` | LLM inference completed | `{ slot, tokensPerSec }` |
| `campaign.detected` | Campaign identified | `{ campaignId, endpoints, score }` |

---

## Rate Limits (Planned v1.1.47)

| Endpoint Group | Rate Limit |
|---------------|-----------|
| Authentication | 10 req/min |
| Scanning | 100 req/min |
| Chat/LLM | 30 req/min |
| Admin | 60 req/min |
| Public | 300 req/min |

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
