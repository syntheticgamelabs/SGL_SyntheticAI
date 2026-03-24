# SGL SyntheticAI Python SDK

Official Python client library for the SGL SyntheticAI REST API.

## Installation

```bash
pip install syntheticai  # Coming soon to PyPI
```

Or install directly:

```bash
pip install requests
cp syntheticai_client.py your_project/
```

## Quick Start

```python
from syntheticai_client import SyntheticAIClient

# Connect and authenticate
client = SyntheticAIClient("http://your-server:5000")
client.login("admin", "your-password")

# Scan a file
result = client.scan_file("/path/to/suspicious.exe")
print(f"Verdict: {result['verdict']} (Score: {result['score']})")

# Get threat intelligence
threats = client.get_threats()
print(f"Total signatures: {threats['totalSignatures']}")

# Stream AI chat
for token in client.chat("Analyze recent threat patterns"):
    print(token, end="", flush=True)

# Server metrics (admin)
metrics = client.get_server_metrics()
print(f"CPU: {metrics['cpuUsage']}%")
```

## Requirements

- Python 3.8+
- `requests` library

## License

MIT License — see [../LICENSE](../LICENSE)
