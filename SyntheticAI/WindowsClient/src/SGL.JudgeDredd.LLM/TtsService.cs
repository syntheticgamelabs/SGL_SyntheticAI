using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SGL.JudgeDredd.Core.Interfaces;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// TTS service with two backends:
/// 1. External vLLM-Omni / Qwen3-TTS server (OpenAI-compatible /v1/audio/speech endpoint)
/// 2. Windows SAPI fallback via PowerShell (always available on Windows)
/// Default external endpoint: http://localhost:8091
/// </summary>
public class TtsService : ITtsService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private Process? _playerProcess;
    private CancellationTokenSource? _playbackCts;
    private volatile bool _isSpeaking;
    private string? _tempAudioFile;
    private bool _isExternalServerAvailable;

    /// <summary>
    /// Available voices for Qwen3-TTS (external server).
    /// When using SAPI fallback, "Windows Default" is used.
    /// </summary>
    private static readonly string[] DefaultVoices =
    {
        "Windows Default", "Chelsie", "Ethan", "Aidan", "Luna",
        "Aria", "Sage", "Quinn", "Nova", "Willow"
    };

    public bool IsAvailable { get; private set; }
    public bool IsSpeaking => _isSpeaking;

    public TtsService(string baseUrl = "http://localhost:8091")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // On Windows, TTS is always available via SAPI fallback
        IsAvailable = OperatingSystem.IsWindows();
    }

    /// <inheritdoc />
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        // Check external Qwen3-TTS server
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models", ct);
            _isExternalServerAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _isExternalServerAvailable = false;
        }

        // Available if external server works OR we're on Windows (SAPI fallback)
        IsAvailable = _isExternalServerAvailable || OperatingSystem.IsWindows();
        return IsAvailable;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAvailableVoicesAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return DefaultVoices;
    }

    /// <inheritdoc />
    public async Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<byte>();

        // This method only works with the external TTS server
        if (!_isExternalServerAvailable)
            throw new InvalidOperationException(
                "External TTS server not available. Use SpeakAsync() which supports Windows SAPI fallback.");

        var effectiveVoice = (voice == "default" || voice == "Windows Default") ? "Chelsie" : voice;

        var request = new TtsRequest
        {
            Model = "tts",
            Input = text,
            Voice = effectiveVoice,
            ResponseFormat = "wav"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/v1/audio/speech",
            request,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"TTS synthesis failed ({response.StatusCode}): {error}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task SpeakAsync(string text, string voice = "default", CancellationToken ct = default)
    {
        StopSpeaking();

        if (string.IsNullOrWhiteSpace(text))
            return;

        // Try external TTS server first (Qwen3-TTS - higher quality)
        if (_isExternalServerAvailable && voice != "Windows Default")
        {
            try
            {
                var audioData = await SynthesizeSpeechAsync(text, voice, ct);
                if (audioData.Length > 0)
                {
                    await PlayWavAudioAsync(audioData, ct);
                    return;
                }
            }
            catch
            {
                // External server failed — fall through to SAPI
                _isExternalServerAvailable = false;
            }
        }

        // Fallback: Windows SAPI via PowerShell
        if (OperatingSystem.IsWindows())
        {
            await SpeakViaSapiAsync(text, ct);
        }
    }

    /// <summary>
    /// Plays WAV audio data through a temp file using PowerShell SoundPlayer.
    /// Used when external TTS server provides audio bytes.
    /// </summary>
    private async Task PlayWavAudioAsync(byte[] audioData, CancellationToken ct)
    {
        _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _playbackCts.Token;

        await Task.Run(async () =>
        {
            try
            {
                _isSpeaking = true;

                _tempAudioFile = Path.Combine(Path.GetTempPath(), $"jd_tts_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(_tempAudioFile, audioData, token);

                _playerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-NoProfile -Command \"(New-Object System.Media.SoundPlayer '{_tempAudioFile}').PlaySync()\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                _playerProcess.Start();
                await _playerProcess.WaitForExitAsync(token);
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _isSpeaking = false;
                CleanupTempFile();
            }
        }, token);
    }

    /// <summary>
    /// Speaks text using Windows SAPI (System.Speech.Synthesis) via PowerShell.
    /// Always available on Windows without any external server.
    /// </summary>
    private async Task SpeakViaSapiAsync(string text, CancellationToken ct)
    {
        _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _playbackCts.Token;

        await Task.Run(async () =>
        {
            try
            {
                _isSpeaking = true;

                // Escape text for PowerShell single-quoted string
                var escapedText = text
                    .Replace("'", "''")
                    .Replace("\r", "")
                    .Replace("\n", " ");

                // Limit length for SAPI to prevent very long speech
                if (escapedText.Length > 1500)
                    escapedText = escapedText[..1500];

                var psCommand =
                    "Add-Type -AssemblyName System.Speech; " +
                    "$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                    "$synth.Rate = 1; " +
                    $"$synth.Speak('{escapedText}'); " +
                    "$synth.Dispose()";

                _playerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-NoProfile -Command \"{psCommand}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                _playerProcess.Start();
                await _playerProcess.WaitForExitAsync(token);
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _isSpeaking = false;
            }
        }, token);
    }

    /// <inheritdoc />
    public void StopSpeaking()
    {
        try
        {
            _playbackCts?.Cancel();
            if (_playerProcess is { HasExited: false })
            {
                _playerProcess.Kill(entireProcessTree: true);
                _playerProcess.Dispose();
                _playerProcess = null;
            }
        }
        catch { }
        _isSpeaking = false;
        CleanupTempFile();
    }

    private void CleanupTempFile()
    {
        try
        {
            if (_tempAudioFile != null && File.Exists(_tempAudioFile))
            {
                File.Delete(_tempAudioFile);
                _tempAudioFile = null;
            }
        }
        catch { }
    }

    public void Dispose()
    {
        StopSpeaking();
        _httpClient.Dispose();
        _playbackCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class TtsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "tts";

        [JsonPropertyName("input")]
        public string Input { get; set; } = "";

        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "Chelsie";

        [JsonPropertyName("response_format")]
        public string ResponseFormat { get; set; } = "wav";
    }
}
