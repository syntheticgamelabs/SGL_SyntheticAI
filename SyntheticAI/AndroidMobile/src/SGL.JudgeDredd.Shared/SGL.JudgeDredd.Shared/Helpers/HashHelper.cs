using System.Security.Cryptography;

namespace SGL.JudgeDredd.Shared.Helpers;

public static class HashHelper
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        var hashBytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static async Task<string> ComputeMd5Async(string filePath, CancellationToken ct = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        var hashBytes = await MD5.HashDataAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
