using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer.Services.Swarm;

/// <summary>
/// Provides real compression and decompression for seed distribution
/// using .NET built-in compression streams (Deflate, GZip, Brotli).
/// Automatically selects the best algorithm based on payload size.
/// </summary>
public static class SeedCompression
{
    /// <summary>
    /// Compresses a seed package for network transmission.
    /// Selects algorithm based on data size:
    ///   &lt;1KB  — no compression (overhead > savings)
    ///   1KB–100KB — Deflate (fast, reasonable ratio)
    ///   &gt;100KB — Brotli (best ratio for larger payloads)
    /// </summary>
    public static CompressedPayload CompressSeed(SeedPackage seed)
    {
        var json = JsonSerializer.Serialize(seed);
        var rawBytes = Encoding.UTF8.GetBytes(json);

        if (rawBytes.Length < 1024)
        {
            return new CompressedPayload
            {
                Data = rawBytes,
                Algorithm = CompressionAlgorithm.None,
                OriginalSize = rawBytes.Length,
                CompressedSize = rawBytes.Length
            };
        }

        var algorithm = rawBytes.Length > 102400 ? CompressionAlgorithm.Brotli : CompressionAlgorithm.Deflate;
        var compressed = Compress(rawBytes, algorithm);

        return new CompressedPayload
        {
            Data = compressed,
            Algorithm = algorithm,
            OriginalSize = rawBytes.Length,
            CompressedSize = compressed.Length
        };
    }

    /// <summary>
    /// Decompresses a payload back into a SeedPackage.
    /// </summary>
    public static SeedPackage? DecompressSeed(CompressedPayload payload)
    {
        var raw = payload.Algorithm == CompressionAlgorithm.None
            ? payload.Data
            : Decompress(payload.Data, payload.Algorithm);

        var json = Encoding.UTF8.GetString(raw);
        return JsonSerializer.Deserialize<SeedPackage>(json);
    }

    /// <summary>
    /// Compresses a batch of seeds for efficient bulk transfer.
    /// </summary>
    public static CompressedPayload CompressBatch(List<SeedPackage> seeds)
    {
        var json = JsonSerializer.Serialize(seeds);
        var rawBytes = Encoding.UTF8.GetBytes(json);
        var algorithm = CompressionAlgorithm.Brotli; // Always use Brotli for batches
        var compressed = Compress(rawBytes, algorithm);

        SglLogger.Information($"[Compression] Batch compressed: {seeds.Count} seeds, " +
                       $"{rawBytes.Length:N0} -> {compressed.Length:N0} bytes " +
                       $"({1f - (float)compressed.Length / rawBytes.Length:P1} reduction)");

        return new CompressedPayload
        {
            Data = compressed,
            Algorithm = algorithm,
            OriginalSize = rawBytes.Length,
            CompressedSize = compressed.Length
        };
    }

    /// <summary>
    /// Decompresses a batch payload back into a list of seeds.
    /// </summary>
    public static List<SeedPackage>? DecompressBatch(CompressedPayload payload)
    {
        var raw = payload.Algorithm == CompressionAlgorithm.None
            ? payload.Data
            : Decompress(payload.Data, payload.Algorithm);

        var json = Encoding.UTF8.GetString(raw);
        return JsonSerializer.Deserialize<List<SeedPackage>>(json);
    }

    /// <summary>
    /// Compresses arbitrary byte data with the specified algorithm.
    /// </summary>
    public static byte[] Compress(byte[] data, CompressionAlgorithm algorithm)
    {
        using var output = new MemoryStream();
        using (var compressor = CreateCompressionStream(output, algorithm, CompressionLevel.Optimal))
        {
            compressor.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses byte data with the specified algorithm.
    /// </summary>
    public static byte[] Decompress(byte[] data, CompressionAlgorithm algorithm)
    {
        using var input = new MemoryStream(data);
        using var decompressor = CreateDecompressionStream(input, algorithm);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    private static Stream CreateCompressionStream(Stream output, CompressionAlgorithm alg, CompressionLevel level) =>
        alg switch
        {
            CompressionAlgorithm.Deflate => new DeflateStream(output, level, leaveOpen: true),
            CompressionAlgorithm.GZip => new GZipStream(output, level, leaveOpen: true),
            CompressionAlgorithm.Brotli => new BrotliStream(output, level, leaveOpen: true),
            _ => throw new ArgumentException($"Unsupported compression: {alg}")
        };

    private static Stream CreateDecompressionStream(Stream input, CompressionAlgorithm alg) =>
        alg switch
        {
            CompressionAlgorithm.Deflate => new DeflateStream(input, CompressionMode.Decompress),
            CompressionAlgorithm.GZip => new GZipStream(input, CompressionMode.Decompress),
            CompressionAlgorithm.Brotli => new BrotliStream(input, CompressionMode.Decompress),
            _ => throw new ArgumentException($"Unsupported decompression: {alg}")
        };
}
