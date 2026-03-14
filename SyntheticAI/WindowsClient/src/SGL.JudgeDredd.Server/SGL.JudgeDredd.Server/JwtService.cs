using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// Service responsible for generating and validating JWT tokens for client authentication.
/// Signs tokens using HMAC-SHA256. The signing key is persisted to data/jwt_secret.key
/// and generated automatically on first use.
/// </summary>
public sealed class JwtService
{
    private const string Issuer = "SGL.SyntheticAI.Server";
    private const string Audience = "SGL.SyntheticAI.Clients";
    private const int DefaultExpiryHours = 24;

    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TokenValidationParameters _validationParameters;

    public JwtService()
    {
        var keyBytes = LoadOrGenerateKey();
        _signingKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        _tokenHandler = new JwtSecurityTokenHandler();

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        SglLogger.Information("JwtService initialized. Signing key loaded.");
    }

    /// <summary>
    /// Generate a JWT token for an authenticated client.
    /// </summary>
    /// <param name="clientId">The unique device/client identifier (GUID).</param>
    /// <param name="username">The username of the client.</param>
    /// <param name="machineName">The machine/host name of the client device.</param>
    /// <param name="role">The role assigned to the client (default: "device").</param>
    /// <returns>A signed JWT token string.</returns>
    public string GenerateToken(Guid clientId, string username, string machineName, string role = "device")
    {
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim("user_id", username),
            new Claim("device_id", clientId.ToString()),
            new Claim("machine_name", machineName),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddHours(DefaultExpiryHours),
            IssuedAt = now,
            NotBefore = now,
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = _signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validate a JWT token and return the ClaimsPrincipal if valid.
    /// </summary>
    /// <param name="token">The JWT token string to validate.</param>
    /// <returns>A ClaimsPrincipal containing the token's claims, or null if validation fails.</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, _validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            SglLogger.Warning("JWT validation failed: token expired.");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            SglLogger.Warning("JWT validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            SglLogger.Warning("JWT validation failed with unexpected error: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Extract the client/device ID (GUID) from a JWT token.
    /// </summary>
    /// <param name="token">The JWT token string.</param>
    /// <returns>The client GUID if extraction succeeds, or null.</returns>
    public Guid? GetClientIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal == null)
            return null;

        var deviceIdClaim = principal.FindFirst("device_id");
        if (deviceIdClaim != null && Guid.TryParse(deviceIdClaim.Value, out var clientId))
            return clientId;

        return null;
    }

    /// <summary>
    /// Extract the username from a validated ClaimsPrincipal.
    /// </summary>
    public static string? GetUsernameFromPrincipal(ClaimsPrincipal principal)
    {
        return principal.FindFirst("user_id")?.Value;
    }

    /// <summary>
    /// Extract the device/client ID from a validated ClaimsPrincipal.
    /// </summary>
    public static Guid? GetClientIdFromPrincipal(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("device_id");
        if (claim != null && Guid.TryParse(claim.Value, out var id))
            return id;
        return null;
    }

    /// <summary>
    /// Load the HMAC signing key from disk, or generate and persist a new one.
    /// Key file location: data/jwt_secret.key (relative to application base directory).
    /// </summary>
    private static byte[] LoadOrGenerateKey()
    {
        var keyDir = Path.Combine(AppContext.BaseDirectory, "data");
        var keyPath = Path.Combine(keyDir, "jwt_secret.key");

        if (File.Exists(keyPath))
        {
            var existingKey = File.ReadAllBytes(keyPath);
            if (existingKey.Length >= 32)
            {
                SglLogger.Information("JWT signing key loaded from {Path}", keyPath);
                return existingKey;
            }

            SglLogger.Warning("Existing JWT key too short ({Length} bytes). Regenerating.", existingKey.Length);
        }

        // Generate a new 256-bit (32 byte) key for HMAC-SHA256
        var newKey = new byte[64]; // 512-bit key for extra strength
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(newKey);
        }

        Directory.CreateDirectory(keyDir);
        File.WriteAllBytes(keyPath, newKey);

        SglLogger.Information("Generated new JWT signing key at {Path}", keyPath);
        return newKey;
    }
}
