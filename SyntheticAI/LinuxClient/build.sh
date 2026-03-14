#!/bin/bash
echo "Building SGL SyntheticAI Linux Client v1.1.39..."
dotnet publish src/SGL.JudgeDredd.LinuxClient -c Release -r linux-x64 --self-contained -o build/
echo "Build complete. Output: build/"
