using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Server.ApiServer.Services.Swarm;
using System.Text.Json;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// REST API endpoints for the Hierarchical Cluster, Digital Immune System,
/// Self-Healing Network, and Research Agent.
/// </summary>
public static class ClusterEndpoints
{
    public static void MapClusterEndpoints(this IEndpointRouteBuilder app)
    {
        // ─── Cluster Hierarchy ───────────────────────────────────

        app.MapGet("/api/v1/cluster/hierarchy", HandleGetHierarchy);
        app.MapGet("/api/v1/cluster/list", HandleGetClusters);
        app.MapPost("/api/v1/cluster/register", HandleRegisterCluster);
        app.MapPost("/api/v1/cluster/heartbeat", HandleClusterHeartbeat);
        app.MapGet("/api/v1/cluster/{clusterId}/agents", HandleGetClusterAgents);

        app.MapPost("/api/v1/agent/register", HandleRegisterAgent);
        app.MapPost("/api/v1/agent/heartbeat", HandleAgentHeartbeat);
        app.MapGet("/api/v1/agent/list", HandleGetAgents);

        // ─── Digital Immune System ───────────────────────────────

        app.MapGet("/api/v1/immune/threats", HandleGetThreats);
        app.MapGet("/api/v1/immune/threats/active", HandleGetActiveThreats);
        app.MapPost("/api/v1/immune/threats/{threatId}/resolve", HandleResolveThreat);
        app.MapGet("/api/v1/immune/quarantine", HandleGetQuarantine);
        app.MapPost("/api/v1/immune/quarantine/{nodeId}", HandleQuarantineNode);
        app.MapDelete("/api/v1/immune/quarantine/{nodeId}", HandleReleaseQuarantine);
        app.MapGet("/api/v1/immune/rules", HandleGetRules);
        app.MapPost("/api/v1/immune/rules", HandleAddRule);

        // ─── Self-Healing Network ────────────────────────────────

        app.MapGet("/api/v1/health/all", HandleGetAllHealth);
        app.MapGet("/api/v1/health/node/{nodeId}", HandleGetNodeHealth);
        app.MapGet("/api/v1/health/stats", HandleGetHealthStats);
        app.MapGet("/api/v1/health/recovery", HandleGetRecoveryHistory);
        app.MapPost("/api/v1/health/check", HandleTriggerHealthCheck);

        // ─── Research Agent ──────────────────────────────────────

        app.MapGet("/api/v1/research/status", HandleGetResearchStatus);
        app.MapPost("/api/v1/research/enqueue", HandleEnqueueResearch);
        app.MapGet("/api/v1/research/results", HandleGetResearchResults);
        app.MapGet("/api/v1/research/result/{taskId}", HandleGetResearchResult);

        // ─── Gossip Protocol ─────────────────────────────────────

        app.MapPost("/api/v1/gossip/broadcast", HandleGossipBroadcast);
        app.MapGet("/api/v1/gossip/drain/{clusterId}", HandleGossipDrain);
    }

    // ─── Cluster Handlers ────────────────────────────────────────

    private static IResult HandleGetHierarchy(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetHierarchySnapshot());
    }

    private static IResult HandleGetClusters(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetClusters());
    }

    private static async Task<IResult> HandleRegisterCluster(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var cluster = await context.Request.ReadFromJsonAsync<EdgeCluster>();
        if (cluster == null) return Results.BadRequest("Invalid cluster data");
        svc.RegisterCluster(cluster);
        return Results.Ok(new { cluster.ClusterId, status = "registered" });
    }

    private static async Task<IResult> HandleClusterHeartbeat(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var body = await context.Request.ReadFromJsonAsync<JsonElement>();
        var clusterId = body.GetProperty("clusterId").GetString()!;
        int agentCount = body.TryGetProperty("agentCount", out var ac) ? ac.GetInt32() : 0;
        float gpuPower = body.TryGetProperty("gpuPower", out var gp) ? gp.GetSingle() : 0;
        float capacity = body.TryGetProperty("capacity", out var cap) ? cap.GetSingle() : 1f;
        bool ok = svc.ClusterHeartbeat(clusterId, agentCount, gpuPower, capacity);
        return ok ? Results.Ok(new { alive = true }) : Results.NotFound();
    }

    private static IResult HandleGetClusterAgents(HttpContext context, string clusterId)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetAgentsForCluster(clusterId));
    }

    private static async Task<IResult> HandleRegisterAgent(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var agent = await context.Request.ReadFromJsonAsync<ClientAgent>();
        if (agent == null) return Results.BadRequest("Invalid agent data");
        svc.RegisterAgent(agent);
        return Results.Ok(new { agent.AgentId, status = "registered" });
    }

    private static async Task<IResult> HandleAgentHeartbeat(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var body = await context.Request.ReadFromJsonAsync<JsonElement>();
        var agentId = body.GetProperty("agentId").GetString()!;
        bool ok = svc.AgentHeartbeat(agentId);
        return ok ? Results.Ok(new { alive = true }) : Results.NotFound();
    }

    private static IResult HandleGetAgents(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetAgents());
    }

    // ─── Immune System Handlers ──────────────────────────────────

    private static IResult HandleGetThreats(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetThreatHistory());
    }

    private static IResult HandleGetActiveThreats(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetActiveThreats());
    }

    private static IResult HandleResolveThreat(HttpContext context, string threatId)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        svc.ResolveThreat(threatId, ThreatStatus.Resolved);
        return Results.Ok(new { resolved = true });
    }

    private static IResult HandleGetQuarantine(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetQuarantinedNodes());
    }

    private static async Task<IResult> HandleQuarantineNode(HttpContext context, string nodeId)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        var body = await context.Request.ReadFromJsonAsync<JsonElement>();
        var reason = body.TryGetProperty("reason", out var r) ? r.GetString() ?? "Admin quarantine" : "Admin quarantine";
        int hours = body.TryGetProperty("hours", out var h) ? h.GetInt32() : 2;
        svc.QuarantineNode(nodeId, reason, TimeSpan.FromHours(hours));
        return Results.Ok(new { quarantined = true, nodeId });
    }

    private static IResult HandleReleaseQuarantine(HttpContext context, string nodeId)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        svc.ReleaseFromQuarantine(nodeId);
        return Results.Ok(new { released = true, nodeId });
    }

    private static IResult HandleGetRules(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetRules());
    }

    private static async Task<IResult> HandleAddRule(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(DigitalImmuneSystem)) as DigitalImmuneSystem;
        if (svc == null) return Results.StatusCode(503);
        var rule = await context.Request.ReadFromJsonAsync<ImmuneRule>();
        if (rule == null) return Results.BadRequest("Invalid rule");
        svc.AddOrUpdateRule(rule);
        return Results.Ok(new { rule.RuleId, added = true });
    }

    // ─── Health Handlers ─────────────────────────────────────────

    private static IResult HandleGetAllHealth(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(SelfHealingNetwork)) as SelfHealingNetwork;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetAllHealth());
    }

    private static IResult HandleGetNodeHealth(HttpContext context, string nodeId)
    {
        var svc = context.RequestServices.GetService(typeof(SelfHealingNetwork)) as SelfHealingNetwork;
        if (svc == null) return Results.StatusCode(503);
        var health = svc.GetNodeHealth(nodeId);
        return health != null ? Results.Ok(health) : Results.NotFound();
    }

    private static IResult HandleGetHealthStats(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(SelfHealingNetwork)) as SelfHealingNetwork;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetStats());
    }

    private static IResult HandleGetRecoveryHistory(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(SelfHealingNetwork)) as SelfHealingNetwork;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetRecoveryHistory());
    }

    private static IResult HandleTriggerHealthCheck(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(SelfHealingNetwork)) as SelfHealingNetwork;
        if (svc == null) return Results.StatusCode(503);
        svc.RunHealthChecks();
        return Results.Ok(new { triggered = true, stats = svc.GetStats() });
    }

    // ─── Research Agent Handlers ─────────────────────────────────

    private static IResult HandleGetResearchStatus(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(ResearchLlmAgent)) as ResearchLlmAgent;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetStatus());
    }

    private static async Task<IResult> HandleEnqueueResearch(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(ResearchLlmAgent)) as ResearchLlmAgent;
        if (svc == null) return Results.StatusCode(503);
        var body = await context.Request.ReadFromJsonAsync<JsonElement>();
        var typeStr = body.GetProperty("type").GetString() ?? "SeedQualityAnalysis";
        var input = body.GetProperty("input").GetString() ?? "";
        if (!Enum.TryParse<ResearchTaskType>(typeStr, true, out var type))
            return Results.BadRequest($"Unknown research type: {typeStr}");
        var taskId = svc.EnqueueTask(type, input, "admin");
        return Results.Ok(new { taskId, queued = true });
    }

    private static IResult HandleGetResearchResults(HttpContext context)
    {
        if (context.Items["Role"]?.ToString() != "admin") return Results.Unauthorized();
        var svc = context.RequestServices.GetService(typeof(ResearchLlmAgent)) as ResearchLlmAgent;
        if (svc == null) return Results.StatusCode(503);
        return Results.Ok(svc.GetRecentResults());
    }

    private static IResult HandleGetResearchResult(HttpContext context, string taskId)
    {
        var svc = context.RequestServices.GetService(typeof(ResearchLlmAgent)) as ResearchLlmAgent;
        if (svc == null) return Results.StatusCode(503);
        var result = svc.GetResult(taskId);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    // ─── Gossip Handlers ─────────────────────────────────────────

    private static async Task<IResult> HandleGossipBroadcast(HttpContext context)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var message = await context.Request.ReadFromJsonAsync<GossipMessage>();
        if (message == null) return Results.BadRequest("Invalid gossip message");
        svc.BroadcastGossip(message);
        return Results.Ok(new { broadcast = true });
    }

    private static IResult HandleGossipDrain(HttpContext context, string clusterId)
    {
        var svc = context.RequestServices.GetService(typeof(HierarchicalClusterService)) as HierarchicalClusterService;
        if (svc == null) return Results.StatusCode(503);
        var messages = svc.DrainGossipForCluster(clusterId);
        return Results.Ok(messages);
    }
}
