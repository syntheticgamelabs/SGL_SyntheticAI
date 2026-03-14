# SGL SyntheticAI - Android Mobile v3.6.0

AI-powered cybersecurity mobile app for Android by Synthetic Game Labs.

## Quick Start (Pre-built)
Install `build/SGL_SyntheticAI_Mobile_v3.6.0.apk` on your Android device.

## Build from Source
**Requirements:** .NET 9.0 SDK with Android workload

```bash
dotnet workload install android
dotnet build SGL-AI-Mobile.slnx -c Release
dotnet publish src/SGL.JudgeDredd.Mobile -c Release -f net9.0-android
```

Or run `build.bat` for a one-click build.

## License
See LICENSE file. Copyright (c) 2024-2026 Synthetic Game Labs.
