#!/usr/bin/env bash
# SGL SyntheticAI - Linux Server Installer v1.1.46
# Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.

set -e

INSTALL_DIR="/opt/sgl-syntheticai-server"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "============================================"
echo "  SGL SyntheticAI - Linux Server Installer"
echo "  Version 1.1.46"
echo "============================================"
echo ""

# Check for root privileges
if [ "$(id -u)" -ne 0 ]; then
    echo "This installer requires root privileges."
    echo "Please run with sudo: sudo bash $0"
    exit 1
fi

echo "[1/7] Creating installation directory..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$INSTALL_DIR/data"
mkdir -p "$INSTALL_DIR/data/website"
mkdir -p "$INSTALL_DIR/data/client"
mkdir -p "$INSTALL_DIR/data/mobile"
mkdir -p "$INSTALL_DIR/data/linux-client"
mkdir -p "$INSTALL_DIR/data/copilot"
mkdir -p "$INSTALL_DIR/data/developers"
mkdir -p "$INSTALL_DIR/data/faq"
mkdir -p "$INSTALL_DIR/data/scan_logs"
mkdir -p "$INSTALL_DIR/data/chat_history"
mkdir -p "$INSTALL_DIR/data/dm_store"
mkdir -p "$INSTALL_DIR/LLM"
mkdir -p "$INSTALL_DIR/LLM_Server"
mkdir -p "$INSTALL_DIR/logs"

echo "[2/7] Copying server application files..."
if [ -d "$SCRIPT_DIR/publish" ]; then
    cp -r "$SCRIPT_DIR/publish/"* "$INSTALL_DIR/"
elif [ -f "$SCRIPT_DIR/JudgeDredd.ServerHost" ]; then
    cp -r "$SCRIPT_DIR/"* "$INSTALL_DIR/"
else
    echo "ERROR: Could not find application files."
    echo "Expected either a 'publish' directory or application binaries."
    exit 1
fi

echo "[3/7] Setting permissions..."
chmod +x "$INSTALL_DIR/JudgeDredd.ServerHost" 2>/dev/null || true
find "$INSTALL_DIR" -name "*.so" -exec chmod +x {} \; 2>/dev/null || true

echo "[4/7] Creating server settings..."
if [ ! -f "$INSTALL_DIR/data/server_settings.json" ]; then
    cat > "$INSTALL_DIR/data/server_settings.json" << 'SETTINGS'
{
  "DeploymentMode": "Server",
  "ServerPort": 5000,
  "EnableApi": true,
  "EnableWebsite": true,
  "EnableWebSocket": true,
  "JwtSecret": "",
  "AdminPassword": "changeme"
}
SETTINGS
    echo "  Default server settings created. CHANGE THE ADMIN PASSWORD!"
fi

echo "[5/7] Creating systemd service..."
cat > /etc/systemd/system/sgl-syntheticai.service << SYSTEMD
[Unit]
Description=SGL SyntheticAI Server
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/JudgeDredd.ServerHost
Restart=on-failure
RestartSec=10
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
SYSTEMD
systemctl daemon-reload

echo "[6/7] Configuring firewall (if ufw is active)..."
if command -v ufw &>/dev/null && ufw status | grep -q "active"; then
    ufw allow 5000/tcp comment "SGL SyntheticAI Server API + Website"
    echo "  Port 5000 opened in UFW."
else
    echo "  UFW not active. Ensure port 5000 is open in your firewall."
fi

echo "[7/7] Creating uninstall script..."
cat > "$INSTALL_DIR/uninstall.sh" << 'UNINSTALL'
#!/usr/bin/env bash
echo "Uninstalling SGL SyntheticAI Server..."
systemctl stop sgl-syntheticai 2>/dev/null || true
systemctl disable sgl-syntheticai 2>/dev/null || true
rm -f /etc/systemd/system/sgl-syntheticai.service
systemctl daemon-reload
rm -rf /opt/sgl-syntheticai-server
echo "SGL SyntheticAI Server has been uninstalled."
echo "Note: Port 5000 firewall rule was NOT removed."
UNINSTALL
chmod +x "$INSTALL_DIR/uninstall.sh"

echo ""
echo "============================================"
echo "  Installation complete!"
echo ""
echo "  Start:   sudo systemctl start sgl-syntheticai"
echo "  Enable:  sudo systemctl enable sgl-syntheticai"
echo "  Status:  sudo systemctl status sgl-syntheticai"
echo "  Logs:    journalctl -u sgl-syntheticai -f"
echo ""
echo "  API + Website: http://localhost:5000"
echo "  Uninstall: sudo bash $INSTALL_DIR/uninstall.sh"
echo "============================================"
