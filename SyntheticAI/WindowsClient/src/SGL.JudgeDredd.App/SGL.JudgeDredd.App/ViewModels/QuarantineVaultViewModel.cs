using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class QuarantineItem : ObservableObject
{
    [ObservableProperty] private string _originalPath = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _threatName = string.Empty;
    [ObservableProperty] private string _severity = "Medium";
    [ObservableProperty] private DateTime _quarantinedAt;
    [ObservableProperty] private long _fileSize;
    [ObservableProperty] private string _sha256Hash = string.Empty;
    [ObservableProperty] private string _quarantinePath = string.Empty;
    [ObservableProperty] private string _status = "Quarantined";
    [ObservableProperty] private bool _isSelected;

    public string FileSizeFormatted => fileSize < 1024 ? $"{fileSize} B"
        : fileSize < 1024 * 1024 ? $"{fileSize / 1024.0:F1} KB"
        : $"{fileSize / (1024.0 * 1024):F1} MB";

    private long fileSize => FileSize;
}

public partial class QuarantineVaultViewModel : ViewModelBase
{
    private readonly string _quarantineDir;

    [ObservableProperty] private int _totalQuarantined;
    [ObservableProperty] private int _totalRestored;
    [ObservableProperty] private int _totalDeleted;
    [ObservableProperty] private string _vaultStatus = "Vault ready";
    [ObservableProperty] private string _vaultSize = "0 B";
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _searchFilter = string.Empty;

    public ObservableCollection<QuarantineItem> QuarantinedItems { get; } = [];
    public ObservableCollection<string> ActivityLog { get; } = [];

    public QuarantineVaultViewModel()
    {
        Title = "Quarantine Vault";
        _quarantineDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGL-JudgeDredd", "Quarantine");

        Directory.CreateDirectory(_quarantineDir);
        LoadQuarantinedItems();
        LogActivity("Quarantine Vault initialized");
    }

    [RelayCommand]
    private async Task QuarantineFileAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        IsProcessing = true;
        VaultStatus = $"Quarantining: {Path.GetFileName(filePath)}...";

        try
        {
            var fileName = Path.GetFileName(filePath);
            var hash = await ComputeHashAsync(filePath);
            var quarantineName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.qvault";
            var quarantinePath = Path.Combine(_quarantineDir, quarantineName);

            // Encrypt and move to quarantine
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var encrypted = EncryptData(fileBytes);
            await File.WriteAllBytesAsync(quarantinePath, encrypted);

            // Write metadata
            var metaPath = quarantinePath + ".meta";
            var meta = $"{filePath}\n{fileName}\n{hash}\n{fileBytes.Length}\n{DateTime.UtcNow:O}\nMalware.Generic";
            await File.WriteAllTextAsync(metaPath, meta);

            // Delete original
            File.Delete(filePath);

            var item = new QuarantineItem
            {
                OriginalPath = filePath,
                FileName = fileName,
                ThreatName = "Malware.Generic",
                Severity = "High",
                QuarantinedAt = DateTime.UtcNow,
                FileSize = fileBytes.Length,
                Sha256Hash = hash,
                QuarantinePath = quarantinePath,
                Status = "Quarantined",
            };
            QuarantinedItems.Add(item);
            TotalQuarantined = QuarantinedItems.Count;
            RecalcVaultSize();
            LogActivity($"Quarantined: {fileName} ({item.FileSizeFormatted})");

            VaultStatus = $"Successfully quarantined: {fileName}";
        }
        catch (Exception ex)
        {
            VaultStatus = $"Quarantine failed: {ex.Message}";
            LogActivity($"ERROR: Failed to quarantine {filePath} - {ex.Message}");
        }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private async Task RestoreFileAsync(QuarantineItem? item)
    {
        if (item is null) return;

        IsProcessing = true;
        VaultStatus = $"Restoring: {item.FileName}...";

        try
        {
            if (!File.Exists(item.QuarantinePath))
            {
                VaultStatus = "Quarantine file not found";
                return;
            }

            var encrypted = await File.ReadAllBytesAsync(item.QuarantinePath);
            var decrypted = DecryptData(encrypted);

            var restoreDir = Path.GetDirectoryName(item.OriginalPath);
            if (!string.IsNullOrEmpty(restoreDir))
                Directory.CreateDirectory(restoreDir);

            await File.WriteAllBytesAsync(item.OriginalPath, decrypted);

            // Cleanup quarantine files
            File.Delete(item.QuarantinePath);
            var metaPath = item.QuarantinePath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);

            item.Status = "Restored";
            QuarantinedItems.Remove(item);
            TotalRestored++;
            TotalQuarantined = QuarantinedItems.Count;
            RecalcVaultSize();
            LogActivity($"Restored: {item.FileName} -> {item.OriginalPath}");

            VaultStatus = $"Restored: {item.FileName}";

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble($"File restored: {item.FileName}");
        }
        catch (Exception ex)
        {
            VaultStatus = $"Restore failed: {ex.Message}";
            LogActivity($"ERROR: Restore failed for {item.FileName} - {ex.Message}");
        }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private async Task DeletePermanentlyAsync(QuarantineItem? item)
    {
        if (item is null) return;

        IsProcessing = true;
        try
        {
            if (File.Exists(item.QuarantinePath))
                File.Delete(item.QuarantinePath);
            var metaPath = item.QuarantinePath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);

            QuarantinedItems.Remove(item);
            TotalDeleted++;
            TotalQuarantined = QuarantinedItems.Count;
            RecalcVaultSize();
            LogActivity($"Permanently deleted: {item.FileName}");
            VaultStatus = $"Deleted: {item.FileName}";

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Threat permanently eliminated: {item.FileName}");
        }
        catch (Exception ex)
        {
            VaultStatus = $"Delete failed: {ex.Message}";
        }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var item in QuarantinedItems.ToList())
        {
            try
            {
                if (File.Exists(item.QuarantinePath)) File.Delete(item.QuarantinePath);
                var metaPath = item.QuarantinePath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
            catch { }
        }
        TotalDeleted += QuarantinedItems.Count;
        QuarantinedItems.Clear();
        TotalQuarantined = 0;
        RecalcVaultSize();
        LogActivity("Vault cleared - all quarantined items permanently deleted");
        VaultStatus = "Vault cleared";
    }

    [RelayCommand]
    private void RefreshVault()
    {
        LoadQuarantinedItems();
        VaultStatus = $"Vault refreshed - {TotalQuarantined} item(s)";
    }

    private void LoadQuarantinedItems()
    {
        QuarantinedItems.Clear();
        try
        {
            var metaFiles = Directory.GetFiles(_quarantineDir, "*.meta");
            foreach (var metaFile in metaFiles)
            {
                try
                {
                    var lines = File.ReadAllLines(metaFile);
                    if (lines.Length < 6) continue;

                    var qPath = metaFile[..^5]; // Remove .meta
                    if (!File.Exists(qPath)) continue;

                    QuarantinedItems.Add(new QuarantineItem
                    {
                        OriginalPath = lines[0],
                        FileName = lines[1],
                        Sha256Hash = lines[2],
                        FileSize = long.TryParse(lines[3], out var sz) ? sz : 0,
                        QuarantinedAt = DateTime.TryParse(lines[4], out var dt) ? dt : DateTime.UtcNow,
                        ThreatName = lines[5],
                        QuarantinePath = qPath,
                        Status = "Quarantined",
                    });
                }
                catch { }
            }
        }
        catch { }

        TotalQuarantined = QuarantinedItems.Count;
        RecalcVaultSize();
    }

    private void RecalcVaultSize()
    {
        try
        {
            var dir = new DirectoryInfo(_quarantineDir);
            var totalBytes = dir.EnumerateFiles("*.qvault").Sum(f => f.Length);
            VaultSize = totalBytes < 1024 ? $"{totalBytes} B"
                : totalBytes < 1024 * 1024 ? $"{totalBytes / 1024.0:F1} KB"
                : totalBytes < 1024L * 1024 * 1024 ? $"{totalBytes / (1024.0 * 1024):F1} MB"
                : $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB";
        }
        catch { VaultSize = "Unknown"; }
    }

    private void LogActivity(string msg) =>
        ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly byte[] _vaultKey = LoadOrGenerateVaultKey();

    private static byte[] LoadOrGenerateVaultKey()
    {
        var keyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGL-JudgeDredd", "vault.key");
        if (File.Exists(keyPath))
            return File.ReadAllBytes(keyPath);
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.WriteAllBytes(keyPath, key);
        return key;
    }

    private static byte[] EncryptData(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _vaultKey;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return result;
    }

    private static byte[] DecryptData(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _vaultKey;
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 16, data.Length - 16);
    }
}
