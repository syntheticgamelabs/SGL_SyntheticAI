"""
SGL SyntheticAI Python SDK
Copyright (c) 2024-2026 Synthetic Game Labs. MIT Licensed.
"""

import requests
from typing import Optional, Iterator, Dict, Any


class SyntheticAIClient:
    """Client for the SGL SyntheticAI REST API."""

    def __init__(self, base_url: str, token: Optional[str] = None):
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.session = requests.Session()
        if token:
            self.session.headers["Authorization"] = f"Bearer {token}"

    def login(self, username: str, password: str) -> Dict[str, Any]:
        """Authenticate and store JWT token."""
        resp = self.session.post(
            f"{self.base_url}/api/v1/auth/login",
            json={"username": username, "password": password},
        )
        resp.raise_for_status()
        data = resp.json()
        self.token = data["token"]
        self.session.headers["Authorization"] = f"Bearer {self.token}"
        return data

    def scan_file(self, file_path: str) -> Dict[str, Any]:
        """Upload and scan a file for threats."""
        with open(file_path, "rb") as f:
            resp = self.session.post(
                f"{self.base_url}/api/v1/scan/file",
                files={"file": f},
            )
        resp.raise_for_status()
        return resp.json()

    def get_threats(self) -> Dict[str, Any]:
        """Get current threat intelligence data."""
        resp = self.session.get(f"{self.base_url}/api/v1/threats")
        resp.raise_for_status()
        return resp.json()

    def get_server_metrics(self) -> Dict[str, Any]:
        """Get server performance metrics (admin only)."""
        resp = self.session.get(f"{self.base_url}/api/v1/admin/metrics")
        resp.raise_for_status()
        return resp.json()

    def get_llm_catalog(self) -> Dict[str, Any]:
        """Get available LLM models."""
        resp = self.session.get(f"{self.base_url}/api/v1/llm/catalog")
        resp.raise_for_status()
        return resp.json()

    def chat(self, message: str, username: str = "sdk-user") -> Iterator[str]:
        """Stream chat tokens from the AI assistant."""
        resp = self.session.post(
            f"{self.base_url}/api/v1/llm/chat",
            json={"message": message, "username": username},
            stream=True,
        )
        resp.raise_for_status()
        for line in resp.iter_lines(decode_unicode=True):
            if line and line.startswith("data: "):
                yield line[6:]

    def refresh_threats(self) -> Dict[str, Any]:
        """Trigger manual threat intelligence refresh (admin only)."""
        resp = self.session.post(f"{self.base_url}/api/v1/threats/refresh")
        resp.raise_for_status()
        return resp.json()


# --- Quick usage example ---
if __name__ == "__main__":
    client = SyntheticAIClient("http://localhost:5000")
    client.login("admin", "your-password")

    # Scan a file
    result = client.scan_file("/path/to/suspicious.exe")
    print(f"Verdict: {result['verdict']} (Score: {result['score']})")

    # Chat with AI
    for token in client.chat("What threats have you detected today?"):
        print(token, end="", flush=True)
    print()
