# Contributing to SGL SyntheticAI

Thank you for your interest in contributing to SGL SyntheticAI.

## Contribution Model

SGL SyntheticAI uses a **dual-license model**:

- **Core Platform** — Proprietary. Contributions to the core security engine, AI/ML modules, and server infrastructure require a Contributor License Agreement (CLA).
- **SDK & Client Libraries** — MIT Licensed. Community contributions are welcome and encouraged.

## How to Contribute

### SDK & Client Libraries (Open)

The SDK client libraries in `sdk/` are open for community contributions:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes in the `sdk/` directory
4. Write tests for your changes
5. Submit a pull request with a clear description

#### SDK Contribution Guidelines

- Follow the existing code style for each language (C#, Python, TypeScript)
- Include XML doc comments (C#), docstrings (Python), or JSDoc (TypeScript)
- Add unit tests for all new public methods
- Update the relevant README in the SDK subdirectory
- Do not introduce new external dependencies without discussion

### Bug Reports

Found a bug? Please open an issue with:

1. **Environment** — OS, .NET version, SyntheticAI version
2. **Steps to reproduce** — Detailed step-by-step instructions
3. **Expected behavior** — What should happen
4. **Actual behavior** — What actually happens
5. **Logs** — Relevant log output (redact sensitive information)

### Security Vulnerabilities

**Do NOT open public issues for security vulnerabilities.**

See [SECURITY.md](SECURITY.md) for responsible disclosure instructions.

### Feature Requests

Feature suggestions are welcome via email: security@syntheticgamelabs.com

Include:
- Use case description
- Expected behavior
- Why existing features don't meet the need

## Code of Conduct

### Our Standards

- Be respectful and inclusive
- Focus on constructive feedback
- Accept differing viewpoints gracefully
- Prioritize the community's best interest

### Unacceptable Behavior

- Harassment, trolling, or derogatory comments
- Publishing others' private information
- Conduct that could be considered inappropriate in a professional setting

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or JetBrains Rider
- Node.js 18+ (for website development)
- Git

### Building the SDK

```bash
# .NET SDK
cd sdk/dotnet
dotnet build
dotnet test

# Python SDK
cd sdk/python
pip install -e ".[dev]"
pytest

# TypeScript SDK
cd sdk/typescript
npm install
npm test
```

## License

By contributing to the SDK, you agree that your contributions will be licensed under the MIT License. By contributing to the core platform (with CLA), you assign copyright to Synthetic Game Labs.

---

<p align="center">
  <sub>Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.</sub>
</p>
