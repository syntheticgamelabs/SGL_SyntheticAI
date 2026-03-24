# SGL SyntheticAI TypeScript SDK

Official TypeScript/JavaScript client library for the SGL SyntheticAI REST API.

## Installation

```bash
npm install syntheticai-sdk  # Coming soon to npm
```

Or copy directly:
```bash
cp syntheticai-client.ts your-project/lib/
```

## Quick Start

```typescript
import { SyntheticAIClient } from "./syntheticai-client";

const client = new SyntheticAIClient("http://your-server:5000");

// Authenticate
await client.login("admin", "your-password");

// Scan a file (browser)
const fileInput = document.querySelector<HTMLInputElement>("#file");
const file = fileInput?.files?.[0];
if (file) {
  const result = await client.scanFile(file);
  console.log(`Verdict: ${result.verdict} (Score: ${result.score})`);
}

// Get threat intel
const threats = await client.getThreats();
console.log(`Signatures: ${threats.totalSignatures}`);

// Stream AI chat
for await (const token of client.chat("Analyze recent threats")) {
  process.stdout.write(token);
}

// Server metrics
const metrics = await client.getServerMetrics();
console.log(`CPU: ${metrics.cpuUsage}%, Connections: ${metrics.activeConnections}`);
```

## Requirements

- TypeScript 5.0+ or modern JavaScript (ES2018+)
- `fetch` API (browsers, Node.js 18+, or polyfill)

## License

MIT License — see [../LICENSE](../LICENSE)
