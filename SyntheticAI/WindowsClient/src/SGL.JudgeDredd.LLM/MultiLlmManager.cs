using System.Collections.Concurrent;
using LLama;
using LLama.Common;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// Manages up to 4 simultaneous LLM model slots with assigned roles.
/// Each slot can load a different GGUF model and serve a specific function.
/// </summary>
public class MultiLlmManager : IDisposable
{
    private readonly string _modelDirectory;
    private readonly ConcurrentDictionary<int, LlmSlot> _slots = new();
    private readonly SemaphoreSlim _mountLock = new(1, 1);
    private bool _disposed;

    /// <summary>Maximum number of concurrent LLM model slots.</summary>
    public const int MaxSlots = 4;

    /// <summary>Raised when a slot's state changes (mount/unmount/role change).</summary>
    public event EventHandler<LlmSlotChangedEventArgs>? SlotChanged;

    public MultiLlmManager(string modelDirectory)
    {
        _modelDirectory = modelDirectory ?? throw new ArgumentNullException(nameof(modelDirectory));
    }

    /// <summary>
    /// Gets the status of all 4 slots (including empty ones).
    /// </summary>
    public List<LlmSlotInfo> GetAllSlots()
    {
        var result = new List<LlmSlotInfo>();
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_slots.TryGetValue(i, out var slot))
            {
                result.Add(new LlmSlotInfo
                {
                    SlotIndex = i,
                    IsMounted = slot.IsLoaded,
                    IsLoading = slot.IsLoading,
                    ModelId = slot.ModelId,
                    ModelName = slot.ModelName,
                    Role = slot.Role,
                    Ability = slot.Ability,
                    Error = slot.Error,
                    LoadedAt = slot.LoadedAt,
                    MemoryUsageMB = slot.EstimatedMemoryMB
                });
            }
            else
            {
                result.Add(new LlmSlotInfo
                {
                    SlotIndex = i,
                    IsMounted = false,
                    Role = i == 0 ? LlmSlotRole.MainEngine : LlmSlotRole.Unassigned
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Mounts a model into the specified slot with the given role and ability.
    /// </summary>
    public async Task<LlmSlotInfo> MountAsync(int slotIndex, string modelId, LlmSlotRole role,
        LlmSlotAbility ability = LlmSlotAbility.None, int gpuLayers = 0, uint contextSize = 2048,
        CancellationToken ct = default)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be 0-{MaxSlots - 1}");

        var modelInfo = LlmModelInfo.FindById(modelId);
        if (modelInfo == null)
            throw new ArgumentException($"Unknown model ID: {modelId}");

        await _mountLock.WaitAsync(ct);
        try
        {
            // Unmount existing model in this slot if any
            if (_slots.TryGetValue(slotIndex, out var existing) && existing.IsLoaded)
            {
                existing.Unload();
            }

            var slot = new LlmSlot
            {
                SlotIndex = slotIndex,
                ModelId = modelId,
                ModelName = modelInfo.DisplayName,
                Role = role,
                Ability = ability,
                IsLoading = true,
                EstimatedMemoryMB = modelInfo.RamRequiredMB
            };

            _slots[slotIndex] = slot;
            SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.Loading));

            try
            {
                var modelPath = FindModelPath(modelInfo);
                if (!File.Exists(modelPath))
                    throw new FileNotFoundException($"Model file not found: {modelPath}");

                var modelParams = new ModelParams(modelPath)
                {
                    ContextSize = contextSize,
                    GpuLayerCount = gpuLayers,
                };

                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    slot.Weights = LLamaWeights.LoadFromFile(modelParams);
                    ct.ThrowIfCancellationRequested();
                    slot.Context = slot.Weights.CreateContext(modelParams);
                    slot.SessionManager = new ChatSessionManager();
                }, ct);

                slot.IsLoaded = true;
                slot.IsLoading = false;
                slot.LoadedAt = DateTime.UtcNow;
                slot.Error = null;

                SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.Mounted));
            }
            catch (Exception ex)
            {
                slot.IsLoading = false;
                slot.IsLoaded = false;
                slot.Error = ex.Message;
                SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.Error));
                throw;
            }

            return GetSlotInfo(slotIndex);
        }
        finally
        {
            _mountLock.Release();
        }
    }

    /// <summary>
    /// Unmounts the model from the specified slot, freeing resources.
    /// </summary>
    public void Unmount(int slotIndex)
    {
        if (_slots.TryRemove(slotIndex, out var slot))
        {
            slot.Unload();
            SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.Unmounted));
        }
    }

    /// <summary>
    /// Sets the role for a mounted slot.
    /// </summary>
    public void SetRole(int slotIndex, LlmSlotRole role)
    {
        if (_slots.TryGetValue(slotIndex, out var slot))
        {
            slot.Role = role;
            SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.RoleChanged));
        }
    }

    /// <summary>
    /// Sets the ability for a mounted slot.
    /// </summary>
    public void SetAbility(int slotIndex, LlmSlotAbility ability)
    {
        if (_slots.TryGetValue(slotIndex, out var slot))
        {
            slot.Ability = ability;
            SlotChanged?.Invoke(this, new LlmSlotChangedEventArgs(slotIndex, LlmSlotChangeType.AbilityChanged));
        }
    }

    /// <summary>
    /// Gets the LLamaContext for the slot with the specified role, or null if not mounted.
    /// </summary>
    public LLamaContext? GetContextByRole(LlmSlotRole role)
    {
        var slot = _slots.Values.FirstOrDefault(s => s.Role == role && s.IsLoaded);
        return slot?.Context;
    }

    /// <summary>
    /// Gets the ChatSessionManager for a specific slot.
    /// </summary>
    public ChatSessionManager? GetSessionManager(int slotIndex)
    {
        return _slots.TryGetValue(slotIndex, out var slot) && slot.IsLoaded ? slot.SessionManager : null;
    }

    /// <summary>
    /// Gets the main engine slot (slot 0 or whichever has MainEngine role).
    /// </summary>
    public LlmSlot? GetMainEngine()
    {
        return _slots.Values.FirstOrDefault(s => s.Role == LlmSlotRole.MainEngine && s.IsLoaded)
            ?? (_slots.TryGetValue(0, out var s0) && s0.IsLoaded ? s0 : null);
    }

    /// <summary>
    /// Gets a slot by its index.
    /// </summary>
    public LlmSlot? GetSlot(int index)
    {
        return _slots.TryGetValue(index, out var slot) ? slot : null;
    }

    /// <summary>
    /// Returns the total estimated memory usage across all mounted slots in MB.
    /// </summary>
    public int TotalMemoryUsageMB => _slots.Values.Where(s => s.IsLoaded).Sum(s => s.EstimatedMemoryMB);

    /// <summary>
    /// Returns the number of currently mounted (loaded) models.
    /// </summary>
    public int MountedCount => _slots.Values.Count(s => s.IsLoaded);

    private LlmSlotInfo GetSlotInfo(int index)
    {
        if (_slots.TryGetValue(index, out var slot))
        {
            return new LlmSlotInfo
            {
                SlotIndex = index,
                IsMounted = slot.IsLoaded,
                IsLoading = slot.IsLoading,
                ModelId = slot.ModelId,
                ModelName = slot.ModelName,
                Role = slot.Role,
                Ability = slot.Ability,
                Error = slot.Error,
                LoadedAt = slot.LoadedAt,
                MemoryUsageMB = slot.EstimatedMemoryMB
            };
        }
        return new LlmSlotInfo { SlotIndex = index };
    }

    private string FindModelPath(LlmModelInfo modelInfo)
    {
        // Try direct path
        var direct = Path.Combine(_modelDirectory, modelInfo.FileName);
        if (File.Exists(direct)) return direct;

        // Try subfolder path
        if (!string.IsNullOrEmpty(modelInfo.FolderName))
        {
            var subPath = Path.Combine(_modelDirectory, modelInfo.FolderName, modelInfo.FileName);
            if (File.Exists(subPath)) return subPath;
        }

        // Search recursively
        if (Directory.Exists(_modelDirectory))
        {
            var found = Directory.GetFiles(_modelDirectory, modelInfo.FileName, SearchOption.AllDirectories);
            if (found.Length > 0) return found[0];
        }

        return direct; // Return default path to get a meaningful error
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var slot in _slots.Values)
            slot.Unload();

        _slots.Clear();
        _mountLock.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a single LLM model slot with its loaded state.
/// </summary>
public class LlmSlot
{
    public int SlotIndex { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public LlmSlotRole Role { get; set; }
    public LlmSlotAbility Ability { get; set; }
    public bool IsLoaded { get; set; }
    public bool IsLoading { get; set; }
    public string? Error { get; set; }
    public DateTime? LoadedAt { get; set; }
    public int EstimatedMemoryMB { get; set; }

    internal LLamaWeights? Weights { get; set; }
    internal LLamaContext? Context { get; set; }
    internal ChatSessionManager? SessionManager { get; set; }

    public void Unload()
    {
        IsLoaded = false;
        IsLoading = false;
        Context?.Dispose();
        Context = null;
        Weights?.Dispose();
        Weights = null;
        SessionManager = null;
    }
}

/// <summary>
/// Serializable information about an LLM slot for API responses.
/// </summary>
public class LlmSlotInfo
{
    public int SlotIndex { get; set; }
    public bool IsMounted { get; set; }
    public bool IsLoading { get; set; }
    public string? ModelId { get; set; }
    public string? ModelName { get; set; }
    public LlmSlotRole Role { get; set; }
    public LlmSlotAbility Ability { get; set; }
    public string? Error { get; set; }
    public DateTime? LoadedAt { get; set; }
    public int MemoryUsageMB { get; set; }
}

/// <summary>
/// Roles that can be assigned to LLM model slots.
/// </summary>
public enum LlmSlotRole
{
    Unassigned = 0,
    MainEngine = 1,
    PromptCleanup = 2,
    TtsEngine = 3,
    AiChat = 4
}

/// <summary>
/// Abilities that can be assigned to non-main LLM slots.
/// </summary>
public enum LlmSlotAbility
{
    None = 0,
    Agent = 1,
    Monitor = 2,
    DualPowerMain = 3,
    ImageGeneration = 4,
    TextToSpeech = 5,
    PromptCleanUp = 6
}

/// <summary>
/// Event args for slot state changes.
/// </summary>
public class LlmSlotChangedEventArgs : EventArgs
{
    public int SlotIndex { get; }
    public LlmSlotChangeType ChangeType { get; }

    public LlmSlotChangedEventArgs(int slotIndex, LlmSlotChangeType changeType)
    {
        SlotIndex = slotIndex;
        ChangeType = changeType;
    }
}

public enum LlmSlotChangeType
{
    Loading,
    Mounted,
    Unmounted,
    Error,
    RoleChanged,
    AbilityChanged
}
