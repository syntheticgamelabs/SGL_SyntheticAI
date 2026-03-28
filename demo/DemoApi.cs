// Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.
// NOTE: This is a simplified demonstration module. Production detection algorithms are proprietary.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace SyntheticAI.Demo;

/// <summary>
/// Minimal ASP.NET Core API exposing demo endpoints.
/// The production API is a distributed microservice architecture.
/// </summary>
public static class DemoApi
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var simulator = new TelemetrySimulator();
        var detector = new DemoThreatDetector();
        var graphBuilder = new ThreatGraphBuilder();

        // GET /demo/events - Generate a random telemetry event
        app.MapGet("/demo/events", () =>
        {
            var evt = simulator.GenerateEvent();
            graphBuilder.AddEvent(evt);
            return Results.Ok(evt);
        });

        // GET /demo/threats - Return the current threat graph
        app.MapGet("/demo/threats", () =>
        {
            var graph = graphBuilder.GetGraph();
            return Results.Ok(new
            {
                NodeCount = graph.Count,
                Nodes = graph,
                HighRiskNodes = graph.Where(n => n.RiskScore > 0.5).ToList()
            });
        });

        // POST /demo/analyze - Analyze a batch of generated events
        app.MapPost("/demo/analyze", (BatchRequest request) =>
        {
            var events = simulator.GenerateBatch(Math.Clamp(request.Count, 1, 100));
            foreach (var evt in events)
                graphBuilder.AddEvent(evt);

            var results = events.Select(e => new
            {
                Event = e,
                IsSuspicious = detector.IsSuspicious(e),
                ThreatScore = detector.GetThreatScore(e)
            });
            return Results.Ok(results);
        });

        // GET /demo/status - System information
        app.MapGet("/demo/status", () => Results.Ok(new
        {
            System = "SyntheticAI Demo",
            Version = "1.1.49-demo",
            Mode = "Demonstration Only",
            GraphNodes = graphBuilder.GetGraph().Count,
            Disclaimer = "This demo uses simplified rules. Production system is proprietary.",
            Timestamp = DateTime.UtcNow
        }));
    }
}

public record BatchRequest(int Count = 10);
