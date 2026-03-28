# SyntheticAI Telemetry Event Schema

> Copyright 2024-2026 Synthetic Game Labs. All rights reserved.

## Overview

This directory contains the canonical JSON Schema definition for telemetry
events processed by the SyntheticAI threat-intelligence platform. Every
endpoint agent, SIEM forwarder, and third-party integration that submits
data to SyntheticAI **must** conform to this schema.

## Schema File

| File | Version | Format |
|------|---------|--------|
| `telemetry_event.schema.json` | 1.0.0 | JSON Schema Draft-07 |

## Required Fields

Every event **must** include at minimum:

- `device_id` -- unique agent or device identifier
- `timestamp` -- ISO 8601 datetime of observation
- `event_type` -- one of the supported event categories

All other fields are optional and should be populated when the data is
available for the given event type.

## Supported Event Types

| Event Type | Description |
|---|---|
| `process_start` | A new process was created |
| `process_end` | A process terminated |
| `file_create` | A new file was written to disk |
| `file_modify` | An existing file was modified |
| `file_delete` | A file was removed |
| `network_connect` | An outbound network connection was established |
| `network_dns` | A DNS query was performed |
| `registry_modify` | A Windows registry key was written |

## Quick Example

```json
{
  "device_id": "agent-4a7b9c2e-df01-4e3a-b8c7-1a2b3c4d5e6f",
  "timestamp": "2026-03-28T14:32:07.042Z",
  "event_type": "process_start",
  "process_name": "powershell.exe",
  "parent_process": "explorer.exe",
  "command_line": "powershell.exe -NoProfile -Command Get-Process",
  "user_name": "CORP\\analyst",
  "threat_score": 0.23,
  "mitre_tactic": "TA0002",
  "mitre_technique": "T1059.001"
}
```

## Validation

You can validate events locally with any JSON Schema Draft-07 library:

```bash
# Python (jsonschema)
pip install jsonschema
python -c "
import json, jsonschema
schema = json.load(open('telemetry_event.schema.json'))
event  = json.load(open('my_event.json'))
jsonschema.validate(event, schema)
print('Valid')
"
```

## Integration Notes

- Submit validated events via `POST /api/telemetry/batch` (see `sdk/api-client/`).
- Maximum batch size: **500 events** per request.
- Events older than 72 hours may be deprioritized during ingestion.
- The `threat_score` field is populated server-side if omitted; pre-scored
  events from trusted agents will be accepted as-is.
