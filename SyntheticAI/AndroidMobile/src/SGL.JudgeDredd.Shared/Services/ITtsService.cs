namespace SGL.JudgeDredd.Shared.Services;

/// <summary>
/// Provides text-to-speech synthesis via the Qwen3-TTS engine (vLLM-Omni on localhost:8091).
/// </summary>
public interface ITtsService
{
    /// <summary>Whether the TTS backend server is reachable.</summary>
    bool IsAvailable { get; }

    /// <summary>Master on/off toggle for TTS playback.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Currently selected voice preset.</summary>
    string CurrentVoice { get; set; }

    /// <summary>List of voice presets supported by the TTS engine.</summary>
    string[] AvailableVoices { get; }

    /// <summary>Synthesize <paramref name="text"/> into WAV audio bytes.</summary>
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default);

    /// <summary>Ping the TTS backend and update <see cref="IsAvailable"/>.</summary>
    Task<bool> CheckAvailabilityAsync();
}
