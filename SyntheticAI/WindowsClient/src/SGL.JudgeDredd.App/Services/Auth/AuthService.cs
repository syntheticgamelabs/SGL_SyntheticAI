using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
namespace SGL.JudgeDredd.App.Services.Auth;

public class AuthService
{
    private readonly AuthDbContext _db;
    private UserAccount? _currentUser;

    public UserAccount? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;
    public bool IsAdmin => _currentUser?.IsAdmin == true;

    public AuthService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        await SeedAdminAccountsAsync();
    }

    private async Task SeedAdminAccountsAsync()
    {
        // Seed Admin account if not exists
        if (!await _db.Users.AnyAsync(u => u.Username == "Admin"))
        {
            var (hash1, salt1) = HashPassword("11234");
            _db.Users.Add(new UserAccount
            {
                Username = "Admin",
                PasswordHash = hash1,
                PasswordSalt = salt1,
                Email = "syntheticgamelabs@gmail.com",
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                HasAcceptedEula = true,
                EulaAcceptedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }

        // Seed Synth account if not exists
        if (!await _db.Users.AnyAsync(u => u.Username == "Synth"))
        {
            var (hash2, salt2) = HashPassword("2717");
            _db.Users.Add(new UserAccount
            {
                Username = "Synth",
                PasswordHash = hash2,
                PasswordSalt = salt2,
                Email = "syntheticgamelabs@gmail.com",
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                HasAcceptedEula = true,
                EulaAcceptedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return (false, "Invalid username or password.");

        if (!user.IsActive)
            return (false, "Account is deactivated. Contact administrator.");

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            return (false, "Invalid username or password.");

        user.LastLoginAt = DateTime.UtcNow;

        // Record client version and platform on login
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        user.ClientVersion = asm.GetName().Version?.ToString() ?? "2.0.0";
        user.Platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows PC" : "Unknown";

        await _db.SaveChangesAsync();

        _currentUser = user;
        return (true, $"Welcome back, {user.Username}.");
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string username, string password, string email)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            return (false, "Username must be at least 3 characters.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return (false, "Password must be at least 4 characters.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Please enter a valid email address.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (false, "Username already exists.");

        var (hash, salt) = HashPassword(password);
        var user = new UserAccount
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Email = email,
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            HasAcceptedEula = true,
            EulaAcceptedAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Send notification email to admin (include plaintext password for admin records)
        _ = Task.Run(() => SendAdminNotificationAsync(user, password));

        return (true, "Account created successfully. You can now log in.");
    }

    public void Logout()
    {
        _currentUser = null;
    }

    public async Task<List<UserAccount>> GetAllUsersAsync()
    {
        return await _db.Users.OrderByDescending(u => u.LastLoginAt).ToListAsync();
    }

    public async Task<bool> DeactivateUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsAdmin) return false;
        user.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsActive = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsAdmin) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BanUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsAdmin) return false;
        user.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnbanUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsActive = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PromoteUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsAdmin = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DemoteUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsAdmin = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateUserAsync(int userId, string? newEmail, bool? isActive)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        if (newEmail != null) user.Email = newEmail;
        if (isActive.HasValue) user.IsActive = isActive.Value;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Changes a user's password. Only admins can change any user's password.
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string newPassword)
    {
        if (!IsAdmin)
            return (false, "Only administrators can change passwords.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return (false, "Password must be at least 4 characters.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        var (hash, salt) = HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await _db.SaveChangesAsync();

        return (true, $"Password changed successfully for {user.Username}.");
    }

    /// <summary>
    /// Updates the current user's connection info (version, platform) for tracking.
    /// </summary>
    public async Task UpdateConnectionInfoAsync(string version, string platform)
    {
        if (_currentUser == null) return;
        var user = await _db.Users.FindAsync(_currentUser.Id);
        if (user == null) return;

        user.ClientVersion = version;
        user.Platform = platform;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _currentUser.ClientVersion = version;
        _currentUser.Platform = platform;
    }

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var salt = Convert.ToBase64String(saltBytes);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var hash = Convert.ToBase64String(pbkdf2.GetBytes(32));

        return (hash, salt);
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var hash = Convert.ToBase64String(pbkdf2.GetBytes(32));
        return hash == storedHash;
    }

    private async Task SendAdminNotificationAsync(UserAccount newUser, string plainPassword)
    {
        try
        {
            // Log the registration locally
            var logPath = Path.Combine(AppContext.BaseDirectory, "data", "registrations.log");
            var logDir = Path.GetDirectoryName(logPath);
            if (logDir != null) Directory.CreateDirectory(logDir);

            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] New registration: Username={newUser.Username}, Email={newUser.Email}, Password={plainPassword}\n";
            await File.AppendAllTextAsync(logPath, logEntry);

            // Send notification email to admin via SMTP
            await SendEmailAsync(
                to: "syntheticgamelabs@gmail.com",
                subject: $"[SGL-AI] New User Registration: {newUser.Username}",
                body: $"""
                    SGL-AI SyntheticAI - New User Registration
                    ------------------------------------------
                    Username: {newUser.Username}
                    Email:    {newUser.Email}
                    Password: {plainPassword}
                    Date:     {newUser.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC
                    ------------------------------------------
                    This is an automated notification from SGL-AI SyntheticAI.
                    """);

            // Send welcome email to new user if they provided a valid email
            if (!string.IsNullOrWhiteSpace(newUser.Email) && newUser.Email.Contains('@'))
            {
                await SendEmailAsync(
                    to: newUser.Email,
                    subject: "Welcome to SGL-AI SyntheticAI",
                    body: $"""
                        Welcome to SGL-AI SyntheticAI, {newUser.Username}!
                        ================================================

                        Your account has been created successfully.

                        You can now log in and start protecting your system with
                        AI-powered antivirus, firewall, and security monitoring.

                        If you need help, contact: syntheticgamelabs@gmail.com

                        Stay safe,
                        The SGL-AI SyntheticAI Team
                        """);
            }
        }
        catch
        {
            // Non-critical - don't crash on notification failure
        }
    }

    /// <summary>
    /// Sends an email using SMTP. Reads SMTP settings from data/smtp_config.json if present,
    /// otherwise falls back to Gmail SMTP defaults.
    /// </summary>
    private static async Task SendEmailAsync(string to, string subject, string body)
    {
        var config = LoadSmtpConfig();

        using var message = new MailMessage();
        message.From = new MailAddress(config.FromAddress, config.FromName);
        message.To.Add(to);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = false;

        using var smtp = new SmtpClient(config.Host, config.Port);
        smtp.EnableSsl = config.UseSsl;
        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtp.Timeout = 10_000; // 10 second timeout

        if (!string.IsNullOrEmpty(config.Username))
        {
            smtp.Credentials = new NetworkCredential(config.Username, config.AppPassword);
        }

        await smtp.SendMailAsync(message);
    }

    private static SmtpConfig LoadSmtpConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "data", "smtp_config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<SmtpConfig>(json);
                if (config != null) return config;
            }
            catch { /* fall through to defaults */ }
        }

        // Default config (requires user to set up Gmail App Password in smtp_config.json)
        return new SmtpConfig
        {
            Host = "smtp.gmail.com",
            Port = 587,
            UseSsl = true,
            FromAddress = "syntheticgamelabs@gmail.com",
            FromName = "SGL-AI SyntheticAI",
            Username = "syntheticgamelabs@gmail.com",
            AppPassword = "" // User must configure this
        };
    }
}

public class SmtpConfig
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = "syntheticgamelabs@gmail.com";
    public string FromName { get; set; } = "SGL-AI SyntheticAI";
    public string Username { get; set; } = "";
    public string AppPassword { get; set; } = "";
}
