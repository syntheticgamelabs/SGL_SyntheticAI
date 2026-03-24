/**
 * SGL SyntheticAI TypeScript SDK
 * Copyright (c) 2024-2026 Synthetic Game Labs. MIT Licensed.
 */

export interface AuthResponse {
  token: string;
  username: string;
  role: "admin" | "user";
}

export interface ScanResult {
  fileName: string;
  sha256: string;
  verdict: "clean" | "suspicious" | "malicious";
  score: number;
  engines: {
    yara: { matched: boolean; rules: string[] };
    hash: { matched: boolean };
    heuristic: { score: number; checks: string[] };
  };
}

export interface ServerMetrics {
  cpuUsage: number;
  memoryUsageMb: number;
  diskUsagePercent: number;
  uptime: string;
  activeConnections: number;
  serverVersion: string;
}

export interface ThreatIntelResponse {
  totalSignatures: number;
  lastUpdated: string;
  signatures: Array<{
    hash: string;
    type: string;
    source: string;
    firstSeen: string;
  }>;
}

export interface LlmModel {
  id: string;
  name: string;
  sizeBytes: number;
  role?: string;
}

export class SyntheticAIClient {
  private baseUrl: string;
  private token?: string;

  constructor(baseUrl: string, token?: string) {
    this.baseUrl = baseUrl.replace(/\/+$/, "");
    this.token = token;
  }

  private get headers(): Record<string, string> {
    const h: Record<string, string> = { "Content-Type": "application/json" };
    if (this.token) h["Authorization"] = `Bearer ${this.token}`;
    return h;
  }

  /** Authenticate and store JWT token. */
  async login(username: string, password: string): Promise<AuthResponse> {
    const resp = await fetch(`${this.baseUrl}/api/v1/auth/login`, {
      method: "POST",
      headers: this.headers,
      body: JSON.stringify({ username, password }),
    });
    if (!resp.ok) throw new Error(`Login failed: ${resp.status}`);
    const data: AuthResponse = await resp.json();
    this.token = data.token;
    return data;
  }

  /** Upload and scan a file for threats. */
  async scanFile(file: File | Blob, fileName?: string): Promise<ScanResult> {
    const form = new FormData();
    form.append("file", file, fileName);

    const headers: Record<string, string> = {};
    if (this.token) headers["Authorization"] = `Bearer ${this.token}`;

    const resp = await fetch(`${this.baseUrl}/api/v1/scan/file`, {
      method: "POST",
      headers,
      body: form,
    });
    if (!resp.ok) throw new Error(`Scan failed: ${resp.status}`);
    return resp.json();
  }

  /** Get current threat intelligence data. */
  async getThreats(): Promise<ThreatIntelResponse> {
    const resp = await fetch(`${this.baseUrl}/api/v1/threats`, {
      headers: this.headers,
    });
    if (!resp.ok) throw new Error(`Failed: ${resp.status}`);
    return resp.json();
  }

  /** Get server metrics (admin only). */
  async getServerMetrics(): Promise<ServerMetrics> {
    const resp = await fetch(`${this.baseUrl}/api/v1/admin/metrics`, {
      headers: this.headers,
    });
    if (!resp.ok) throw new Error(`Failed: ${resp.status}`);
    return resp.json();
  }

  /** Get available LLM models. */
  async getLlmCatalog(): Promise<{ models: LlmModel[] }> {
    const resp = await fetch(`${this.baseUrl}/api/v1/llm/catalog`, {
      headers: this.headers,
    });
    if (!resp.ok) throw new Error(`Failed: ${resp.status}`);
    return resp.json();
  }

  /** Stream chat tokens from the AI assistant. */
  async *chat(
    message: string,
    username = "sdk-user"
  ): AsyncGenerator<string, void, void> {
    const resp = await fetch(`${this.baseUrl}/api/v1/llm/chat`, {
      method: "POST",
      headers: this.headers,
      body: JSON.stringify({ message, username }),
    });
    if (!resp.ok) throw new Error(`Chat failed: ${resp.status}`);
    if (!resp.body) return;

    const reader = resp.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split("\n");
      buffer = lines.pop() || "";
      for (const line of lines) {
        if (line.startsWith("data: ")) {
          yield line.slice(6);
        }
      }
    }
  }
}
