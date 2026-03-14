namespace SGL.JudgeDredd.App.Services.Auth;

public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasAcceptedEula { get; set; }
    public DateTime? EulaAcceptedAt { get; set; }

    /// <summary>
    /// The client application version (e.g. "2.0.1", "1.5.0-mobile").
    /// Updated on each login.
    /// </summary>
    public string ClientVersion { get; set; } = string.Empty;

    /// <summary>
    /// The platform/device type (e.g. "Windows PC", "Android", "iOS").
    /// Updated on each login.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// The IP address of the machine where this user account resides.
    /// Populated at runtime (not persisted).
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;
}
