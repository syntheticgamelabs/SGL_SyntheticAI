using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.LLM;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Endpoints;

/// <summary>
/// Admin-only endpoints for managing multi-LLM model slots.
/// GET  /api/v1/llm/slots         - Get status of all 4 slots
/// POST /api/v1/llm/slots/mount   - Mount a model into a slot
/// POST /api/v1/llm/slots/unmount - Unmount a model from a slot
/// POST /api/v1/llm/slots/role    - Change a slot's role
/// POST /api/v1/llm/slots/ability - Change a slot's ability
/// GET  /api/v1/llm/catalog       - Get available models catalog
/// </summary>
public static class MultiLlmEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiConstants.ApiPrefix + "/llm/slots", (Delegate)HandleGetSlots);
        app.MapPost(ApiConstants.ApiPrefix + "/llm/slots/mount", (Delegate)HandleMount);
        app.MapPost(ApiConstants.ApiPrefix + "/llm/slots/unmount", (Delegate)HandleUnmount);
        app.MapPost(ApiConstants.ApiPrefix + "/llm/slots/role", (Delegate)HandleSetRole);
        app.MapPost(ApiConstants.ApiPrefix + "/llm/slots/ability", (Delegate)HandleSetAbility);
        app.MapGet(ApiConstants.ApiPrefix + "/llm/catalog", (Delegate)HandleGetCatalog);
    }

    private static IResult HandleGetSlots(HttpContext context, MultiLlmManager manager)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var slots = manager.GetAllSlots();
            return Results.Json(new
            {
                slots,
                mountedCount = manager.MountedCount,
                maxSlots = MultiLlmManager.MaxSlots,
                totalMemoryMB = manager.TotalMemoryUsageMB,
                timestamp = DateTime.UtcNow
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM slots GET error: {ex.Message}");
            return Results.Problem("Failed to retrieve LLM slot status.");
        }
    }

    private static async Task<IResult> HandleMount(HttpContext context, MultiLlmManager manager)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var request = await context.Request.ReadFromJsonAsync<MountRequest>(JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.ModelId))
                return Results.BadRequest(new { error = "ModelId is required." });

            if (request.SlotIndex < 0 || request.SlotIndex >= MultiLlmManager.MaxSlots)
                return Results.BadRequest(new { error = $"SlotIndex must be 0-{MultiLlmManager.MaxSlots - 1}." });

            SglLogger.Information("Admin mounting model {ModelId} into slot {Slot} with role {Role}",
                request.ModelId, request.SlotIndex, request.Role);

            var slotInfo = await manager.MountAsync(
                request.SlotIndex,
                request.ModelId,
                request.Role,
                request.Ability,
                request.GpuLayers,
                request.ContextSize > 0 ? request.ContextSize : 2048u);

            return Results.Ok(new { message = "Model mounted successfully", slot = slotInfo });
        }
        catch (FileNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM mount error: {ex.Message}");
            return Results.Problem($"Failed to mount model: {ex.Message}");
        }
    }

    private static IResult HandleUnmount(HttpContext context, MultiLlmManager manager)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var slotIndex = 0;
            if (context.Request.Query.TryGetValue("slot", out var slotStr) && int.TryParse(slotStr, out var s))
                slotIndex = s;

            if (slotIndex < 0 || slotIndex >= MultiLlmManager.MaxSlots)
                return Results.BadRequest(new { error = $"Slot index must be 0-{MultiLlmManager.MaxSlots - 1}." });

            manager.Unmount(slotIndex);
            SglLogger.Information("Admin unmounted LLM slot {Slot}", slotIndex);

            return Results.Ok(new { message = $"Slot {slotIndex} unmounted", slots = manager.GetAllSlots() });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM unmount error: {ex.Message}");
            return Results.Problem($"Failed to unmount: {ex.Message}");
        }
    }

    private static async Task<IResult> HandleSetRole(HttpContext context, MultiLlmManager manager)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var request = await context.Request.ReadFromJsonAsync<SetRoleRequest>(JsonOptions);
            if (request == null)
                return Results.BadRequest(new { error = "Request body required." });

            manager.SetRole(request.SlotIndex, request.Role);
            return Results.Ok(new { message = "Role updated", slots = manager.GetAllSlots() });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM set role error: {ex.Message}");
            return Results.Problem($"Failed to set role: {ex.Message}");
        }
    }

    private static async Task<IResult> HandleSetAbility(HttpContext context, MultiLlmManager manager)
    {
        var role = context.Items["Role"]?.ToString();
        if (role != "admin")
            return Results.Json(new { error = "Admin access required" }, statusCode: 403);

        try
        {
            var request = await context.Request.ReadFromJsonAsync<SetAbilityRequest>(JsonOptions);
            if (request == null)
                return Results.BadRequest(new { error = "Request body required." });

            manager.SetAbility(request.SlotIndex, request.Ability);
            return Results.Ok(new { message = "Ability updated", slots = manager.GetAllSlots() });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM set ability error: {ex.Message}");
            return Results.Problem($"Failed to set ability: {ex.Message}");
        }
    }

    private static IResult HandleGetCatalog(HttpContext context)
    {
        try
        {
            var catalog = LlmModelInfo.GetCatalog();
            return Results.Ok(new { models = catalog, count = catalog.Count });
        }
        catch (Exception ex)
        {
            SglLogger.Error($"LLM catalog error: {ex.Message}");
            return Results.Problem("Failed to retrieve model catalog.");
        }
    }

    // Request models
    private class MountRequest
    {
        public int SlotIndex { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public LlmSlotRole Role { get; set; } = LlmSlotRole.Unassigned;
        public LlmSlotAbility Ability { get; set; } = LlmSlotAbility.None;
        public int GpuLayers { get; set; }
        public uint ContextSize { get; set; } = 2048;
    }

    private class SetRoleRequest
    {
        public int SlotIndex { get; set; }
        public LlmSlotRole Role { get; set; }
    }

    private class SetAbilityRequest
    {
        public int SlotIndex { get; set; }
        public LlmSlotAbility Ability { get; set; }
    }
}
