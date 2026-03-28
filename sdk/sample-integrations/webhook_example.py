#!/usr/bin/env python3
"""SyntheticAI Webhook Alert Forwarder

Polls the SyntheticAI /api/threats endpoint and pushes high-severity
alerts to a configurable webhook URL as JSON POST requests.

Copyright 2024-2026 Synthetic Game Labs. All rights reserved.
"""

import argparse
import json
import os
import sys
import time

import requests

API_URL = os.getenv("SYNTHETICAI_API_URL", "https://api.syntheticai.syntheticgamelabs.com/v1")
API_KEY = os.getenv("SYNTHETICAI_API_KEY", "")

POLL_INTERVAL_SECONDS = 30


def fetch_threats(min_severity: str = "high") -> list:
    """Retrieve recent threats from SyntheticAI."""
    headers = {"Authorization": f"Bearer {API_KEY}"}
    resp = requests.get(
        f"{API_URL}/api/threats",
        headers=headers,
        params={"severity": min_severity, "limit": 50},
        timeout=15,
    )
    resp.raise_for_status()
    return resp.json().get("threats", [])


def send_webhook(threat: dict, webhook_url: str) -> None:
    """POST a single alert payload to the webhook endpoint."""
    payload = {
        "source": "SyntheticAI",
        "alert_id": threat.get("id"),
        "severity": threat.get("severity"),
        "title": threat.get("title"),
        "mitre_technique": threat.get("mitre_technique"),
        "device_count": threat.get("device_count", 0),
        "first_seen": threat.get("first_seen"),
    }
    requests.post(webhook_url, json=payload, timeout=10).raise_for_status()


def main() -> None:
    parser = argparse.ArgumentParser(description="Forward SyntheticAI alerts to a webhook")
    parser.add_argument("--webhook-url", required=True, help="Destination webhook URL")
    args = parser.parse_args()

    if not API_KEY:
        sys.exit("Error: set SYNTHETICAI_API_KEY environment variable.")

    print(f"Forwarding SyntheticAI alerts to {args.webhook_url}")

    while True:
        try:
            for threat in fetch_threats():
                send_webhook(threat, args.webhook_url)
                print(f"Sent alert {threat.get('id')}")
        except requests.RequestException as exc:
            print(f"Error: {exc}", file=sys.stderr)
        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    main()
