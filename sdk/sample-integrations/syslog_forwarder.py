#!/usr/bin/env python3
"""SyntheticAI Syslog Forwarder

Polls the SyntheticAI /api/threats endpoint and forwards high-severity
alerts to a remote syslog server using UDP (RFC 5424).

Copyright 2024-2026 Synthetic Game Labs. All rights reserved.
"""

import argparse
import json
import logging
import logging.handlers
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


def forward_to_syslog(threats: list, syslog_logger: logging.Logger) -> int:
    """Send each threat as a syslog message. Returns count forwarded."""
    count = 0
    for threat in threats:
        msg = (
            f"SyntheticAI alert_id={threat.get('id')} "
            f"severity={threat.get('severity')} "
            f"title=\"{threat.get('title')}\" "
            f"mitre={threat.get('mitre_technique', 'N/A')} "
            f"devices={threat.get('device_count', 0)}"
        )
        syslog_logger.warning(msg)
        count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description="Forward SyntheticAI alerts to syslog")
    parser.add_argument("--syslog-host", default="127.0.0.1", help="Syslog server address")
    parser.add_argument("--syslog-port", type=int, default=514, help="Syslog server port")
    args = parser.parse_args()

    if not API_KEY:
        sys.exit("Error: set SYNTHETICAI_API_KEY environment variable.")

    syslog_handler = logging.handlers.SysLogHandler(
        address=(args.syslog_host, args.syslog_port),
    )
    syslog_logger = logging.getLogger("syntheticai.syslog")
    syslog_logger.addHandler(syslog_handler)
    syslog_logger.setLevel(logging.WARNING)

    print(f"Forwarding SyntheticAI alerts to {args.syslog_host}:{args.syslog_port}")

    while True:
        try:
            threats = fetch_threats()
            if threats:
                n = forward_to_syslog(threats, syslog_logger)
                print(f"Forwarded {n} alert(s)")
        except requests.RequestException as exc:
            print(f"API error: {exc}", file=sys.stderr)
        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    main()
