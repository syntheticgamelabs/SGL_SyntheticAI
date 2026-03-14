namespace SGL.JudgeDredd.Shared;

/// <summary>
/// Single source of truth for all version strings across the entire application.
/// Update these constants when bumping versions — all endpoints, installers, and UI
/// will automatically pick up the changes.
/// </summary>
public static class VersionInfo
{
    /// <summary>Server / desktop application version.</summary>
    public const string ServerVersion = "1.1.39";

    /// <summary>Mobile application version.</summary>
    public const string MobileVersion = "3.6.0";

    /// <summary>
    /// Signature database version. Computed dynamically from the last-updated signature
    /// when available, but this constant is used as a fallback.
    /// </summary>
    public const string DefaultSignatureVersion = "2026.03.14.1";

    /// <summary>Full display name.</summary>
    public const string ProductName = "SGL SyntheticAI";

    /// <summary>Publisher name.</summary>
    public const string Publisher = "Synthetic Game Labs";

    /// <summary>Copyright notice.</summary>
    public const string Copyright = "Copyright (c) 2024-2026 Synthetic Game Labs. All rights reserved.";
}
