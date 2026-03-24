#!/usr/bin/env bash
# SGL SyntheticAI - Linux Client Installer v1.1.46
# Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.

set -e

INSTALL_DIR="/opt/sgl-syntheticai"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "============================================"
echo "  SGL SyntheticAI - Linux Client Installer"
echo "  Version 1.1.46"
echo "============================================"
echo ""

# Check for root privileges
if [ "$(id -u)" -ne 0 ]; then
    echo "This installer requires root privileges."
    echo "Please run with sudo: sudo bash $0"
    exit 1
fi

echo "[1/5] Creating installation directory..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$INSTALL_DIR/data"
mkdir -p "$INSTALL_DIR/data/faq"
mkdir -p "$INSTALL_DIR/data/scan_logs"
mkdir -p "$INSTALL_DIR/data/chat_history"
mkdir -p "$INSTALL_DIR/logs"

echo "[2/5] Copying application files..."
if [ -d "$SCRIPT_DIR/publish" ]; then
    cp -r "$SCRIPT_DIR/publish/"* "$INSTALL_DIR/"
elif [ -f "$SCRIPT_DIR/SGL.JudgeDredd.LinuxClient" ]; then
    cp -r "$SCRIPT_DIR/"* "$INSTALL_DIR/"
else
    echo "ERROR: Could not find application files."
    echo "Expected either a 'publish' directory or application binaries."
    exit 1
fi

echo "[3/5] Setting permissions..."
chmod +x "$INSTALL_DIR/SGL.JudgeDredd.LinuxClient" 2>/dev/null || true
find "$INSTALL_DIR" -name "*.so" -exec chmod +x {} \; 2>/dev/null || true

echo "[4/5] Creating symlink and desktop entry..."
ln -sf "$INSTALL_DIR/SGL.JudgeDredd.LinuxClient" /usr/local/bin/syntheticai

# Create desktop entry
cat > /usr/share/applications/sgl-syntheticai.desktop << 'EOF'
[Desktop Entry]
Name=SGL SyntheticAI
Comment=AI-Powered Security Platform
Exec=/opt/sgl-syntheticai/SGL.JudgeDredd.LinuxClient
Terminal=true
Type=Application
Categories=Security;System;
StartupNotify=true
EOF

echo "[5/5] Creating uninstall script..."
cat > "$INSTALL_DIR/uninstall.sh" << 'UNINSTALL'
#!/usr/bin/env bash
echo "Uninstalling SGL SyntheticAI..."
rm -f /usr/local/bin/syntheticai
rm -f /usr/share/applications/sgl-syntheticai.desktop
rm -rf /opt/sgl-syntheticai
echo "SGL SyntheticAI has been uninstalled."
UNINSTALL
chmod +x "$INSTALL_DIR/uninstall.sh"

echo ""
echo "============================================"
echo "  Installation complete!"
echo "  Run 'syntheticai' from terminal to start."
echo "  Uninstall: sudo bash $INSTALL_DIR/uninstall.sh"
echo "============================================"
