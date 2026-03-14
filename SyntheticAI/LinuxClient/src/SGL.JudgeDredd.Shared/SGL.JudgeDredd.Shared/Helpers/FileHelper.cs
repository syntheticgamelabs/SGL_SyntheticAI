namespace SGL.JudgeDredd.Shared.Helpers;

public record FileInfoResult(
    string Name,
    long Size,
    string Extension,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    bool IsReadOnly);

public static class FileHelper
{
    public static bool SafeDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            var info = new FileInfo(path);
            if (info.IsReadOnly)
                info.IsReadOnly = false;

            File.Delete(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<bool> MoveToQuarantine(string filePath, string quarantinePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            Directory.CreateDirectory(quarantinePath);

            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var quarantinedName = $"{timestamp}_{fileName}.quarantined";
            var destinationPath = Path.Combine(quarantinePath, quarantinedName);

            using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, 65536, true);
            using var destStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            await sourceStream.CopyToAsync(destStream);

            File.Delete(filePath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static FileInfoResult? GetFileInfo(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var info = new FileInfo(path);
            return new FileInfoResult(
                Name: info.Name,
                Size: info.Length,
                Extension: info.Extension,
                CreatedAt: info.CreationTimeUtc,
                ModifiedAt: info.LastWriteTimeUtc,
                IsReadOnly: info.IsReadOnly);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
