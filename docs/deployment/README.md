# Deployment Guide — SGL SyntheticAI

## System Requirements

### Windows Server
| Resource | Minimum | Recommended |
|----------|---------|-------------|
| OS | Windows 10/11, Server 2019+ | Windows Server 2022 |
| CPU | 4 cores | 8+ cores |
| RAM | 8 GB | 32 GB (16 GB minimum for LLMs) |
| Disk | 100 GB | 500 GB SSD |
| GPU | Not required | NVIDIA 8GB+ VRAM (for LLM acceleration) |
| .NET | 9.0 Runtime (bundled) | — |

### Windows Client
| Resource | Minimum | Recommended |
|----------|---------|-------------|
| OS | Windows 10 (64-bit) | Windows 11 |
| CPU | 2 cores | 4+ cores |
| RAM | 4 GB | 8 GB |
| Disk | 500 MB | 2 GB |

### Linux Server
| Resource | Minimum | Recommended |
|----------|---------|-------------|
| OS | Ubuntu 22.04, Debian 12 | Ubuntu 24.04 LTS |
| CPU | 4 cores | 8+ cores |
| RAM | 8 GB | 32 GB |
| Disk | 100 GB | 500 GB SSD |

### Android
| Resource | Minimum |
|----------|---------|
| OS | Android 8.0 (API 26) |
| RAM | 3 GB |
| Storage | 200 MB |

---

## Installation

### Windows Server (Installer)

1. Run `SyntheticAI_ServerSetup_v1.1.46.exe` as Administrator
2. Follow the installation wizard
3. The server auto-starts on port 5000
4. **Immediately change the default admin password**

```
Default URL: http://localhost:5000
Default Admin: admin / changeme (forced change on first login)
```

### Windows Client (Installer)

1. Run `SyntheticAI_ClientSetup_v1.1.46.exe`
2. Configure server connection on first launch
3. Login with your credentials

### Linux Server

```bash
chmod +x install.sh
sudo ./install.sh

# Start the server
sudo systemctl start syntheticai-server

# Enable on boot
sudo systemctl enable syntheticai-server
```

### Linux Client

```bash
chmod +x install.sh
./install.sh

# Run
syntheticai-client --server http://your-server:5000
```

---

## TLS Configuration

SyntheticAI's HTTP server does not include built-in TLS. Use one of these approaches:

### Option 1: Cloudflare Tunnel (Recommended)

The admin panel includes built-in Cloudflare Tunnel management:

1. Navigate to Admin Panel → Network/Tunnel Management
2. Click "Install Cloudflare Tunnel"
3. Enter your Cloudflare tunnel token
4. Click "Start Tunnel"

### Option 2: nginx Reverse Proxy

```nginx
server {
    listen 443 ssl http2;
    server_name your-domain.com;

    ssl_certificate /etc/ssl/certs/your-cert.pem;
    ssl_certificate_key /etc/ssl/private/your-key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## LLM Model Setup

### Supported Models

SyntheticAI supports any GGUF-format model compatible with llama.cpp:

| Model | Size | Recommended Use |
|-------|------|----------------|
| Qwen2.5-7B-Q4_K_M | ~4.3 GB | Main engine, general security AI |
| Llama-3-8B-Q4_K_M | ~4.7 GB | AI chat assistant |
| Mistral-7B-Q4_K_M | ~4.1 GB | Security-specific analysis |

### Model Placement

Place GGUF files in the `LLM/` directory:
```
<install-dir>/LLM/
  ├── qwen2.5-7b-instruct-q4_k_m.gguf
  ├── llama-3-8b-instruct-q4_k_m.gguf
  └── mistral-7b-instruct-q4_k_m.gguf
```

Models are automatically detected and can be mounted via the Admin Panel → Multi-LLM tab.

### GPU Offloading

For NVIDIA GPUs with CUDA support, configure GPU layers in the Multi-LLM management tab. More layers offloaded = faster inference.

---

## Firewall Rules

### Required Ports (Server)

| Port | Protocol | Purpose |
|------|----------|---------|
| 5000 | TCP | HTTP API + WebSocket |
| 7860 | TCP | Stable Diffusion WebUI (local only) |
| 41234 | UDP | Gossip Protocol (swarm mode) |

---

## Backup & Recovery

### Data to Back Up

```
data/
  ├── settings.json          # Application settings
  ├── server_settings.json   # Server configuration
  ├── jwt_secret.key         # JWT signing key (critical)
  ├── threat_signatures.json # Threat signature database
  ├── campaigns.json         # Detected campaigns
  ├── yara_rules/            # YARA detection rules
  ├── models/                # Trained anomaly models
  └── llm_memory_*.json      # Per-user chat memory
```

### Backup Command

```bash
# Linux
tar -czf syntheticai-backup-$(date +%Y%m%d).tar.gz data/

# Windows (PowerShell)
Compress-Archive -Path data -DestinationPath "syntheticai-backup-$(Get-Date -Format yyyyMMdd).zip"
```

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
