# SyntheticAI API Client Reference

> Copyright 2024-2026 Synthetic Game Labs. All rights reserved.

## Base URL

All endpoints are served under the SyntheticAI platform base URL:

```
https://api.syntheticai.syntheticgamelabs.com/v1
```

## Authentication

SyntheticAI uses **JWT Bearer tokens**. Obtain a token via the login
endpoint, then include it in every subsequent request.

### 1. Login

```bash
curl -X POST https://api.syntheticai.syntheticgamelabs.com/v1/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "analyst@example.com",
    "password": "your-password"
  }'
```

**Response** (200 OK):
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "refresh_token": "dGhpcyBpcyBhIHJlZnJl...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

Use the `access_token` in all subsequent requests:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

---

### 2. Submit Telemetry Batch

```bash
curl -X POST https://api.syntheticai.syntheticgamelabs.com/v1/api/telemetry/batch \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "events": [
      {
        "device_id": "agent-4a7b9c2e",
        "timestamp": "2026-03-28T14:32:07Z",
        "event_type": "process_start",
        "process_name": "cmd.exe",
        "threat_score": 0.12
      }
    ]
  }'
```

**Response** (202 Accepted):
```json
{ "batch_id": "b-98a1c3f7", "accepted": 1, "rejected": 0 }
```

---

### 3. List Threats

```bash
curl -X GET "https://api.syntheticai.syntheticgamelabs.com/v1/api/threats?severity=high&limit=25" \
  -H "Authorization: Bearer $TOKEN"
```

**Response** (200 OK):
```json
{
  "threats": [
    {
      "id": "thr-0012af",
      "severity": "high",
      "title": "Suspicious PowerShell execution",
      "mitre_technique": "T1059.001",
      "first_seen": "2026-03-27T09:14:00Z",
      "device_count": 3
    }
  ],
  "total": 1,
  "page": 1
}
```

---

### 4. Start Investigation

```bash
curl -X POST https://api.syntheticai.syntheticgamelabs.com/v1/api/investigate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "threat_id": "thr-0012af", "scope": "full" }'
```

**Response** (201 Created):
```json
{ "investigation_id": "inv-77b3", "status": "running" }
```

---

### 5. List Campaigns

```bash
curl -X GET https://api.syntheticai.syntheticgamelabs.com/v1/api/campaigns \
  -H "Authorization: Bearer $TOKEN"
```

**Response** (200 OK):
```json
{
  "campaigns": [
    {
      "id": "camp-0042",
      "name": "SCATTERED-VIPER",
      "threat_count": 14,
      "status": "active"
    }
  ]
}
```

---

## Error Handling

All error responses follow a consistent envelope:

```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Token expired or invalid."
  }
}
```

| HTTP Code | Meaning |
|-----------|---------|
| 400 | Bad request / validation failure |
| 401 | Missing or expired JWT |
| 403 | Insufficient permissions |
| 404 | Resource not found |
| 429 | Rate limit exceeded |
| 500 | Internal server error |

## Rate Limits

- **Authentication**: 10 requests / minute
- **Telemetry ingest**: 100 requests / minute (up to 500 events each)
- **Query endpoints**: 60 requests / minute

## Further Reading

- Telemetry event schema: `sdk/telemetry-schema/`
- Sample integrations: `sdk/sample-integrations/`
- Full OpenAPI specification: `api/api-spec.yaml`
