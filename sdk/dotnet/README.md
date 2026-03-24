# SGL SyntheticAI .NET SDK

Official .NET client library for the SGL SyntheticAI REST API.

## Installation

Add the source file to your project, or reference via NuGet (coming soon):

```bash
dotnet add package SyntheticAI.Sdk
```

## Quick Start

```csharp
using SyntheticAI.Sdk;

// Connect and authenticate
using var client = new SyntheticAIClient("http://your-server:5000");
await client.LoginAsync("admin", "your-password");

// Scan a file
var result = await client.ScanFileAsync(@"C:\suspicious.exe");
Console.WriteLine($"Verdict: {result.Verdict} (Score: {result.Score})");

// Get threat intel
var threats = await client.GetThreatsAsync();
Console.WriteLine($"Signatures: {threats.TotalSignatures}");

// Chat with AI
await foreach (var token in client.ChatAsync("Analyze the latest threats"))
{
    Console.Write(token);
}

// Server metrics (admin)
var metrics = await client.GetServerMetricsAsync();
Console.WriteLine($"CPU: {metrics.CpuUsage}%, RAM: {metrics.MemoryUsageMb} MB");
```

## Requirements

- .NET 8.0 or later
- `System.Net.Http.Json` (included in .NET 8+)

## License

MIT License — see [../LICENSE](../LICENSE)
