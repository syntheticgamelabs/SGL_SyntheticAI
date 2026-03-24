# API Reference — SGL SyntheticAI v1.1.46

Base URL: `http://<server>:5000/api/v1`

All authenticated endpoints require: `Authorization: Bearer <jwt_token>`

---

## Authentication

### POST /api/v1/auth/login

Authenticate and receive a JWT token.

**Request:**
```json
{
  "username": "string",
  "password": "string"
}
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "username": "string",
  "role": "admin|user",
  "expiresAt": "2026-03-25T00:00:00Z"
}
```

### POST /api/v1/auth/register

Register a new user account.

**Request:**
```json
{
  "username": "string",
  "email": "string",
  "password": "string"
}
```

**Response (201):**
```json
{
  "success": true,
  "username": "string"
}
```

---

## Scanning

### POST /api/v1/scan/file

Upload and scan a file for threats.

**Request:** `multipart/form-data` with file attachment

**Response (200):**
```json
{
  "fileName": "string",
  "sha256": "string",
  "verdict": "clean|suspicious|malicious",
  "score": 0.0,
  "engines": {
    "yara": { "matched": false, "rules": [] },
    "hash": { "matched": false },
    "heuristic": { "score": 0.0, "checks": [] }
  },
  "securityBrain": {
    "compositeScore": 0.0,
    "behavioral": 0.0,
    "graph": 0.0,
    "llmConfidence": 0.0,
    "threatIntel": 0.0
  }
}
```

### GET /api/v1/scan/status/{scanId}

Get the status of an ongoing scan.

### GET /api/v1/scan/history

Get scan history with pagination.

**Query Parameters:**
- `page` (int, default: 1)
- `pageSize` (int, default: 20)

---

## Threat Intelligence

### GET /api/v1/threats

Get current threat intelligence data.

**Response (200):**
```json
{
  "signatures": [
    {
      "hash": "string",
      "type": "string",
      "source": "string",
      "firstSeen": "2026-03-24T00:00:00Z"
    }
  ],
  "totalSignatures": 0,
  "lastUpdated": "2026-03-24T00:00:00Z"
}
```

### POST /api/v1/threats/refresh

Trigger a manual refresh of threat intelligence feeds.

**Authentication:** Admin only

---

## LLM / Chat

### POST /api/v1/llm/chat

Send a message to the AI assistant.

**Request:**
```json
{
  "message": "string",
  "username": "string",
  "systemPrompt": "string (optional)"
}
```

**Response (200):** Server-Sent Events (SSE) stream of tokens.

### GET /api/v1/llm/catalog

Get available LLM models.

**Response (200):**
```json
{
  "models": [
    {
      "id": "string",
      "name": "string",
      "fileName": "string",
      "sizeBytes": 0,
      "quantization": "Q4_K_M",
      "role": "MainEngine|AiChat|SecurityAI"
    }
  ]
}
```

### POST /api/v1/llm/mount

Mount a model into an LLM slot.

**Request:**
```json
{
  "slotIndex": 0,
  "modelId": "string",
  "role": "string",
  "gpuLayers": 0,
  "contextSize": 2048
}
```

### POST /api/v1/llm/unmount

Unmount a model from a slot.

**Request:**
```json
{
  "slotIndex": 0
}
```

---

## Admin

### GET /api/v1/admin/metrics

Get server performance metrics.

**Authentication:** Admin only

**Response (200):**
```json
{
  "cpuUsage": 0.0,
  "memoryUsageMb": 0,
  "diskUsagePercent": 0.0,
  "uptime": "string",
  "activeConnections": 0,
  "serverVersion": "1.1.46"
}
```

### GET /api/v1/admin/metrics/website

Get website visitor metrics.

**Authentication:** Admin only

**Response (200):**
```json
{
  "totalVisitors": 0,
  "activeVisitors": 0,
  "avgDuration": "string",
  "totalDownloads": 0,
  "downloadsByPlatform": {
    "windows": 0,
    "linux": 0,
    "android": 0
  },
  "topCountries": {
    "US": 0,
    "UK": 0
  }
}
```

### GET /api/v1/admin/users

List all registered users.

**Authentication:** Admin only

### POST /api/v1/admin/users/{userId}/ban

Ban a user account.

### POST /api/v1/admin/users/{userId}/promote

Promote a user to admin role.

---

## Client Sync

### POST /api/v1/client/register

Register a client installation.

### GET /api/v1/client/settings

Get synchronized settings for the client.

### GET /api/v1/client/signatures/version

Get the current signature database version.

### GET /api/v1/client/download

Download the Windows client installer.

### GET /api/v1/linux-client/download

Download the Linux client installer.

### GET /api/v1/mobile/download

Download the Android APK.

---

## WebSocket

### ws://<server>:5000/ws

Bidirectional real-time communication channel.

**Message Types:**
```json
{ "type": "chat", "data": { "message": "string" } }
{ "type": "scan.result", "data": { "verdict": "string" } }
{ "type": "notification", "data": { "title": "string", "body": "string" } }
{ "type": "threat.alert", "data": { "score": 0.0, "details": {} } }
```

---

## Error Responses

All endpoints return standard error format:

```json
{
  "error": "string",
  "message": "string",
  "statusCode": 400
}
```

| Code | Meaning |
|------|---------|
| 400 | Bad request — invalid parameters |
| 401 | Unauthorized — invalid or missing JWT |
| 403 | Forbidden — insufficient permissions |
| 404 | Not found |
| 429 | Rate limited (planned) |
| 500 | Internal server error |

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
