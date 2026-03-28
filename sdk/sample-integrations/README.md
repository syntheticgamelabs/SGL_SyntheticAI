# SyntheticAI Sample Integrations

> Copyright 2024-2026 Synthetic Game Labs. All rights reserved.

## Overview

This directory contains lightweight, production-safe sample scripts that
demonstrate common integration patterns with the SyntheticAI platform.
Each script is self-contained and requires only the Python standard library
plus the `requests` package.

## Integration Patterns

### 1. SIEM Forwarding (Syslog / CEF)

Forward SyntheticAI alerts to any SIEM that accepts Syslog or CEF input.

- **Script**: `syslog_forwarder.py`
- **Protocol**: UDP Syslog (RFC 5424) with optional CEF formatting
- **Use case**: Splunk, QRadar, ArcSight, Elastic SIEM

```bash
export SYNTHETICAI_API_KEY="your-jwt-token"
python syslog_forwarder.py --syslog-host 10.0.0.50 --syslog-port 514
```

### 2. Webhook Alerts

Push high-severity alerts to any HTTP endpoint in real time.

- **Script**: `webhook_example.py`
- **Protocol**: HTTPS POST with JSON payload
- **Use case**: PagerDuty, Opsgenie, custom dashboards

```bash
export SYNTHETICAI_API_KEY="your-jwt-token"
python webhook_example.py --webhook-url https://hooks.example.com/alerts
```

### 3. Slack / Microsoft Teams Notifications

Both webhook scripts can target Slack Incoming Webhooks or Microsoft Teams
connectors directly. Point `--webhook-url` at the platform-provided URL:

```bash
# Slack
python webhook_example.py --webhook-url https://hooks.slack.com/services/T.../B.../xxx

# Microsoft Teams
python webhook_example.py --webhook-url https://outlook.office.com/webhook/...
```

### 4. Custom Scripts

Use the SyntheticAI REST API (see `sdk/api-client/README.md`) to build
any custom workflow. Common patterns include:

- Periodic threat-feed exports to CSV or STIX
- Automated ticket creation in Jira / ServiceNow
- Enrichment lookups against the SyntheticAI Data Lake

## Prerequisites

```bash
pip install requests
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `SYNTHETICAI_API_URL` | Platform base URL (default: `https://api.syntheticai.syntheticgamelabs.com/v1`) |
| `SYNTHETICAI_API_KEY` | JWT bearer token for authentication |

## Further Reading

- Full API specification: `api/api-spec.yaml`
- Telemetry schema: `sdk/telemetry-schema/`
