namespace SGL.JudgeDredd.Core.Interfaces;

/// <summary>
/// Text-to-Speech service interface.
/// Connects to the vLLM-Omni / Qwen3-TTS server (OpenAI-compatible /v1/audio/speech endpoint).
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Synthesizes speech from text and returns the audio data as a byte array (WAV/MP3).
    /// </summary>
    Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "default", CancellationToken ct = default);

    /// <summary>
    /// Synthesizes speech and plays it directly through the default audio output device.
    /// </summary>
    Task SpeakAsync(string text, string voice = "default", CancellationToken ct = default);

    /// <summary>
    /// Stops any currently playing speech audio.
    /// </summary>
    void StopSpeaking();

    /// <summary>
    /// Whether the TTS server is available and responding.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Whether audio is currently being played.
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Returns available voice names from the TTS server.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableVoicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Tests connectivity to the TTS server.
    /// </summary>
    Task<bool> CheckAvailabilityAsync(CancellationToken ct = default);
}
