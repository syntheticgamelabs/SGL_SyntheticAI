# SGL SyntheticAI BetaV1.1.39

AI-powered cybersecurity platform by Synthetic Game Labs.

## Repository Structure

| Folder | Platform | Description |
|--------|----------|-------------|
| `WindowsServer/` | Windows x64 | Full server with LLM, API, website hosting |
| `WindowsClient/` | Windows x64 | Desktop WPF client application |
| `LinuxServer/` | Linux x64 | Headless server for Linux deployment |
| `LinuxClient/` | Linux x64 | CLI client for Linux |
| `AndroidMobile/` | Android 8.0+ | .NET MAUI mobile app |
| `Website/` | Web | React SPA served by the server |

## Each folder contains:
- `src/` - Complete buildable source code
- `build/` - Pre-compiled binaries for testing
- `README.md` - Build instructions
- Solution file (`.slnx`) - Open in Visual Studio / Rider / VS Code
- Build script (`build.bat` / `build.sh`)

## Tech Stack
- **Backend:** .NET 8.0, C#, Kestrel HTTP server
- **Desktop:** WPF (.NET 8.0-windows)
- **Mobile:** .NET MAUI (net9.0-android)
- **Website:** React 18, TypeScript, Vite, Tailwind CSS
- **AI/LLM:** LLamaSharp (llama.cpp wrapper)

## Version
- Server/Client: v1.1.39
- Mobile: v3.6.0
- Website: v1.1.39

## License
Copyright (c) 2024-2026 Synthetic Game Labs. See LICENSE.
