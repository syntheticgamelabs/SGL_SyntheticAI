using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.LLM;

/// <summary>
/// Request model for AI image generation.
/// Compatible with Stable Diffusion WebUI (Automatic1111) /sdapi/v1/txt2img format.
/// </summary>
public class ImageGenRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("negative_prompt")]
    public string NegativePrompt { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("cfg_scale")]
    public double CfgScale { get; set; } = 7.5;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("sampler_name")]
    public string Sampler { get; set; } = "Euler";
}

/// <summary>
/// Result of an image generation request, including metadata about the source.
/// </summary>
public class ImageGenerationResult
{
    /// <summary>Raw PNG image bytes.</summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>Whether the fallback generator was used instead of SD WebUI.</summary>
    public bool UsedFallback { get; set; }

    /// <summary>Source description: "sd_webui" or "fallback_badge".</summary>
    public string Source { get; set; } = "unknown";

    /// <summary>Whether image generation succeeded at all.</summary>
    public bool Success => ImageData.Length > 0;
}

/// <summary>
/// AI Image Generation service that connects to a local Stable Diffusion WebUI-compatible
/// API server (e.g., Automatic1111 or Forge running on localhost:7860).
///
/// Falls back to a programmatic badge generator using System.Drawing if the image gen server
/// is not reachable. The fallback produces a styled card image with the prompt text,
/// app-themed colors, and a SyntheticAI watermark.
///
/// Endpoints used (SD WebUI compatible):
///   POST /sdapi/v1/txt2img       - Generate images from text prompt
///   GET  /sdapi/v1/sd-models     - List available checkpoint models
///   GET  /sdapi/v1/samplers      - List available samplers
/// </summary>
public class ImageGenerationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private volatile bool _isAvailable;
    private DateTime _lastAvailabilityCheck = DateTime.MinValue;
    private static readonly TimeSpan AvailabilityCacheDuration = TimeSpan.FromSeconds(30);

    // App theme colors
    private static readonly Color BackgroundColor = ColorTranslator.FromHtml("#1A1610");
    private static readonly Color AccentGold = ColorTranslator.FromHtml("#D4A017");
    private static readonly Color AccentGoldDim = Color.FromArgb(100, 212, 160, 23);
    private static readonly Color AccentGoldFaint = Color.FromArgb(40, 212, 160, 23);
    private static readonly Color TextWhite = Color.FromArgb(230, 230, 230);
    private static readonly Color TextGray = Color.FromArgb(160, 160, 160);
    private static readonly Color TextDimGray = Color.FromArgb(100, 100, 100);
    private static readonly Color SurfaceDark = Color.FromArgb(30, 25, 20);
    private static readonly Color SurfaceMid = Color.FromArgb(40, 35, 28);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Whether the SD WebUI image generation server is currently reachable.
    /// Cached for 30 seconds to avoid excessive health-check traffic.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            // Re-check availability if cache has expired
            if (DateTime.UtcNow - _lastAvailabilityCheck > AvailabilityCacheDuration)
            {
                _ = CheckAvailabilityAsync();
            }
            return _isAvailable;
        }
    }

    /// <summary>
    /// Whether the fallback badge generator is available.
    /// Always true since it uses System.Drawing which is bundled.
    /// </summary>
    public bool IsFallbackAvailable => true;

    /// <summary>
    /// Creates a new ImageGenerationService targeting the given base URL.
    /// Default: http://localhost:7860 (standard Stable Diffusion WebUI port).
    /// </summary>
    public ImageGenerationService(string baseUrl = "http://localhost:7860")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // Image generation can be slow
        };

        // Kick off initial availability check (fire-and-forget)
        _ = CheckAvailabilityAsync();
    }

    /// <summary>
    /// Checks whether the SD WebUI image generation server is reachable.
    /// </summary>
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/samplers", ct);
            _isAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _isAvailable = false;
        }

        _lastAvailabilityCheck = DateTime.UtcNow;
        return _isAvailable;
    }

    /// <summary>
    /// Generates an image from a text prompt.
    ///
    /// First attempts to use the Stable Diffusion WebUI API. If the server is not
    /// available or the request fails, falls back to a programmatic badge generator
    /// that creates a styled card image with the prompt text.
    ///
    /// Always returns a result with image data (never null), unless the prompt is empty.
    /// Check <see cref="ImageGenerationResult.UsedFallback"/> to determine the source.
    /// </summary>
    public async Task<ImageGenerationResult?> GenerateImageAsync(ImageGenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return null;

        // Clamp parameters to safe ranges
        request.Width = Math.Clamp(request.Width, 128, 2048);
        request.Height = Math.Clamp(request.Height, 128, 2048);
        request.Steps = Math.Clamp(request.Steps, 1, 150);
        request.CfgScale = Math.Clamp(request.CfgScale, 1.0, 30.0);

        // Try the SD WebUI server first
        if (_isAvailable || await CheckAvailabilityAsync(ct))
        {
            try
            {
                var sdBytes = await GenerateViaServerAsync(request, ct);
                if (sdBytes != null && sdBytes.Length > 0)
                {
                    return new ImageGenerationResult
                    {
                        ImageData = sdBytes,
                        UsedFallback = false,
                        Source = "sd_webui"
                    };
                }
            }
            catch (Exception ex)
            {
                SglLogger.Warning("Image generation server request failed, falling back to badge generator: {Error}", ex.Message);
                _isAvailable = false;
            }
        }

        // Fallback: generate a styled badge/card image programmatically
        SglLogger.Information("SD WebUI unavailable. Using fallback badge generator for prompt: {Prompt}",
            request.Prompt.Length > 80 ? request.Prompt[..80] + "..." : request.Prompt);

        var fallbackBytes = GenerateFallbackImage(request.Prompt, request.Width, request.Height);
        return new ImageGenerationResult
        {
            ImageData = fallbackBytes,
            UsedFallback = true,
            Source = "fallback_badge"
        };
    }

    /// <summary>
    /// Sends the generation request to the SD WebUI /sdapi/v1/txt2img endpoint.
    /// The response contains base64-encoded images; we decode the first one and return PNG bytes.
    /// </summary>
    private async Task<byte[]?> GenerateViaServerAsync(ImageGenRequest request, CancellationToken ct)
    {
        var payload = new
        {
            prompt = request.Prompt,
            negative_prompt = request.NegativePrompt,
            width = request.Width,
            height = request.Height,
            steps = request.Steps,
            cfg_scale = request.CfgScale,
            seed = request.Seed,
            sampler_name = request.Sampler,
            batch_size = 1,
            n_iter = 1
        };

        SglLogger.Information("Sending image gen request: {Width}x{Height}, {Steps} steps, sampler={Sampler}",
            request.Width, request.Height, request.Steps, request.Sampler);

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/sdapi/v1/txt2img", payload, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            SglLogger.Error($"Image gen server returned {(int)response.StatusCode}: {error}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("images", out var imagesArray) &&
            imagesArray.GetArrayLength() > 0)
        {
            var base64Image = imagesArray[0].GetString();
            if (!string.IsNullOrEmpty(base64Image))
            {
                // SD WebUI sometimes prefixes with "data:image/png;base64,"
                var commaIndex = base64Image.IndexOf(',');
                if (commaIndex >= 0)
                    base64Image = base64Image[(commaIndex + 1)..];

                var imageBytes = Convert.FromBase64String(base64Image);
                SglLogger.Information("Image generated successfully: {Size} bytes", imageBytes.Length);
                return imageBytes;
            }
        }

        SglLogger.Warning("Image gen response contained no images.");
        return null;
    }

    /// <summary>
    /// Generates a visually appealing fallback image using System.Drawing.
    /// Creates a styled card/badge with the app theme colors, the prompt text,
    /// decorative elements, and a SyntheticAI watermark.
    /// </summary>
    private static byte[] GenerateFallbackImage(string prompt, int width, int height)
    {
        // Ensure minimum size of 512x512
        width = Math.Max(width, 512);
        height = Math.Max(height, 512);

        using var bitmap = new Bitmap(width, height);
        using var gfx = Graphics.FromImage(bitmap);
        gfx.SmoothingMode = SmoothingMode.AntiAlias;
        gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // ── Background ──────────────────────────────────────────────────
        gfx.Clear(BackgroundColor);

        // Subtle gradient overlay from top-left to bottom-right
        using (var gradBrush = new LinearGradientBrush(
            new Point(0, 0), new Point(width, height),
            Color.FromArgb(15, 212, 160, 23),
            Color.FromArgb(5, 212, 160, 23)))
        {
            gfx.FillRectangle(gradBrush, 0, 0, width, height);
        }

        // ── Decorative grid pattern ─────────────────────────────────────
        using (var gridPen = new Pen(Color.FromArgb(12, 212, 160, 23), 1))
        {
            int gridSpacing = 40;
            for (int x = 0; x < width; x += gridSpacing)
                gfx.DrawLine(gridPen, x, 0, x, height);
            for (int y = 0; y < height; y += gridSpacing)
                gfx.DrawLine(gridPen, 0, y, width, y);
        }

        int margin = (int)(width * 0.06f);
        int innerWidth = width - margin * 2;
        int innerHeight = height - margin * 2;

        // ── Outer border frame ──────────────────────────────────────────
        using (var borderPen = new Pen(AccentGold, 2))
        {
            gfx.DrawRectangle(borderPen, margin, margin, innerWidth, innerHeight);
        }

        // ── Inner border frame ──────────────────────────────────────────
        int innerMargin = margin + 8;
        int innerW2 = width - innerMargin * 2;
        int innerH2 = height - innerMargin * 2;
        using (var innerBorderPen = new Pen(AccentGoldDim, 1))
        {
            gfx.DrawRectangle(innerBorderPen, innerMargin, innerMargin, innerW2, innerH2);
        }

        // ── Corner accents ──────────────────────────────────────────────
        int cornerLen = 30;
        using (var cornerPen = new Pen(AccentGold, 3))
        {
            // Top-left
            gfx.DrawLine(cornerPen, margin, margin, margin + cornerLen, margin);
            gfx.DrawLine(cornerPen, margin, margin, margin, margin + cornerLen);
            // Top-right
            gfx.DrawLine(cornerPen, margin + innerWidth, margin, margin + innerWidth - cornerLen, margin);
            gfx.DrawLine(cornerPen, margin + innerWidth, margin, margin + innerWidth, margin + cornerLen);
            // Bottom-left
            gfx.DrawLine(cornerPen, margin, margin + innerHeight, margin + cornerLen, margin + innerHeight);
            gfx.DrawLine(cornerPen, margin, margin + innerHeight, margin, margin + innerHeight - cornerLen);
            // Bottom-right
            gfx.DrawLine(cornerPen, margin + innerWidth, margin + innerHeight, margin + innerWidth - cornerLen, margin + innerHeight);
            gfx.DrawLine(cornerPen, margin + innerWidth, margin + innerHeight, margin + innerWidth, margin + innerHeight - cornerLen);
        }

        // ── Header bar ──────────────────────────────────────────────────
        int headerY = margin + 20;
        int headerHeight = (int)(height * 0.09f);
        using (var headerBrush = new SolidBrush(SurfaceDark))
        {
            gfx.FillRectangle(headerBrush, margin + 1, headerY, innerWidth - 1, headerHeight);
        }

        // Gold accent line under header
        using (var accentPen = new Pen(AccentGold, 2))
        {
            gfx.DrawLine(accentPen, margin + 20, headerY + headerHeight, margin + innerWidth - 20, headerY + headerHeight);
        }

        // ── Header text: "SYNTHETICAI" ──────────────────────────────────
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        float headerFontSize = Math.Max(12, width * 0.035f);
        using (var headerFont = new Font("Segoe UI", headerFontSize, FontStyle.Bold))
        using (var headerBrush = new SolidBrush(AccentGold))
        {
            gfx.DrawString("SYNTHETICAI", headerFont, headerBrush,
                new RectangleF(margin, headerY, innerWidth, headerHeight), sf);
        }

        // ── Decorative diamond icon ─────────────────────────────────────
        int diamondCenterX = width / 2;
        int diamondCenterY = headerY + headerHeight + (int)(height * 0.10f);
        int diamondSize = (int)(width * 0.06f);

        var diamondPoints = new PointF[]
        {
            new(diamondCenterX, diamondCenterY - diamondSize),
            new(diamondCenterX + diamondSize, diamondCenterY),
            new(diamondCenterX, diamondCenterY + diamondSize),
            new(diamondCenterX - diamondSize, diamondCenterY)
        };

        using (var diamondFill = new SolidBrush(AccentGoldFaint))
        {
            gfx.FillPolygon(diamondFill, diamondPoints);
        }
        using (var diamondPen = new Pen(AccentGold, 2))
        {
            gfx.DrawPolygon(diamondPen, diamondPoints);
        }

        // Inner diamond
        int innerDiamondSize = (int)(diamondSize * 0.55f);
        var innerDiamondPoints = new PointF[]
        {
            new(diamondCenterX, diamondCenterY - innerDiamondSize),
            new(diamondCenterX + innerDiamondSize, diamondCenterY),
            new(diamondCenterX, diamondCenterY + innerDiamondSize),
            new(diamondCenterX - innerDiamondSize, diamondCenterY)
        };
        using (var innerDiamondPen = new Pen(AccentGoldDim, 1))
        {
            gfx.DrawPolygon(innerDiamondPen, innerDiamondPoints);
        }

        // ── "IMAGE GENERATION" subtitle ─────────────────────────────────
        int subtitleY = diamondCenterY + diamondSize + (int)(height * 0.03f);
        float subtitleFontSize = Math.Max(8, width * 0.02f);
        using (var subtitleFont = new Font("Segoe UI", subtitleFontSize, FontStyle.Regular))
        using (var subtitleBrush = new SolidBrush(TextGray))
        {
            gfx.DrawString("IMAGE GENERATION", subtitleFont, subtitleBrush,
                new RectangleF(margin, subtitleY, innerWidth, subtitleFontSize * 2), sf);
        }

        // ── Separator line ──────────────────────────────────────────────
        int sepY = subtitleY + (int)(height * 0.05f);
        using (var sepPen = new Pen(AccentGoldDim, 1))
        {
            int sepMargin = (int)(width * 0.15f);
            gfx.DrawLine(sepPen, sepMargin, sepY, width - sepMargin, sepY);
        }

        // ── Prompt label ────────────────────────────────────────────────
        int promptLabelY = sepY + (int)(height * 0.03f);
        float labelFontSize = Math.Max(7, width * 0.016f);
        using (var labelFont = new Font("Segoe UI", labelFontSize, FontStyle.Bold))
        using (var labelBrush = new SolidBrush(AccentGold))
        {
            gfx.DrawString("PROMPT", labelFont, labelBrush,
                new RectangleF(margin, promptLabelY, innerWidth, labelFontSize * 2), sf);
        }

        // ── Prompt text area ────────────────────────────────────────────
        int promptTextY = promptLabelY + (int)(height * 0.04f);
        int promptTextAreaHeight = (int)(height * 0.25f);
        int textPadding = (int)(width * 0.10f);

        // Background for prompt text
        using (var textBgBrush = new SolidBrush(SurfaceMid))
        {
            gfx.FillRectangle(textBgBrush,
                margin + 20, promptTextY - 5,
                innerWidth - 40, promptTextAreaHeight + 10);
        }
        using (var textBorderPen = new Pen(AccentGoldFaint, 1))
        {
            gfx.DrawRectangle(textBorderPen,
                margin + 20, promptTextY - 5,
                innerWidth - 40, promptTextAreaHeight + 10);
        }

        // Prompt text with word wrap
        float promptFontSize = Math.Max(9, width * 0.022f);
        var promptTextRect = new RectangleF(
            textPadding, promptTextY,
            width - textPadding * 2, promptTextAreaHeight);

        var promptSf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord,
            FormatFlags = StringFormatFlags.LineLimit
        };

        // Truncate very long prompts for display
        string displayPrompt = prompt.Length > 300 ? prompt[..300] + "..." : prompt;

        using (var promptFont = new Font("Segoe UI", promptFontSize, FontStyle.Italic))
        using (var promptBrush = new SolidBrush(TextWhite))
        {
            gfx.DrawString($"\"{displayPrompt}\"", promptFont, promptBrush, promptTextRect, promptSf);
        }

        // ── Bottom separator ────────────────────────────────────────────
        int bottomSepY = margin + innerHeight - (int)(height * 0.14f);
        using (var sepPen = new Pen(AccentGoldDim, 1))
        {
            int sepMargin = (int)(width * 0.15f);
            gfx.DrawLine(sepPen, sepMargin, bottomSepY, width - sepMargin, bottomSepY);
        }

        // ── Status text ─────────────────────────────────────────────────
        int statusY = bottomSepY + (int)(height * 0.02f);
        float statusFontSize = Math.Max(7, width * 0.015f);
        using (var statusFont = new Font("Segoe UI", statusFontSize, FontStyle.Regular))
        using (var statusBrush = new SolidBrush(TextDimGray))
        {
            gfx.DrawString("AI image generation requires Stable Diffusion WebUI \u2022 Install at localhost:7860",
                statusFont, statusBrush,
                new RectangleF(margin, statusY, innerWidth, statusFontSize * 2), sf);
        }

        // ── Watermark ───────────────────────────────────────────────────
        int watermarkY = margin + innerHeight - (int)(height * 0.06f);
        float watermarkFontSize = Math.Max(7, width * 0.017f);
        using (var watermarkFont = new Font("Segoe UI", watermarkFontSize, FontStyle.Bold))
        using (var watermarkBrush = new SolidBrush(AccentGoldDim))
        {
            gfx.DrawString("SyntheticAI Image Generation", watermarkFont, watermarkBrush,
                new RectangleF(margin, watermarkY, innerWidth, watermarkFontSize * 2.5f), sf);
        }

        // ── Timestamp ───────────────────────────────────────────────────
        int timestampY = watermarkY + (int)(height * 0.03f);
        float timestampFontSize = Math.Max(6, width * 0.013f);
        using (var tsFont = new Font("Segoe UI", timestampFontSize, FontStyle.Regular))
        using (var tsBrush = new SolidBrush(TextDimGray))
        {
            gfx.DrawString(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                tsFont, tsBrush,
                new RectangleF(margin, timestampY, innerWidth, timestampFontSize * 2), sf);
        }

        // ── Serialize to PNG bytes ──────────────────────────────────────
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Returns the list of available model checkpoints from the SD WebUI server.
    /// Returns an empty list if the server is unavailable.
    /// </summary>
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = new List<string>();

        if (!_isAvailable && !await CheckAvailabilityAsync(ct))
        {
            models.Add("(SD server offline - fallback badge generator active)");
            return models;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/sd-models", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var model in doc.RootElement.EnumerateArray())
                    {
                        var title = model.TryGetProperty("title", out var t) ? t.GetString() :
                                    model.TryGetProperty("model_name", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrEmpty(title))
                            models.Add(title);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to fetch available models: {Error}", ex.Message);
        }

        if (models.Count == 0)
            models.Add("(no models found)");

        return models;
    }

    /// <summary>
    /// Returns the list of available samplers from the SD WebUI server.
    /// Useful for populating UI dropdowns.
    /// </summary>
    public async Task<List<string>> GetAvailableSamplersAsync(CancellationToken ct = default)
    {
        var samplers = new List<string>();

        if (!_isAvailable && !await CheckAvailabilityAsync(ct))
            return samplers;

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/samplers", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sampler in doc.RootElement.EnumerateArray())
                    {
                        var name = sampler.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (!string.IsNullOrEmpty(name))
                            samplers.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Warning("Failed to fetch available samplers: {Error}", ex.Message);
        }

        return samplers;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
