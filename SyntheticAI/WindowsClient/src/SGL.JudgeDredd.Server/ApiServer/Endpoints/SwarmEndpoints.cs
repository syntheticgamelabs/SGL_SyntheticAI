using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SGL.JudgeDredd.Server.ApiServer.Services.Swarm;
using System.Text.Json;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// API endpoints for the Seed-Based Distributed Learning System.
/// Handles seed upload/download, worker registration, job submission, and knowledge graph queries.
/// </summary>
public static class SwarmEndpoints
{
    public static void Map(WebApplication app)
    {
        // ═══ SEED API ═══

        // Upload a seed from a client
        app.MapPost("/api/v1/seed/upload", async (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var seed = await ctx.Request.ReadFromJsonAsync<SeedPackage>();
            if (seed == null)
                return Results.BadRequest(new { error = "Invalid seed package" });

            var result = await seedService.ProcessSeedAsync(seed);

            // Link to knowledge graph
            if (result.Success)
            {
                var kgService = ctx.RequestServices.GetRequiredService<KnowledgeGraphService>();
                kgService.LinkSeedToGraph(seed);
            }

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        // Upload a batch of seeds
        app.MapPost("/api/v1/seed/upload/batch", async (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var body = await ctx.Request.ReadFromJsonAsync<BatchUploadRequest>();
            if (body?.Seeds == null || body.Seeds.Count == 0)
                return Results.BadRequest(new { error = "No seeds provided" });

            var result = await seedService.ProcessBatchAsync(body.Seeds, body.ClientId ?? "unknown");
            return Results.Ok(result);
        });

        // Download top seeds for training
        app.MapGet("/api/v1/seed/download", (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var taskType = ctx.Request.Query["taskType"].FirstOrDefault();
            var clientId = ctx.Request.Query["clientId"].FirstOrDefault();
            int count = int.TryParse(ctx.Request.Query["count"].FirstOrDefault(), out var c) ? c : 20;

            var seeds = seedService.GetTopSeeds(count, taskType, clientId);
            return Results.Ok(seeds);
        });

        // Get seeds by task type
        app.MapGet("/api/v1/seed/search", (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var taskType = ctx.Request.Query["taskType"].FirstOrDefault() ?? "";
            int count = int.TryParse(ctx.Request.Query["count"].FirstOrDefault(), out var c) ? c : 50;

            var seeds = seedService.GetSeedsByTaskType(taskType, count);
            return Results.Ok(seeds);
        });

        // Validate a seed (dry run)
        app.MapPost("/api/v1/seed/validate", async (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var seed = await ctx.Request.ReadFromJsonAsync<SeedPackage>();
            if (seed == null)
                return Results.BadRequest(new { error = "Invalid seed" });

            // Just validate, don't store
            var validator = new SeedValidator();
            var reputation = seedService.GetClientReputation(seed.ClientId)
                ?? new ClientReputation { ClientId = seed.ClientId };
            var result = validator.Validate(seed, reputation);

            return Results.Ok(new { valid = result.IsValid, reason = result.Reason });
        });

        // ═══ WORKER API ═══

        // Register a worker
        app.MapPost("/api/v1/swarm/worker/register", async (HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            var worker = await ctx.Request.ReadFromJsonAsync<WorkerInfo>();
            if (worker == null)
                return Results.BadRequest(new { error = "Invalid worker info" });

            scheduler.RegisterWorker(worker);
            return Results.Ok(new { workerId = worker.WorkerId, status = "registered" });
        });

        // Worker heartbeat
        app.MapPost("/api/v1/swarm/worker/heartbeat", async (HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            var body = await ctx.Request.ReadFromJsonAsync<HeartbeatRequest>();
            if (body == null)
                return Results.BadRequest(new { error = "Invalid request" });

            var ok = scheduler.Heartbeat(body.WorkerId);
            return ok ? Results.Ok(new { alive = true }) : Results.NotFound();
        });

        // List all workers
        app.MapGet("/api/v1/swarm/workers", (HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            return Results.Ok(scheduler.GetWorkers());
        });

        // ═══ JOB / DAG API ═══

        // Submit a job (creates and executes a task graph)
        app.MapPost("/api/v1/swarm/job/submit", async (HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            var body = await ctx.Request.ReadFromJsonAsync<JobSubmitRequest>();
            if (body == null || string.IsNullOrWhiteSpace(body.Prompt))
                return Results.BadRequest(new { error = "Prompt is required" });

            var graph = await scheduler.SubmitJobAsync(body.Prompt, body.RequesterId ?? "api");
            return Results.Ok(new
            {
                jobId = graph.JobId,
                status = graph.Status.ToString(),
                nodeCount = graph.Nodes.Count,
                result = graph.FinalResult,
                completedAt = graph.CompletedAt
            });
        });

        // Get job status
        app.MapGet("/api/v1/swarm/job/{jobId}", (string jobId, HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            var job = scheduler.GetJob(jobId);
            return job != null ? Results.Ok(job) : Results.NotFound();
        });

        // List all recent jobs
        app.MapGet("/api/v1/swarm/jobs", (HttpContext ctx) =>
        {
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            int count = int.TryParse(ctx.Request.Query["count"].FirstOrDefault(), out var c) ? c : 50;
            return Results.Ok(scheduler.GetJobs(count));
        });

        // ═══ KNOWLEDGE GRAPH API ═══

        // Get the knowledge graph structure
        app.MapGet("/api/v1/swarm/knowledge/graph", (HttpContext ctx) =>
        {
            var kgService = ctx.RequestServices.GetRequiredService<KnowledgeGraphService>();
            return Results.Ok(kgService.GetGraph());
        });

        // Get a knowledge node with children
        app.MapGet("/api/v1/swarm/knowledge/node/{nodeId}", (string nodeId, HttpContext ctx) =>
        {
            var kgService = ctx.RequestServices.GetRequiredService<KnowledgeGraphService>();
            var node = kgService.GetNode(nodeId);
            if (node == null) return Results.NotFound();

            var children = kgService.GetChildren(nodeId);
            return Results.Ok(new { node, children });
        });

        // Vector similarity search
        app.MapPost("/api/v1/swarm/knowledge/search", async (HttpContext ctx) =>
        {
            var kgService = ctx.RequestServices.GetRequiredService<KnowledgeGraphService>();
            var body = await ctx.Request.ReadFromJsonAsync<VectorSearchRequest>();
            if (body?.Embedding == null || body.Embedding.Length == 0)
                return Results.BadRequest(new { error = "Embedding vector required" });

            var results = kgService.FindSimilarSeeds(body.Embedding, body.TopK > 0 ? body.TopK : 10);
            return Results.Ok(results.Select(r => new { seedId = r.SeedId, similarity = r.Similarity }));
        });

        // ═══ NETWORK STATS API ═══

        // Get swarm network statistics
        app.MapGet("/api/v1/swarm/stats", (HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var scheduler = ctx.RequestServices.GetRequiredService<SwarmScheduler>();
            var kgService = ctx.RequestServices.GetRequiredService<KnowledgeGraphService>();

            var stats = seedService.GetNetworkStats();
            return Results.Ok(new
            {
                seeds = stats,
                workers = new
                {
                    total = scheduler.WorkerCount,
                    activeJobs = scheduler.ActiveJobs
                },
                knowledgeGraph = new
                {
                    nodes = kgService.NodeCount,
                    embeddings = kgService.EmbeddingCount
                },
                timestamp = DateTime.UtcNow
            });
        });

        // Client reputation lookup
        app.MapGet("/api/v1/swarm/reputation/{clientId}", (string clientId, HttpContext ctx) =>
        {
            var seedService = ctx.RequestServices.GetRequiredService<SeedService>();
            var reputation = seedService.GetClientReputation(clientId);
            return reputation != null ? Results.Ok(reputation) : Results.NotFound();
        });
    }

    // ── Request models ──

    private class BatchUploadRequest
    {
        public List<SeedPackage> Seeds { get; set; } = new();
        public string? ClientId { get; set; }
    }

    private class HeartbeatRequest
    {
        public string WorkerId { get; set; } = string.Empty;
    }

    private class JobSubmitRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? RequesterId { get; set; }
    }

    private class VectorSearchRequest
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int TopK { get; set; } = 10;
    }
}
