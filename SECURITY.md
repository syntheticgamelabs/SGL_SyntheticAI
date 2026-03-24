# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.1.46  | Current release |
| 1.1.45  | Security patches only |
| < 1.1.45 | Not supported |

## Reporting a Vulnerability

We take the security of SGL SyntheticAI seriously. If you believe you have found a security vulnerability, we encourage you to report it responsibly.

### How to Report

**Email:** security@syntheticgamelabs.com

**Please include:**
1. Description of the vulnerability
2. Steps to reproduce the issue
3. Potential impact assessment
4. Any suggested remediation (optional)

### Response Timeline

| Stage | Timeline |
|-------|----------|
| Acknowledgment | Within 48 hours |
| Initial Assessment | Within 5 business days |
| Status Update | Within 10 business days |
| Resolution Target | Within 30 days for critical issues |

### What to Expect

- A confirmation email acknowledging receipt of your report
- Regular updates on the progress of resolving the vulnerability
- Credit in our security advisories (if desired) once the issue is resolved
- We will NOT take legal action against security researchers acting in good faith

### Responsible Disclosure Guidelines

- **Do** give us reasonable time to fix the issue before public disclosure
- **Do** make a good faith effort to avoid privacy violations, data destruction, and service disruption
- **Do not** access or modify other users' data
- **Do not** perform actions that could negatively impact other users
- **Do not** use automated scanning tools against production infrastructure without permission

---

## Security Architecture Overview

### Authentication & Authorization
- **JWT tokens** with HMAC-SHA256 signing
- **bcrypt** password hashing with configurable work factor
- **Role-based access control** (Admin, User)
- **Forced password change** on default credentials
- **Singleton key management** — single signing key across all services

### Data Protection
- **Quarantine vault encryption** for isolated malware samples
- **JWT secret file protection** — Hidden attribute + Windows ACL restriction (current user only)
- **No plaintext credential storage** — passwords never logged or transmitted in cleartext
- **Per-user chat memory isolation** — separate memory files per authenticated user

### Network Security
- **WebSocket authentication** — JWT-validated connections
- **Cloudflare Tunnel support** — TLS termination via reverse proxy
- **Gossip protocol reputation** — weighted trust scoring prevents rogue node injection
- **Threat feed validation** — IOC deduplication and integrity checking

### AI/ML Security
- **Model validation pipeline** — new models rejected if false-positive rate > 5%
- **Isolation Forest anomaly scoring** — statistical outlier detection resistant to evasion
- **YARA rule false-positive testing** — rules validated against clean file corpus before deployment
- **LLM prompt hardening** — system prompt injection protection

---

## Known Security Considerations

| ID | Item | Severity | Status | Mitigation |
|----|------|----------|--------|------------|
| SEC-001 | HTTP API (no built-in TLS) | Medium | By Design | Deploy behind Cloudflare Tunnel or nginx with TLS 1.3 |
| SEC-002 | No API rate limiting | Medium | Planned v1.1.47 | Middleware rate limiting in development |
| SEC-003 | GGUF model integrity | Low | Planned v1.1.47 | SHA-256 checksum verification on model load |

### Deployment Recommendations

1. **Always deploy the server behind a TLS-terminating reverse proxy** (nginx, Cloudflare Tunnel, or similar)
2. **Change the default admin password immediately** after installation
3. **Restrict network access** to the API server using firewall rules
4. **Enable Windows Firewall rules** via the built-in firewall management interface
5. **Review `data/registrations.log`** — pre-v1.1.46 installations may contain sensitive data; delete old log files
6. **Use strong JWT secrets** — the system auto-generates cryptographically random keys, but ensure the key file has restricted permissions

---

## Security Changelog

### v1.1.46 (2026-03-24)
- **FIXED:** Plaintext password logging removed from all log files and email notifications
- **FIXED:** JWT secret key file now has Hidden attribute and Windows ACL restricted to current user
- **FIXED:** Default admin password triggers forced change requirement and critical warning

### v1.1.45 (2026-03-23)
- **FIXED:** JWT signing key mismatch (singleton key management)
- **FIXED:** Race condition in threat signature persistence

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
