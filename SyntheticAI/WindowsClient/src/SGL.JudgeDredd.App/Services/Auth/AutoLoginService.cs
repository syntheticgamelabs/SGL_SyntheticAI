using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SGL.JudgeDredd.App.Services.Auth;

/// <summary>
/// Persists encrypted login credentials using DPAPI (Windows Data Protection API).
/// Credentials are encrypted per-user and cannot be decrypted by other users on the machine.
/// </summary>
public class AutoLoginService
{
    private readonly string _credentialFilePath;

    public AutoLoginService()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _credentialFilePath = Path.Combine(dataDir, "auto_login.dat");
    }

    public bool HasSavedCredentials()
    {
        return File.Exists(_credentialFilePath);
    }

    public void SaveCredentials(string username, string password)
    {
        var payload = $"{username}\n{password}";
        var plainBytes = Encoding.UTF8.GetBytes(payload);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_credentialFilePath, encryptedBytes);
    }

    public (string username, string password)? LoadCredentials()
    {
        if (!File.Exists(_credentialFilePath))
            return null;

        try
        {
            var encryptedBytes = File.ReadAllBytes(_credentialFilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var payload = Encoding.UTF8.GetString(plainBytes);

            var parts = payload.Split('\n', 2);
            if (parts.Length == 2)
                return (parts[0], parts[1]);

            return null;
        }
        catch
        {
            // Corrupted or tampered file - remove it
            ClearCredentials();
            return null;
        }
    }

    public void ClearCredentials()
    {
        try
        {
            if (File.Exists(_credentialFilePath))
                File.Delete(_credentialFilePath);
        }
        catch { /* best effort */ }
    }
}
