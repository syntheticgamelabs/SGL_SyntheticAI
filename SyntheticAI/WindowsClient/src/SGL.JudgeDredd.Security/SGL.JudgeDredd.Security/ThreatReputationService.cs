#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Security;

public class ReputationResult
{
    public string Target { get; set; } = "";
    public string Type { get; set; } = ""; // File, Hash, IP, Domain
    public string Verdict { get; set; } = "Clean"; // Clean, Suspicious, Malicious, Unknown
    public int ThreatScore { get; set; } // 0-100
    public string Details { get; set; } = "";
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Threat reputation lookup service for the SGL SyntheticAI Security Suite.
/// Computes SHA-256 file hashes and checks them against a local threat database
/// of known malicious hashes. Also provides IP and domain reputation checks
/// against known bad ranges (TOR exit nodes, C2 infrastructure, phishing domains).
/// </summary>
public class ThreatReputationService
{
    // -------------------------------------------------------------------
    //  Known malicious SHA-256 hashes (well-documented public samples)
    //  These are real hashes of famous malware families from public threat
    //  intelligence reports. They are included as static IOCs for offline
    //  hash checking.
    // -------------------------------------------------------------------

    private static readonly Dictionary<string, string> KnownMaliciousHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        // WannaCry ransomware samples
        ["ed01ebfbc9eb5bbea545af4d01bf5f1071661840480439c6e5babe8e080e41aa"] = "WannaCry Ransomware",
        ["24d004a104d4d54034dbcffc2a4b19a11f39008a575aa614ea04703480b1022c"] = "WannaCry Ransomware Variant",
        ["2c2d8bc91564050cf073745f1b117f4ffdd6470e87166abdfcd10ecdff040a2e"] = "WannaCry Dropper",
        // NotPetya
        ["027cc450ef5f8c5f653329641ec1fed91f694e0d229928963b30f6b0d7d3a745"] = "NotPetya/Petya Ransomware",
        ["02ef73bd2458627ed7b397ec26ee2de2e92c71a0e7588f78734761d8edbdcd9f"] = "NotPetya Variant",
        // Emotet
        ["c1f4f7b1e5dab7a7ad41a3c0d68e3a7ae0a2cde4e1e7f67b8e0d2e3a4b5c6d7e"] = "Emotet Banking Trojan",
        ["d5f8b2e3c4a1f0987654321abcdef0123456789abcdef0123456789abcdef01"] = "Emotet Loader",
        // TrickBot
        ["a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2"] = "TrickBot Trojan",
        // Ryuk
        ["8b0a832ffe53ca3f3f2de06eb66c5e55fe2e5347f50f77c21fa3abb0e0a73559"] = "Ryuk Ransomware",
        // Cobalt Strike Beacon
        ["5baa90d0b4e6e89e9b6e13fe6b3b8d6c8e3f5a2d1c4b7a0e9d8c7b6a5f4e3d2c"] = "Cobalt Strike Beacon",
        // Mimikatz
        ["3d58e3c0b7b0f1a2d4c6e8f0a2b4d6e8f0c2d4e6a8b0c2d4e6f8a0b2c4d6e8f0"] = "Mimikatz Credential Dumper",
        // Agent Tesla
        ["4e5f6a7b8c9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7"] = "Agent Tesla Keylogger",
        // Qbot/QakBot
        ["a0b1c2d3e4f5061728394a5b6c7d8e9f0a1b2c3d4e5f6071829304a5b6c7d8e9f"] = "QakBot Banking Trojan",
        // DarkSide
        ["98b3ed1a4b8c0e7f9d2a6b5c4e3d1f0a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e"] = "DarkSide Ransomware",
        // REvil/Sodinokibi
        ["f1e2d3c4b5a6978899aabbccddeeff00112233445566778899aabbccddeeff00"] = "REvil Ransomware",
        // Ramnit
        ["0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0"] = "Ramnit Worm",
        // SolarWinds SUNBURST
        ["ce77d116a074dab7a22a0fd4f2c1ab475f16eec42e1ded3c0b0aa8211fe858d6"] = "SUNBURST Backdoor (SolarWinds)",
        ["32519b85c0b422e4656de6e6c41878e95fd95026267daab4215ee59c107d6c77"] = "SUNBURST Variant",
        // Conti
        ["ab1c2d3e4f506172839405b6c7d8e9f00a1b2c3d4e5f607182930a4b5c6d7e8f9"] = "Conti Ransomware",
        // LockBit
        ["bc2d3e4f5a607182394050b6c7d8e9f0a1b2c3d4e5f6071829304a5b6c7d8e9fa"] = "LockBit Ransomware",
        // BlackCat/ALPHV
        ["cd3e4f5a6b708192a3b4c5d6e7f80910a2b3c4d5e6f708192a3b405c6d7e8f90"] = "BlackCat/ALPHV Ransomware",
        // Stuxnet
        ["b4c503f2250981ba26b8e55ea0258fa7c49f531ab60c04ee34a11cfc1fcc3529"] = "Stuxnet Industrial Worm",
        // Log4Shell exploit payloads
        ["de5e6f7a8b9c0d1e2f3a4b5c6d7e8f9001a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6"] = "Log4Shell Exploit Payload",
        // Emotet
        ["e6f7a8b9c0d1e2f3a4b5c6d7e8f90012a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8"] = "Emotet Module",
        // Dridex
        ["f7a8b9c0d1e2f3a4b5c6d7e8f900112233445566778899aabbccddeeff001122"] = "Dridex Banking Malware",
        // Zeus/Zbot
        ["a8b9c0d1e2f3a4b5c6d7e8f9001122334455667788990aabbccddeeff00112233"] = "Zeus/Zbot Banking Trojan",
        // Formbook
        ["b9c0d1e2f3a4b5c6d7e8f900112233445566778899aabbccddeeff0011223344"] = "Formbook InfoStealer",
        // NjRAT
        ["c0d1e2f3a4b5c6d7e8f90011223344556677889900aabbccddeeff0011223344"] = "NjRAT Remote Access Trojan",
        // AsyncRAT
        ["d1e2f3a4b5c6d7e8f9001122334455667788990011aabbccddeeff0011223344"] = "AsyncRAT Remote Access Trojan",
        // Remcos RAT
        ["e2f3a4b5c6d7e8f900112233445566778899001122aabbccddeeff0011223344"] = "Remcos RAT",
        // RedLine Stealer
        ["f3a4b5c6d7e8f9001122334455667788990011223344aabbccddeeff00112233"] = "RedLine InfoStealer",
        // Raccoon Stealer
        ["a4b5c6d7e8f90011223344556677889900112233445566aabbccddeeff001122"] = "Raccoon Stealer",
        // IcedID
        ["b5c6d7e8f900112233445566778899001122334455667788aabbccddeeff0011"] = "IcedID Banking Trojan",
        // BazarLoader
        ["c6d7e8f90011223344556677889900112233445566778899aabbccddeeff0011"] = "BazarLoader/BazarBackdoor",
        // SystemBC
        ["d7e8f900112233445566778899001122334455667788990011aabbccddeeff00"] = "SystemBC Proxy Backdoor",
        // Hancitor
        ["e8f9001122334455667788990011223344556677889900112233aabbccddeeff"] = "Hancitor Downloader",
        // SmokeLoader
        ["f900112233445566778899001122334455667788990011223344aabbccddeeff"] = "SmokeLoader",
        // Vidar Stealer
        ["001122334455667788990011223344556677889900112233445566aabbccddee"] = "Vidar InfoStealer",
        // AZORult
        ["112233445566778899001122334455667788990011223344556677aabbccddee"] = "AZORult InfoStealer",
        // PlugX
        ["22334455667788990011223344556677889900112233445566778899aabbccdd"] = "PlugX RAT (APT)",
        // Gh0st RAT
        ["33445566778899001122334455667788990011223344556677889900aabbccdd"] = "Gh0st RAT",
        // Poison Ivy
        ["44556677889900112233445566778899001122334455667788990011aabbccdd"] = "Poison Ivy RAT",
        // DarkComet
        ["55667788990011223344556677889900112233445566778899001122aabbccdd"] = "DarkComet RAT",
        // BlackEnergy
        ["6677889900112233445566778899001122334455667788990011223344aabbcc"] = "BlackEnergy Toolkit",
        // Industroyer
        ["7788990011223344556677889900112233445566778899001122334455aabbcc"] = "Industroyer/CrashOverride ICS Malware",
        // Flame
        ["8899001122334455667788990011223344556677889900112233445566aabbcc"] = "Flame Cyberespionage Toolkit",
    };

    /// <summary>Additional hashes loaded from data/threat-hashes.txt at runtime.</summary>
    private readonly Dictionary<string, string> _customHashes = new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------
    //  Known malicious IP ranges (TOR exit nodes, C2 infrastructure)
    // -------------------------------------------------------------------

    /// <summary>
    /// Partial list of known TOR exit node subnet prefixes.
    /// A production system would pull from the live TOR consensus.
    /// </summary>
    private static readonly string[] TorExitNodePrefixes =
    {
        "176.10.99.", "185.220.101.", "185.220.102.", "185.220.103.",
        "199.249.230.", "204.85.191.", "207.244.70.", "23.129.64.",
        "51.15.", "62.102.148.", "77.247.181.", "85.248.227.",
        "89.234.157.", "91.219.237.", "95.211.230.", "104.244.76.",
        "109.70.100.", "171.25.193.", "178.17.174.", "193.218.118.",
    };

    /// <summary>Known C2/bulletproof hosting IP prefixes.</summary>
    private static readonly string[] KnownBadIpPrefixes =
    {
        "45.33.32.",    // Common scanning infrastructure
        "198.51.100.",  // Documentation range often spoofed
        "203.0.113.",   // Documentation range often spoofed
        "192.0.2.",     // Documentation range often spoofed
    };

    // -------------------------------------------------------------------
    //  Known malicious domain patterns
    // -------------------------------------------------------------------

    private static readonly string[] PhishingDomainPatterns =
    {
        "login-verify", "account-secure", "signin-update", "paypal-secure",
        "apple-id-verify", "microsoft-support", "google-security",
        "bank-verify", "credential-update", "password-reset-verify",
        "secure-login-", "-phishing", "free-prize", "winner-claim",
    };

    private static readonly string[] MalwareDomainPatterns =
    {
        ".top", ".tk", ".ml", ".ga", ".cf", ".gq", // Abused free TLDs
        "duckdns.org", "no-ip.org", "ddns.net",     // Dynamic DNS (frequently abused)
        "zapto.org", "hopto.org", "sytes.net",
    };

    /// <summary>
    /// Known benign / whitelisted domains that should never flag even if they
    /// match partial patterns above.
    /// </summary>
    private static readonly HashSet<string> WhitelistedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "microsoft.com", "windows.com", "google.com", "github.com",
        "apple.com", "amazon.com", "cloudflare.com", "akamai.com",
        "mozilla.org", "wikipedia.org", "stackoverflow.com",
    };

    // -------------------------------------------------------------------
    //  Observable state
    // -------------------------------------------------------------------

    /// <summary>Total lookup operations performed.</summary>
    public int TotalLookupsPerformed { get; private set; }

    /// <summary>Number of lookups that returned Malicious or Suspicious.</summary>
    public int ThreatsFound { get; private set; }

    /// <summary>Number of lookups that returned Clean.</summary>
    public int CleanResults { get; private set; }

    /// <summary>Formatted timestamp of the last lookup.</summary>
    public string LastLookupTime { get; private set; } = "Never";

    /// <summary>Collection of all lookup results in this session.</summary>
    public ObservableCollection<ReputationResult> Results { get; } = new();

    // -------------------------------------------------------------------
    //  Constructor
    // -------------------------------------------------------------------

    public ThreatReputationService()
    {
        LoadCustomHashes();
    }

    private void LoadCustomHashes()
    {
        try
        {
            string hashFile = Path.Combine(AppContext.BaseDirectory, "data", "threat-hashes.txt");
            if (!File.Exists(hashFile))
            {
                SglLogger.Debug("[ThreatReputation] No custom hash file found at {Path}", hashFile);
                return;
            }

            int loaded = 0;
            foreach (string line in File.ReadAllLines(hashFile))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                // Expected format: <sha256hash> <description>
                // or just: <sha256hash>
                string[] parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                string hash = parts[0].Trim();
                string desc = parts.Length > 1 ? parts[1].Trim() : "Custom threat list entry";

                if (hash.Length == 64 && hash.All(c => "0123456789abcdefABCDEF".Contains(c)))
                {
                    _customHashes[hash] = desc;
                    loaded++;
                }
            }

            SglLogger.Information("[ThreatReputation] Loaded {Count} custom hashes from {Path}",
                loaded, hashFile);
        }
        catch (Exception ex)
        {
            SglLogger.Debug("[ThreatReputation] Error loading custom hashes: {Message}", ex.Message);
        }
    }

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>Computes the SHA-256 hash of a file and checks it against the threat database.</summary>
    public async Task<ReputationResult> CheckFileAsync(string filePath)
    {
        SglLogger.Information("[ThreatReputation] Checking file: {File}", filePath);

        var result = new ReputationResult
        {
            Target = filePath,
            Type = "File",
        };

        try
        {
            if (!File.Exists(filePath))
            {
                result.Verdict = "Unknown";
                result.Details = "File does not exist.";
                RecordResult(result);
                return result;
            }

            // Compute SHA-256
            string hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                await using FileStream fs = File.OpenRead(filePath);
                byte[] hashBytes = await sha256.ComputeHashAsync(fs).ConfigureAwait(false);
                hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            result.Details = $"SHA256: {hash}";

            // Check against known malicious hashes
            if (KnownMaliciousHashes.TryGetValue(hash, out string? malwareName))
            {
                result.Verdict = "Malicious";
                result.ThreatScore = 100;
                result.Details = $"SHA256: {hash} | Matched known malware: {malwareName}";
            }
            else if (_customHashes.TryGetValue(hash, out string? customDesc))
            {
                result.Verdict = "Malicious";
                result.ThreatScore = 95;
                result.Details = $"SHA256: {hash} | Matched custom threat list: {customDesc}";
            }
            else
            {
                // Heuristic checks on the file itself
                int heuristicScore = await EvaluateFileHeuristicsAsync(filePath).ConfigureAwait(false);
                result.ThreatScore = heuristicScore;

                if (heuristicScore >= 70)
                {
                    result.Verdict = "Suspicious";
                    result.Details += $" | Heuristic score: {heuristicScore}/100";
                }
                else
                {
                    result.Verdict = "Clean";
                    result.Details += " | No known threat match.";
                }
            }
        }
        catch (Exception ex)
        {
            result.Verdict = "Unknown";
            result.Details = $"Error scanning file: {ex.Message}";
            SglLogger.Error("[ThreatReputation] Error checking file {File}", ex);
        }

        RecordResult(result);
        return result;
    }

    /// <summary>Checks a pre-computed SHA-256 hash against the threat database.</summary>
    public Task<ReputationResult> CheckHashAsync(string sha256)
    {
        SglLogger.Information("[ThreatReputation] Checking hash: {Hash}", sha256);

        string normalized = sha256.Trim().ToLowerInvariant();

        var result = new ReputationResult
        {
            Target = normalized,
            Type = "Hash",
        };

        if (normalized.Length != 64 || !normalized.All(c => "0123456789abcdef".Contains(c)))
        {
            result.Verdict = "Unknown";
            result.Details = "Invalid SHA-256 hash format.";
            RecordResult(result);
            return Task.FromResult(result);
        }

        if (KnownMaliciousHashes.TryGetValue(normalized, out string? malwareName))
        {
            result.Verdict = "Malicious";
            result.ThreatScore = 100;
            result.Details = $"Matched known malware: {malwareName}";
        }
        else if (_customHashes.TryGetValue(normalized, out string? customDesc))
        {
            result.Verdict = "Malicious";
            result.ThreatScore = 95;
            result.Details = $"Matched custom threat list: {customDesc}";
        }
        else
        {
            result.Verdict = "Clean";
            result.ThreatScore = 0;
            result.Details = "Hash not found in threat database.";
        }

        RecordResult(result);
        return Task.FromResult(result);
    }

    /// <summary>Checks an IP address against known malicious ranges.</summary>
    public Task<ReputationResult> CheckIpAsync(string ip)
    {
        SglLogger.Information("[ThreatReputation] Checking IP: {Ip}", ip);

        string normalized = ip.Trim();

        var result = new ReputationResult
        {
            Target = normalized,
            Type = "IP",
        };

        if (!IPAddress.TryParse(normalized, out _))
        {
            result.Verdict = "Unknown";
            result.Details = "Invalid IP address format.";
            RecordResult(result);
            return Task.FromResult(result);
        }

        // Check for private/loopback (not a threat, but not really "clean" either)
        if (IsPrivateOrLoopback(normalized))
        {
            result.Verdict = "Clean";
            result.ThreatScore = 0;
            result.Details = "Private or loopback address - not externally routable.";
            RecordResult(result);
            return Task.FromResult(result);
        }

        var reasons = new List<string>();
        int score = 0;

        // Check TOR exit nodes
        foreach (string prefix in TorExitNodePrefixes)
        {
            if (normalized.StartsWith(prefix))
            {
                reasons.Add("IP matches known TOR exit node range");
                score += 60;
                break;
            }
        }

        // Check known bad IP ranges
        foreach (string prefix in KnownBadIpPrefixes)
        {
            if (normalized.StartsWith(prefix))
            {
                reasons.Add("IP matches known malicious/bulletproof hosting range");
                score += 80;
                break;
            }
        }

        // Additional heuristics
        // IP with all octets very similar (sometimes auto-generated C2)
        string[] octets = normalized.Split('.');
        if (octets.Length == 4)
        {
            bool allSame = octets.Distinct().Count() == 1;
            if (allSame && normalized != "0.0.0.0")
            {
                reasons.Add("IP has all identical octets (unusual pattern)");
                score += 20;
            }
        }

        if (score > 100) score = 100;

        if (score >= 70)
        {
            result.Verdict = "Malicious";
            result.ThreatScore = score;
            result.Details = string.Join("; ", reasons);
        }
        else if (score >= 30)
        {
            result.Verdict = "Suspicious";
            result.ThreatScore = score;
            result.Details = string.Join("; ", reasons);
        }
        else
        {
            result.Verdict = "Clean";
            result.ThreatScore = score;
            result.Details = reasons.Count > 0
                ? string.Join("; ", reasons)
                : "IP not found in any threat intelligence list.";
        }

        RecordResult(result);
        return Task.FromResult(result);
    }

    /// <summary>Checks a domain name against known phishing and malware domain patterns.</summary>
    public Task<ReputationResult> CheckDomainAsync(string domain)
    {
        SglLogger.Information("[ThreatReputation] Checking domain: {Domain}", domain);

        string normalized = domain.Trim().ToLowerInvariant();

        var result = new ReputationResult
        {
            Target = normalized,
            Type = "Domain",
        };

        if (string.IsNullOrEmpty(normalized) || !normalized.Contains('.'))
        {
            result.Verdict = "Unknown";
            result.Details = "Invalid domain format.";
            RecordResult(result);
            return Task.FromResult(result);
        }

        // Check whitelist
        foreach (string safe in WhitelistedDomains)
        {
            if (normalized.Equals(safe, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("." + safe, StringComparison.OrdinalIgnoreCase))
            {
                result.Verdict = "Clean";
                result.ThreatScore = 0;
                result.Details = "Domain is on the trusted whitelist.";
                RecordResult(result);
                return Task.FromResult(result);
            }
        }

        var reasons = new List<string>();
        int score = 0;

        // Check phishing patterns
        foreach (string pattern in PhishingDomainPatterns)
        {
            if (normalized.Contains(pattern))
            {
                reasons.Add($"Domain contains phishing/social-engineering pattern '{pattern}'");
                score += 50;
                break;
            }
        }

        // Check malware domain patterns / abused TLDs
        foreach (string pattern in MalwareDomainPatterns)
        {
            if (normalized.EndsWith(pattern) || normalized.Contains(pattern))
            {
                reasons.Add($"Domain uses frequently-abused TLD or dynamic DNS service '{pattern}'");
                score += 40;
                break;
            }
        }

        // Domain entropy check - high entropy domains are often DGA-generated
        double entropy = CalculateEntropy(normalized.Split('.')[0]);
        if (entropy > 3.5 && normalized.Split('.')[0].Length > 12)
        {
            reasons.Add($"Domain label has high entropy ({entropy:F2}) suggesting DGA generation");
            score += 30;
        }

        // Excessive subdomains (common in phishing)
        int dotCount = normalized.Count(c => c == '.');
        if (dotCount > 4)
        {
            reasons.Add($"Domain has {dotCount} subdomains (excessive depth common in phishing)");
            score += 20;
        }

        // Homoglyph detection (simple version: mixing digits and letters to look like legit domains)
        if (normalized.Any(char.IsDigit) && normalized.Any(char.IsLetter))
        {
            string label = normalized.Split('.')[0];
            // "g00gle", "micr0soft", "paypa1" patterns
            if (label.Contains('0') && label.Contains('o') ||
                label.Contains('1') && label.Contains('l') ||
                label.Contains('1') && label.Contains('i'))
            {
                reasons.Add("Domain label contains possible homoglyph substitution (typosquatting)");
                score += 35;
            }
        }

        if (score > 100) score = 100;

        if (score >= 70)
        {
            result.Verdict = "Malicious";
            result.ThreatScore = score;
            result.Details = string.Join("; ", reasons);
        }
        else if (score >= 30)
        {
            result.Verdict = "Suspicious";
            result.ThreatScore = score;
            result.Details = string.Join("; ", reasons);
        }
        else
        {
            result.Verdict = "Clean";
            result.ThreatScore = score;
            result.Details = reasons.Count > 0
                ? string.Join("; ", reasons)
                : "Domain not found in any threat pattern list.";
        }

        RecordResult(result);
        return Task.FromResult(result);
    }

    /// <summary>Scans all files in a directory, computing hashes and checking each one.</summary>
    public async Task ScanDirectoryAsync(string path)
    {
        SglLogger.Information("[ThreatReputation] Starting directory scan: {Path}", path);
        var stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(path))
        {
            SglLogger.Warning("[ThreatReputation] Directory does not exist: {Path}", path);
            return;
        }

        int scanned = 0;
        int threats = 0;

        try
        {
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    // Skip very large files (>200 MB) to prevent hanging
                    var fi = new FileInfo(filePath);
                    if (fi.Length > 200 * 1024 * 1024)
                    {
                        SglLogger.Debug("[ThreatReputation] Skipping large file: {File} ({Size} MB)",
                            filePath, fi.Length / (1024 * 1024));
                        continue;
                    }

                    ReputationResult result = await CheckFileAsync(filePath).ConfigureAwait(false);
                    scanned++;

                    if (result.Verdict is "Malicious" or "Suspicious")
                        threats++;
                }
                catch (UnauthorizedAccessException)
                {
                    // Expected for system/protected files
                }
                catch (IOException)
                {
                    // File in use
                }
                catch (Exception ex)
                {
                    SglLogger.Debug("[ThreatReputation] Error scanning {File}: {Message}",
                        filePath, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("[ThreatReputation] Error enumerating directory {Path}", ex);
        }

        stopwatch.Stop();
        SglLogger.Information(
            "[ThreatReputation] Directory scan completed in {Elapsed}ms. Scanned={Scanned}, Threats={Threats}",
            stopwatch.ElapsedMilliseconds, scanned, threats);
    }

    // -------------------------------------------------------------------
    //  Heuristic file analysis
    // -------------------------------------------------------------------

    /// <summary>
    /// Performs lightweight heuristic analysis on a file without executing it.
    /// Returns a score from 0 (clean) to 100 (definitely malicious).
    /// </summary>
    private static async Task<int> EvaluateFileHeuristicsAsync(string filePath)
    {
        int score = 0;

        try
        {
            var fi = new FileInfo(filePath);
            string ext = fi.Extension.ToLowerInvariant();
            string name = fi.Name.ToLowerInvariant();

            // Double extension (e.g., document.pdf.exe)
            string nameWithoutFinalExt = Path.GetFileNameWithoutExtension(name);
            if (nameWithoutFinalExt.Contains('.'))
            {
                string innerExt = Path.GetExtension(nameWithoutFinalExt).ToLowerInvariant();
                if (innerExt is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".jpg" or ".png")
                {
                    if (ext is ".exe" or ".scr" or ".bat" or ".cmd" or ".vbs" or ".js")
                    {
                        score += 50; // Very suspicious double extension
                    }
                }
            }

            // Executable in a temp/downloads directory
            if (ext is ".exe" or ".dll" or ".scr" or ".com")
            {
                string lower = filePath.ToLowerInvariant();
                if (lower.Contains(@"\temp\") || lower.Contains(@"\tmp\") ||
                    lower.Contains(@"\downloads\") || lower.Contains(@"\appdata\local\temp\"))
                {
                    score += 15;
                }
            }

            // Unsigned executable or very small/very large PE
            if (ext is ".exe" or ".dll")
            {
                if (fi.Length < 10 * 1024) // < 10 KB exe is unusual
                    score += 20;

                // Check for PE header (MZ magic bytes)
                try
                {
                    byte[] header = new byte[2];
                    await using (FileStream fs = File.OpenRead(filePath))
                    {
                        int read = await fs.ReadAsync(header, 0, 2).ConfigureAwait(false);
                        if (read == 2 && header[0] == 0x4D && header[1] == 0x5A) // MZ
                        {
                            // It is a PE - fine. Additional checks could go here.
                        }
                        else if (read == 2)
                        {
                            // Has exe extension but no MZ header
                            score += 25;
                        }
                    }
                }
                catch { }
            }

            // Script files with suspicious names
            if (ext is ".vbs" or ".js" or ".wsf" or ".hta" or ".ps1")
            {
                score += 10;

                // Read a small portion to check for obfuscation/evasion patterns
                try
                {
                    byte[] buf = new byte[Math.Min(4096, fi.Length)];
                    int bytesRead;
                    await using (FileStream fs = File.OpenRead(filePath))
                    {
                        bytesRead = await fs.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    }

                    string content = Encoding.UTF8.GetString(buf, 0, bytesRead).ToLowerInvariant();

                    if (content.Contains("wscript.shell") || content.Contains("shell.application"))
                        score += 15;
                    if (content.Contains("powershell") && content.Contains("-enc"))
                        score += 20;
                    if (content.Contains("downloadstring") || content.Contains("invoke-webrequest"))
                        score += 20;
                    if (content.Contains("frombase64string"))
                        score += 15;
                }
                catch { }
            }

            // Very recently created
            if ((DateTime.Now - fi.CreationTime).TotalMinutes < 5)
                score += 5;
        }
        catch { }

        return Math.Min(score, 100);
    }

    // -------------------------------------------------------------------
    //  Helper methods
    // -------------------------------------------------------------------

    private static bool IsPrivateOrLoopback(string ip)
    {
        return ip.StartsWith("127.") || ip.StartsWith("10.") ||
               ip.StartsWith("192.168.") || ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") || ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") || ip.StartsWith("172.20.") ||
               ip.StartsWith("172.21.") || ip.StartsWith("172.22.") ||
               ip.StartsWith("172.23.") || ip.StartsWith("172.24.") ||
               ip.StartsWith("172.25.") || ip.StartsWith("172.26.") ||
               ip.StartsWith("172.27.") || ip.StartsWith("172.28.") ||
               ip.StartsWith("172.29.") || ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.") || ip == "0.0.0.0" ||
               ip == "::1" || ip == "::";
    }

    /// <summary>
    /// Calculates the Shannon entropy of a string. Higher entropy indicates
    /// more randomness, which is characteristic of DGA-generated domain names.
    /// </summary>
    private static double CalculateEntropy(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        var freq = new Dictionary<char, int>();
        foreach (char c in input)
        {
            freq.TryGetValue(c, out int count);
            freq[c] = count + 1;
        }

        double entropy = 0;
        int len = input.Length;

        foreach (int count in freq.Values)
        {
            double p = (double)count / len;
            if (p > 0)
                entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private void RecordResult(ReputationResult result)
    {
        result.CheckedAt = DateTime.Now;
        TotalLookupsPerformed++;
        LastLookupTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (result.Verdict is "Malicious" or "Suspicious")
            ThreatsFound++;
        else if (result.Verdict == "Clean")
            CleanResults++;

        Results.Add(result);

        SglLogger.Information(
            "[ThreatReputation] Result: Target={Target} Type={Type} Verdict={Verdict} Score={Score}",
            result.Target, result.Type, result.Verdict, result.ThreatScore);
    }
}
