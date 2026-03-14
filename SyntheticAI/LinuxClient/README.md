# SGL SyntheticAI - Linux Client v1.1.39

AI-powered cybersecurity CLI client for Linux by Synthetic Game Labs.

## Quick Start (Pre-built)
```bash
chmod +x build/JudgeDredd.LinuxClient
./build/JudgeDredd.LinuxClient
```

## Build from Source
**Requirements:** .NET 8.0 SDK, Linux x64

```bash
dotnet build SGL-AI-LinuxClient.slnx -c Release
dotnet publish src/SGL.JudgeDredd.LinuxClient -c Release -r linux-x64 --self-contained
```

Or run `./build.sh` for a one-click build.

## License
See LICENSE file. Copyright (c) 2024-2026 Synthetic Game Labs.
