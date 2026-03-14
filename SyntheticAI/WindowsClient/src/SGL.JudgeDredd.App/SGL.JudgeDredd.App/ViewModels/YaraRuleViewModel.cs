using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

// ────────────────────────────────────────────────────────────────────────────
// Supporting types
// ────────────────────────────────────────────────────────────────────────────

public enum YaraStringType
{
    Text,
    Hex,
    Regex
}

public enum YaraRuleSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents a single string / pattern entry inside a YARA rule.
/// </summary>
public partial class YaraStringEntry : ObservableObject
{
    [ObservableProperty] private string _identifier = "$s1";
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private YaraStringType _type = YaraStringType.Text;
    [ObservableProperty] private bool _isNocase;
    [ObservableProperty] private bool _isWide;
    [ObservableProperty] private bool _isAscii = true;
    [ObservableProperty] private bool _isFullword;
    [ObservableProperty] private bool _isMatched;
}

/// <summary>
/// Represents a complete YARA-style detection rule with name, strings, condition,
/// and metadata.
/// </summary>
public partial class YaraRule : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _author = "SGL SyntheticAI";
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private YaraRuleSeverity _severity = YaraRuleSeverity.Medium;
    [ObservableProperty] private string _condition = "any of them";
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _matchCount;
    [ObservableProperty] private DateTime _dateCreated = DateTime.Now;
    [ObservableProperty] private DateTime? _lastMatch;
    [ObservableProperty] private bool _isValid = true;
    [ObservableProperty] private string _validationError = string.Empty;

    public ObservableCollection<YaraStringEntry> Strings { get; set; } = [];

    /// <summary>Clone this rule into a detached copy suitable for editing.</summary>
    public YaraRule Clone()
    {
        var clone = new YaraRule
        {
            Name = Name,
            Description = Description,
            Author = Author,
            Tags = Tags,
            Severity = Severity,
            Condition = Condition,
            IsEnabled = IsEnabled,
            MatchCount = MatchCount,
            DateCreated = DateCreated,
            LastMatch = LastMatch,
            IsValid = IsValid,
            ValidationError = ValidationError,
        };
        foreach (var s in Strings)
        {
            clone.Strings.Add(new YaraStringEntry
            {
                Identifier = s.Identifier,
                Value = s.Value,
                Type = s.Type,
                IsNocase = s.IsNocase,
                IsWide = s.IsWide,
                IsAscii = s.IsAscii,
                IsFullword = s.IsFullword,
            });
        }
        return clone;
    }
}

/// <summary>
/// Result object returned when a YARA rule matches a scanned file.
/// </summary>
public partial class YaraMatchResult : ObservableObject
{
    [ObservableProperty] private string _ruleName = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _matchedStrings = string.Empty;
    [ObservableProperty] private YaraRuleSeverity _severity = YaraRuleSeverity.Medium;
    [ObservableProperty] private DateTime _detectedAt = DateTime.Now;
    [ObservableProperty] private int _matchedStringCount;
}

// ────────────────────────────────────────────────────────────────────────────
// Serialization helpers -- these DTOs map to/from .yar-compatible JSON
// ────────────────────────────────────────────────────────────────────────────
internal sealed class YaraStringDto
{
    public string Identifier { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
    public bool Nocase { get; set; }
    public bool Wide { get; set; }
    public bool Ascii { get; set; } = true;
    public bool Fullword { get; set; }
}

internal sealed class YaraRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Condition { get; set; } = "any of them";
    public bool Enabled { get; set; } = true;
    public DateTime DateCreated { get; set; } = DateTime.Now;
    public List<YaraStringDto> Strings { get; set; } = [];
}

internal sealed class YaraRuleSetDto
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public List<YaraRuleDto> Rules { get; set; } = [];
}

// ────────────────────────────────────────────────────────────────────────────
// ViewModel
// ────────────────────────────────────────────────────────────────────────────

public partial class YaraRuleViewModel : ViewModelBase
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SGL-JudgeDredd", "YaraRules");

    private static readonly string DefaultRulesFile = Path.Combine(AppDataDir, "rules.yar");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Collections ──────────────────────────────────────────────────────
    public ObservableCollection<YaraRule> Rules { get; } = [];
    public ObservableCollection<YaraMatchResult> MatchResults { get; } = [];
    public ObservableCollection<string> Log { get; } = [];
    public ObservableCollection<YaraStringEntry> EditorStrings { get; } = [];

    // ── Statistics ────────────────────────────────────────────────────────
    [ObservableProperty] private int _totalRules;
    [ObservableProperty] private int _enabledRules;
    [ObservableProperty] private int _totalMatches;
    [ObservableProperty] private string _lastScanTime = "Never";

    // ── Rule editor properties ───────────────────────────────────────────
    [ObservableProperty] private YaraRule? _selectedRule;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editorName = string.Empty;
    [ObservableProperty] private string _editorDescription = string.Empty;
    [ObservableProperty] private string _editorAuthor = "SGL SyntheticAI";
    [ObservableProperty] private string _editorTags = string.Empty;
    [ObservableProperty] private YaraRuleSeverity _editorSeverity = YaraRuleSeverity.Medium;
    [ObservableProperty] private string _editorCondition = "any of them";
    [ObservableProperty] private string _editorValidation = string.Empty;
    [ObservableProperty] private bool _editorIsNew;

    // ── Test / scan ──────────────────────────────────────────────────────
    [ObservableProperty] private string _testFilePath = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isScanning;

    // ── Severity items for ComboBox binding ──────────────────────────────
    public YaraRuleSeverity[] SeverityValues { get; } = Enum.GetValues<YaraRuleSeverity>();
    public YaraStringType[] StringTypeValues { get; } = Enum.GetValues<YaraStringType>();

    // ── Search / filter ──────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    // ── Constructor ──────────────────────────────────────────────────────

    public YaraRuleViewModel()
    {
        Title = "YARA Rules";
        Directory.CreateDirectory(AppDataDir);

        if (!LoadRulesFromDisk())
        {
            LoadBuiltInRules();
            SaveRulesToDisk();
        }

        RefreshStats();
        LogEntry("YARA rule engine initialized");
    }

    // ════════════════════════════════════════════════════════════════════
    //  COMMANDS
    // ════════════════════════════════════════════════════════════════════

    // ── New / Edit / Delete ──────────────────────────────────────────────

    [RelayCommand]
    private void NewRule()
    {
        EditorIsNew = true;
        EditorName = $"custom_rule_{Rules.Count + 1}";
        EditorDescription = string.Empty;
        EditorAuthor = "SGL SyntheticAI";
        EditorTags = string.Empty;
        EditorSeverity = YaraRuleSeverity.Medium;
        EditorCondition = "any of them";
        EditorValidation = string.Empty;
        EditorStrings.Clear();
        EditorStrings.Add(new YaraStringEntry { Identifier = "$s1", Value = string.Empty, Type = YaraStringType.Text });
        IsEditing = true;
        LogEntry("Opened editor for new rule");
    }

    [RelayCommand]
    private void EditRule(YaraRule? rule)
    {
        if (rule is null) return;

        SelectedRule = rule;
        EditorIsNew = false;
        EditorName = rule.Name;
        EditorDescription = rule.Description;
        EditorAuthor = rule.Author;
        EditorTags = rule.Tags;
        EditorSeverity = rule.Severity;
        EditorCondition = rule.Condition;
        EditorValidation = string.Empty;
        EditorStrings.Clear();
        foreach (var s in rule.Strings)
        {
            EditorStrings.Add(new YaraStringEntry
            {
                Identifier = s.Identifier,
                Value = s.Value,
                Type = s.Type,
                IsNocase = s.IsNocase,
                IsWide = s.IsWide,
                IsAscii = s.IsAscii,
                IsFullword = s.IsFullword,
            });
        }

        IsEditing = true;
        LogEntry($"Editing rule: {rule.Name}");
    }

    [RelayCommand]
    private void SaveRule()
    {
        var error = ValidateEditor();
        if (!string.IsNullOrEmpty(error))
        {
            EditorValidation = error;
            return;
        }

        YaraRule target;

        if (EditorIsNew)
        {
            target = new YaraRule { DateCreated = DateTime.Now };
            Rules.Add(target);
        }
        else
        {
            target = SelectedRule!;
        }

        target.Name = EditorName.Trim();
        target.Description = EditorDescription?.Trim() ?? string.Empty;
        target.Author = EditorAuthor?.Trim() ?? "SGL SyntheticAI";
        target.Tags = EditorTags?.Trim() ?? string.Empty;
        target.Severity = EditorSeverity;
        target.Condition = EditorCondition.Trim();
        target.IsValid = true;
        target.ValidationError = string.Empty;
        target.Strings.Clear();
        foreach (var s in EditorStrings)
        {
            target.Strings.Add(new YaraStringEntry
            {
                Identifier = s.Identifier,
                Value = s.Value,
                Type = s.Type,
                IsNocase = s.IsNocase,
                IsWide = s.IsWide,
                IsAscii = s.IsAscii,
                IsFullword = s.IsFullword,
            });
        }

        IsEditing = false;
        SaveRulesToDisk();
        RefreshStats();
        LogEntry($"Saved rule: {target.Name}");
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        EditorValidation = string.Empty;
    }

    [RelayCommand]
    private void DeleteRule(YaraRule? rule)
    {
        if (rule is null) return;
        Rules.Remove(rule);
        if (SelectedRule == rule)
        {
            IsEditing = false;
            SelectedRule = null;
        }
        SaveRulesToDisk();
        RefreshStats();
        LogEntry($"Deleted rule: {rule.Name}");
    }

    [RelayCommand]
    private void ToggleRule(YaraRule? rule)
    {
        if (rule is null) return;
        rule.IsEnabled = !rule.IsEnabled;
        SaveRulesToDisk();
        RefreshStats();
    }

    // ── String entries in editor ─────────────────────────────────────────

    [RelayCommand]
    private void AddString()
    {
        var nextIndex = EditorStrings.Count + 1;
        EditorStrings.Add(new YaraStringEntry
        {
            Identifier = $"$s{nextIndex}",
            Value = string.Empty,
            Type = YaraStringType.Text,
        });
    }

    [RelayCommand]
    private void RemoveString(YaraStringEntry? entry)
    {
        if (entry is null) return;
        EditorStrings.Remove(entry);
    }

    // ── Condition presets ────────────────────────────────────────────────

    [RelayCommand]
    private void SetCondition(string? preset)
    {
        if (string.IsNullOrEmpty(preset)) return;
        EditorCondition = preset;
    }

    // ── Test single rule against file ────────────────────────────────────

    [RelayCommand]
    private async Task TestRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(TestFilePath))
        {
            TestResult = "Please enter a file path to test.";
            return;
        }

        if (!File.Exists(TestFilePath))
        {
            TestResult = $"File not found: {TestFilePath}";
            return;
        }

        if (SelectedRule is null && !IsEditing)
        {
            TestResult = "Select a rule or create a new one before testing.";
            return;
        }

        IsScanning = true;
        TestResult = "Scanning...";

        try
        {
            // Build a temporary rule from the editor if we are editing, else use selected
            var rule = IsEditing ? BuildRuleFromEditor() : SelectedRule!;

            var bytes = await File.ReadAllBytesAsync(TestFilePath);
            var matched = EvaluateRule(rule, bytes);

            if (matched)
            {
                rule.MatchCount++;
                rule.LastMatch = DateTime.Now;
                var matchedIds = rule.Strings.Where(s => s.IsMatched).Select(s => s.Identifier);
                TestResult = $"MATCH - Rule \"{rule.Name}\" triggered on {Path.GetFileName(TestFilePath)}\n" +
                             $"Matched strings: {string.Join(", ", matchedIds)}";
            }
            else
            {
                TestResult = $"NO MATCH - Rule \"{rule.Name}\" did not match {Path.GetFileName(TestFilePath)}";
            }

            LastScanTime = DateTime.Now.ToString("HH:mm:ss");
            LogEntry($"Test: {rule.Name} vs {Path.GetFileName(TestFilePath)} => {(matched ? "MATCH" : "NO MATCH")}");
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
            LogEntry($"Test error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    // ── Test ALL enabled rules against file ──────────────────────────────

    [RelayCommand]
    private async Task TestAllRulesAsync()
    {
        if (string.IsNullOrWhiteSpace(TestFilePath))
        {
            TestResult = "Please enter a file path to test.";
            return;
        }

        if (!File.Exists(TestFilePath))
        {
            TestResult = $"File not found: {TestFilePath}";
            return;
        }

        IsScanning = true;
        MatchResults.Clear();
        TestResult = "Scanning with all enabled rules...";

        try
        {
            var bytes = await File.ReadAllBytesAsync(TestFilePath);
            int matchCount = 0;
            var sb = new StringBuilder();

            foreach (var rule in Rules.Where(r => r.IsEnabled))
            {
                var matched = EvaluateRule(rule, bytes);
                if (matched)
                {
                    matchCount++;
                    rule.MatchCount++;
                    rule.LastMatch = DateTime.Now;

                    var matchedIds = rule.Strings.Where(s => s.IsMatched).Select(s => s.Identifier);
                    MatchResults.Add(new YaraMatchResult
                    {
                        RuleName = rule.Name,
                        FilePath = TestFilePath,
                        Severity = rule.Severity,
                        MatchedStringCount = rule.Strings.Count(s => s.IsMatched),
                        MatchedStrings = string.Join(", ", matchedIds),
                        DetectedAt = DateTime.Now,
                    });

                    sb.AppendLine($"  [{rule.Severity}] {rule.Name} ({string.Join(", ", matchedIds)})");
                }
            }

            TotalMatches += matchCount;
            LastScanTime = DateTime.Now.ToString("HH:mm:ss");

            TestResult = matchCount > 0
                ? $"DETECTED - {matchCount} rule(s) matched against {Path.GetFileName(TestFilePath)}:\n{sb}"
                : $"CLEAN - No rules matched {Path.GetFileName(TestFilePath)}";

            LogEntry($"Full scan: {Path.GetFileName(TestFilePath)} => {matchCount} match(es)");
            RefreshStats();
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
            LogEntry($"Full scan error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    // ── Scan a file against all enabled rules (callable from other VMs) ──

    public async Task<List<YaraMatchResult>> ScanFileAsync(string filePath)
    {
        var results = new List<YaraMatchResult>();
        if (!File.Exists(filePath)) return results;

        var bytes = await File.ReadAllBytesAsync(filePath);

        foreach (var rule in Rules.Where(r => r.IsEnabled))
        {
            if (EvaluateRule(rule, bytes))
            {
                rule.MatchCount++;
                rule.LastMatch = DateTime.Now;

                var matchedIds = rule.Strings.Where(s => s.IsMatched).Select(s => s.Identifier);
                results.Add(new YaraMatchResult
                {
                    RuleName = rule.Name,
                    FilePath = filePath,
                    Severity = rule.Severity,
                    MatchedStringCount = rule.Strings.Count(s => s.IsMatched),
                    MatchedStrings = string.Join(", ", matchedIds),
                    DetectedAt = DateTime.Now,
                });
            }
        }

        TotalMatches += results.Count;
        LastScanTime = DateTime.Now.ToString("HH:mm:ss");
        RefreshStats();
        return results;
    }

    // ── Import / Export ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task ImportRulesAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "YARA Rule Files (*.yar;*.json)|*.yar;*.json|All Files (*.*)|*.*",
                Title = "Import YARA Rules",
                Multiselect = false,
            };

            if (dialog.ShowDialog() != true) return;

            var fileContent = await File.ReadAllTextAsync(dialog.FileName);
            List<YaraRule>? parsedRules = null;

            // Try JSON format first
            try
            {
                var dto = JsonSerializer.Deserialize<YaraRuleSetDto>(fileContent, JsonOpts);
                if (dto is not null && dto.Rules.Count > 0)
                {
                    parsedRules = dto.Rules.Select(DtoToRule).ToList();
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, will try YARA text format below
            }

            // Fall back to standard YARA text format (.yar)
            if (parsedRules is null || parsedRules.Count == 0)
            {
                parsedRules = ParseYaraTextFormat(fileContent);
            }

            if (parsedRules is null || parsedRules.Count == 0)
            {
                LogEntry("Import failed: no rules found in file");
                return;
            }

            int imported = 0;
            foreach (var rule in parsedRules)
            {
                // Skip duplicates by name
                if (Rules.Any(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Rules.Add(rule);
                imported++;
            }

            SaveRulesToDisk();
            RefreshStats();
            LogEntry($"Imported {imported} rule(s) from {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            LogEntry($"Import error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportRulesAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "YARA Rule Files (*.yar)|*.yar|JSON Files (*.json)|*.json",
                Title = "Export YARA Rules",
                FileName = $"sgl_yara_rules_{DateTime.Now:yyyyMMdd}.yar",
            };

            if (dialog.ShowDialog() != true) return;

            var dto = new YaraRuleSetDto
            {
                ExportedAt = DateTime.Now,
                Rules = Rules.Select(RuleToDto).ToList(),
            };

            var json = JsonSerializer.Serialize(dto, JsonOpts);
            await File.WriteAllTextAsync(dialog.FileName, json);
            LogEntry($"Exported {Rules.Count} rule(s) to {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            LogEntry($"Export error: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  PATTERN-MATCHING ENGINE
    // ════════════════════════════════════════════════════════════════════

    // ── Hex pattern token types ─────────────────────────────────────────
    private enum HexTokenType { Byte, Wildcard, Jump, Alternation }
    private sealed class HexToken
    {
        public HexTokenType Type;
        public byte ByteValue;
        public int JumpMin, JumpMax;
        public List<List<HexToken>>? Alternatives;
    }

    /// <summary>
    /// Evaluate a single YARA rule against a span of file bytes.
    /// Returns true if the rule's condition is satisfied.
    /// Tracks match positions for the 'at' operator and applies fullword checking.
    /// </summary>
    private static bool EvaluateRule(YaraRule rule, byte[] fileBytes)
    {
        // Reset match state
        foreach (var s in rule.Strings)
            s.IsMatched = false;

        var matchPositions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        // Evaluate each string entry and collect match positions
        foreach (var entry in rule.Strings)
        {
            var positions = entry.Type switch
            {
                YaraStringType.Hex => FindHexMatchPositions(entry.Value, fileBytes),
                YaraStringType.Text => FindTextMatchPositions(entry, fileBytes),
                YaraStringType.Regex => FindRegexMatchPositions(entry.Value, fileBytes),
                _ => new List<int>(),
            };

            matchPositions[entry.Identifier] = positions;
            entry.IsMatched = positions.Count > 0;
        }

        // Evaluate condition with file size and match positions
        return EvaluateCondition(rule.Condition, rule.Strings, fileBytes.Length, matchPositions);
    }

    // ── Hex pattern matching (with jumps and alternation) ───────────────

    /// <summary>
    /// Find all match positions for a hex pattern in the data.
    /// Supports: literal bytes, ?? wildcards, [N-M] variable-length jumps,
    /// and ( AA | BB CC ) alternation groups.
    /// </summary>
    private static List<int> FindHexMatchPositions(string pattern, byte[] data)
    {
        var positions = new List<int>();
        if (string.IsNullOrWhiteSpace(pattern)) return positions;

        var cleaned = pattern.Replace("{", "").Replace("}", "").Trim();
        var tokens = TokenizeHexPattern(cleaned);
        if (tokens.Count == 0) return positions;

        for (int offset = 0; offset < data.Length; offset++)
        {
            if (MatchHexTokensAt(tokens, 0, data, offset))
                positions.Add(offset);
        }
        return positions;
    }

    /// <summary>
    /// Match a hex pattern against raw bytes (legacy bool API).
    /// Supports ?? wildcards, [N-M] variable-length jumps, and ( AA | BB CC ) alternation.
    /// </summary>
    private static bool MatchHexPattern(string pattern, byte[] data)
    {
        return FindHexMatchPositions(pattern, data).Count > 0;
    }

    /// <summary>
    /// Tokenize a hex pattern string into structured tokens.
    /// Handles: hex bytes, ?/?? wildcards, [N-M]/[N] jumps, ( A | B ) alternation groups.
    /// </summary>
    private static List<HexToken> TokenizeHexPattern(string cleaned)
    {
        var tokens = new List<HexToken>();
        int i = 0;

        while (i < cleaned.Length)
        {
            // Skip whitespace
            while (i < cleaned.Length && char.IsWhiteSpace(cleaned[i])) i++;
            if (i >= cleaned.Length) break;

            if (cleaned[i] == '[')
            {
                // Jump: [N-M] or [N]
                int end = cleaned.IndexOf(']', i);
                if (end == -1) break;
                var jumpStr = cleaned[(i + 1)..end].Trim();
                var dashIdx = jumpStr.IndexOf('-');
                if (dashIdx >= 0)
                {
                    if (int.TryParse(jumpStr[..dashIdx].Trim(), out int min) &&
                        int.TryParse(jumpStr[(dashIdx + 1)..].Trim(), out int max))
                    {
                        tokens.Add(new HexToken { Type = HexTokenType.Jump, JumpMin = min, JumpMax = max });
                    }
                }
                else
                {
                    if (int.TryParse(jumpStr, out int exact))
                    {
                        tokens.Add(new HexToken { Type = HexTokenType.Jump, JumpMin = exact, JumpMax = exact });
                    }
                }
                i = end + 1;
            }
            else if (cleaned[i] == '(')
            {
                // Alternation group: ( AA BB | CC DD )
                int depth = 1;
                int start = i + 1;
                i++;
                while (i < cleaned.Length && depth > 0)
                {
                    if (cleaned[i] == '(') depth++;
                    else if (cleaned[i] == ')') depth--;
                    i++;
                }
                var groupStr = cleaned[start..(i - 1)].Trim();
                var alternatives = SplitAlternation(groupStr);
                var altToken = new HexToken
                {
                    Type = HexTokenType.Alternation,
                    Alternatives = new List<List<HexToken>>()
                };
                foreach (var alt in alternatives)
                {
                    altToken.Alternatives.Add(TokenizeHexPattern(alt.Trim()));
                }
                tokens.Add(altToken);
            }
            else if (cleaned[i] == '?' && i + 1 < cleaned.Length && cleaned[i + 1] == '?')
            {
                tokens.Add(new HexToken { Type = HexTokenType.Wildcard });
                i += 2;
            }
            else if (cleaned[i] == '?')
            {
                tokens.Add(new HexToken { Type = HexTokenType.Wildcard });
                i++;
            }
            else if (IsHexChar(cleaned[i]))
            {
                // Read two hex characters as one byte
                if (i + 1 < cleaned.Length && IsHexChar(cleaned[i + 1]))
                {
                    try
                    {
                        byte val = Convert.ToByte(cleaned.Substring(i, 2), 16);
                        tokens.Add(new HexToken { Type = HexTokenType.Byte, ByteValue = val });
                    }
                    catch { /* skip invalid */ }
                    i += 2;
                }
                else
                {
                    i++; // skip lone hex char
                }
            }
            else
            {
                i++; // skip unknown character
            }
        }

        return tokens;
    }

    /// <summary>Split alternation group contents on '|', respecting nested parentheses.</summary>
    private static List<string> SplitAlternation(string groupContent)
    {
        var parts = new List<string>();
        int depth = 0;
        int lastSplit = 0;

        for (int i = 0; i < groupContent.Length; i++)
        {
            if (groupContent[i] == '(') depth++;
            else if (groupContent[i] == ')') depth--;
            else if (groupContent[i] == '|' && depth == 0)
            {
                parts.Add(groupContent[lastSplit..i]);
                lastSplit = i + 1;
            }
        }
        parts.Add(groupContent[lastSplit..]);
        return parts;
    }

    /// <summary>Check if a character is a valid hexadecimal digit.</summary>
    private static bool IsHexChar(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    /// <summary>
    /// Recursively match hex tokens against data starting at a given offset.
    /// Supports backtracking for variable-length jumps and alternation groups.
    /// </summary>
    private static bool MatchHexTokensAt(List<HexToken> tokens, int tokenIndex, byte[] data, int dataOffset)
    {
        while (tokenIndex < tokens.Count)
        {
            var token = tokens[tokenIndex];

            switch (token.Type)
            {
                case HexTokenType.Byte:
                    if (dataOffset >= data.Length) return false;
                    if (data[dataOffset] != token.ByteValue) return false;
                    dataOffset++;
                    tokenIndex++;
                    break;

                case HexTokenType.Wildcard:
                    if (dataOffset >= data.Length) return false;
                    dataOffset++;
                    tokenIndex++;
                    break;

                case HexTokenType.Jump:
                    // Try all valid jump lengths via backtracking
                    for (int len = token.JumpMin; len <= token.JumpMax; len++)
                    {
                        if (dataOffset + len > data.Length) break;
                        if (MatchHexTokensAt(tokens, tokenIndex + 1, data, dataOffset + len))
                            return true;
                    }
                    return false;

                case HexTokenType.Alternation:
                    if (token.Alternatives is null) return false;
                    // Try each alternative branch
                    foreach (var alt in token.Alternatives)
                    {
                        // Build combined list: alternative tokens + remaining tokens after this one
                        var combined = new List<HexToken>(alt.Count + tokens.Count - tokenIndex - 1);
                        combined.AddRange(alt);
                        for (int r = tokenIndex + 1; r < tokens.Count; r++)
                            combined.Add(tokens[r]);
                        if (MatchHexTokensAt(combined, 0, data, dataOffset))
                            return true;
                    }
                    return false;
            }
        }
        return true; // All tokens matched successfully
    }

    // ── Text pattern matching (with fullword support) ───────────────────

    /// <summary>
    /// Find all match positions for a text string in the file bytes.
    /// Supports ASCII, wide (UTF-16LE), nocase, and fullword modifiers.
    /// </summary>
    private static List<int> FindTextMatchPositions(YaraStringEntry entry, byte[] data)
    {
        var positions = new List<int>();
        if (string.IsNullOrEmpty(entry.Value)) return positions;

        // ASCII search
        if (entry.IsAscii)
        {
            var searchBytes = Encoding.ASCII.GetBytes(entry.Value);
            var found = entry.IsNocase
                ? FindBytesNoCase(data, searchBytes)
                : FindBytesPositions(data, searchBytes);

            if (entry.IsFullword)
                found = found.Where(pos => IsWordBoundary(data, pos, searchBytes.Length)).ToList();

            positions.AddRange(found);
        }

        // Wide (UTF-16LE) search
        if (entry.IsWide)
        {
            var wideBytes = Encoding.Unicode.GetBytes(entry.Value);
            var found = entry.IsNocase
                ? FindBytesNoCase(data, wideBytes)
                : FindBytesPositions(data, wideBytes);

            if (entry.IsFullword)
                found = found.Where(pos => IsWordBoundary(data, pos, wideBytes.Length)).ToList();

            positions.AddRange(found);
        }

        return positions;
    }

    /// <summary>
    /// Search for a text string inside the file bytes (legacy bool API).
    /// </summary>
    private static bool MatchTextPattern(YaraStringEntry entry, byte[] data)
    {
        return FindTextMatchPositions(entry, data).Count > 0;
    }

    // ── Regex pattern matching ──────────────────────────────────────────

    /// <summary>
    /// Find all match positions for a regex pattern in the file bytes.
    /// </summary>
    private static List<int> FindRegexMatchPositions(string pattern, byte[] data)
    {
        var positions = new List<int>();
        if (string.IsNullOrWhiteSpace(pattern)) return positions;

        try
        {
            var text = Encoding.Latin1.GetString(data);
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromSeconds(5));
            foreach (Match m in regex.Matches(text))
            {
                positions.Add(m.Index);
            }
        }
        catch { /* invalid regex or timeout */ }

        return positions;
    }

    /// <summary>
    /// Match a .NET regex pattern against the file contents (legacy bool API).
    /// </summary>
    private static bool MatchRegexPattern(string pattern, byte[] data)
    {
        return FindRegexMatchPositions(pattern, data).Count > 0;
    }

    // ── Byte search helpers ─────────────────────────────────────────────

    /// <summary>Exact byte subsequence search returning all match positions.</summary>
    private static List<int> FindBytesPositions(byte[] haystack, byte[] needle)
    {
        var positions = new List<int>();
        if (needle.Length == 0) return positions;
        int end = haystack.Length - needle.Length;
        for (int i = 0; i <= end; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) positions.Add(i);
        }
        return positions;
    }

    /// <summary>Case-insensitive byte subsequence search returning all match positions (ASCII only).</summary>
    private static List<int> FindBytesNoCase(byte[] haystack, byte[] needle)
    {
        var positions = new List<int>();
        if (needle.Length == 0) return positions;
        int end = haystack.Length - needle.Length;
        for (int i = 0; i <= end; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                byte a = haystack[i + j];
                byte b = needle[j];
                // Lowercase ASCII letters for case-insensitive comparison
                if (a >= 0x41 && a <= 0x5A) a += 0x20;
                if (b >= 0x41 && b <= 0x5A) b += 0x20;
                if (a != b)
                {
                    found = false;
                    break;
                }
            }
            if (found) positions.Add(i);
        }
        return positions;
    }

    /// <summary>Exact byte subsequence search (legacy bool API).</summary>
    private static bool ContainsBytes(byte[] haystack, byte[] needle) =>
        FindBytesPositions(haystack, needle).Count > 0;

    /// <summary>Case-insensitive byte subsequence search (legacy bool API).</summary>
    private static bool ContainsBytesNoCase(byte[] haystack, byte[] needle) =>
        FindBytesNoCase(haystack, needle).Count > 0;

    // ── Word boundary helpers (for fullword modifier) ───────────────────

    /// <summary>Check whether a match at the given offset is at a word boundary.</summary>
    private static bool IsWordBoundary(byte[] data, int offset, int length)
    {
        // Check byte before the match
        if (offset > 0 && IsAlphanumericByte(data[offset - 1]))
            return false;
        // Check byte after the match
        int afterPos = offset + length;
        if (afterPos < data.Length && IsAlphanumericByte(data[afterPos]))
            return false;
        return true;
    }

    /// <summary>Check if a byte represents an ASCII alphanumeric character or underscore.</summary>
    private static bool IsAlphanumericByte(byte b) =>
        (b >= 0x30 && b <= 0x39) ||  // 0-9
        (b >= 0x41 && b <= 0x5A) ||  // A-Z
        (b >= 0x61 && b <= 0x7A) ||  // a-z
        b == 0x5F;                    // _

    // ── Condition evaluator ──────────────────────────────────────────────

    /// <summary>
    /// Evaluate the condition clause. Supports:
    ///   "any of them"             - at least one string matched
    ///   "all of them"             - every string matched
    ///   "N of them"               - at least N strings matched
    ///   "$s1"                     - specific string reference (single)
    ///   "$s1 and $s2"             - AND combination
    ///   "$s1 or $s2"              - OR combination
    ///   "N of ($s1, $s2, ...)"    - N of specific subset
    ///   "not $s1"                 - negation (string was NOT found)
    ///   "$s1 at 0"                - string must match at specific byte offset
    ///   "filesize &lt; 100KB"     - file size comparison (supports KB/MB/GB suffixes)
    /// Conditions can be combined: "$s1 at 0 and filesize &lt; 1MB and not $s2"
    /// </summary>
    private static bool EvaluateCondition(string condition, ObservableCollection<YaraStringEntry> strings,
        long fileSize, Dictionary<string, List<int>> matchPositions)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;

        var trimmed = condition.Trim().ToLowerInvariant();

        // ── Simple well-known patterns (fast path) ──
        if (trimmed == "any of them")
            return strings.Any(s => s.IsMatched);

        if (trimmed == "all of them")
            return strings.All(s => s.IsMatched);

        // "N of them"
        var nOfThemMatch = Regex.Match(trimmed, @"^(\d+)\s+of\s+them$");
        if (nOfThemMatch.Success && int.TryParse(nOfThemMatch.Groups[1].Value, out int nRequired))
            return strings.Count(s => s.IsMatched) >= nRequired;

        // "N of ($s1, $s2, ...)"
        var nOfSubsetMatch = Regex.Match(trimmed, @"^(\d+)\s+of\s+\(([^)]+)\)$");
        if (nOfSubsetMatch.Success && int.TryParse(nOfSubsetMatch.Groups[1].Value, out int nReq))
        {
            var ids = nOfSubsetMatch.Groups[2].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return strings.Count(s => ids.Contains(s.Identifier.ToLowerInvariant()) && s.IsMatched) >= nReq;
        }

        // ── Complex conditions with or / and ──
        // Split on " or " first (lower precedence), respecting parentheses
        if (ContainsOutsideParens(trimmed, " or "))
        {
            var orParts = SplitConditionParts(trimmed, " or ");
            return orParts.Any(part => EvaluateConditionGroup(part.Trim(), strings, fileSize, matchPositions));
        }

        // If there's " and ", treat the whole thing as an and-group
        if (ContainsOutsideParens(trimmed, " and "))
        {
            return EvaluateConditionGroup(trimmed, strings, fileSize, matchPositions);
        }

        // Single atomic term
        return EvaluateAtomicCondition(trimmed, strings, fileSize, matchPositions);
    }

    /// <summary>Evaluate a group of AND-combined conditions.</summary>
    private static bool EvaluateConditionGroup(string group, ObservableCollection<YaraStringEntry> strings,
        long fileSize, Dictionary<string, List<int>> matchPositions)
    {
        if (ContainsOutsideParens(group, " and "))
        {
            var andParts = SplitConditionParts(group, " and ");
            return andParts.All(part => EvaluateAtomicCondition(part.Trim(), strings, fileSize, matchPositions));
        }
        return EvaluateAtomicCondition(group, strings, fileSize, matchPositions);
    }

    /// <summary>
    /// Evaluate a single atomic condition term:
    ///   $sN, not $sN, $sN at OFFSET, filesize OP VALUE,
    ///   any of them, all of them, N of them, N of (...)
    /// </summary>
    private static bool EvaluateAtomicCondition(string term, ObservableCollection<YaraStringEntry> strings,
        long fileSize, Dictionary<string, List<int>> matchPositions)
    {
        term = term.Trim();

        // "any of them"
        if (term == "any of them")
            return strings.Any(s => s.IsMatched);
        if (term == "all of them")
            return strings.All(s => s.IsMatched);

        // "N of them"
        var nOfThemMatch = Regex.Match(term, @"^(\d+)\s+of\s+them$");
        if (nOfThemMatch.Success && int.TryParse(nOfThemMatch.Groups[1].Value, out int nReq))
            return strings.Count(s => s.IsMatched) >= nReq;

        // "N of ($s1, $s2, ...)"
        var nOfSubsetMatch = Regex.Match(term, @"^(\d+)\s+of\s+\(([^)]+)\)$");
        if (nOfSubsetMatch.Success && int.TryParse(nOfSubsetMatch.Groups[1].Value, out int nReqSub))
        {
            var ids = nOfSubsetMatch.Groups[2].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return strings.Count(s => ids.Contains(s.Identifier.ToLowerInvariant()) && s.IsMatched) >= nReqSub;
        }

        // "not ..."  (negation operator)
        if (term.StartsWith("not "))
        {
            var inner = term[4..].Trim();
            return !EvaluateAtomicCondition(inner, strings, fileSize, matchPositions);
        }

        // "filesize <op> <value>[KB|MB|GB]"
        var filesizeMatch = Regex.Match(term, @"^filesize\s*(==|!=|<=|>=|<|>)\s*(\d+)\s*(kb|mb|gb)?$");
        if (filesizeMatch.Success)
        {
            long size = long.Parse(filesizeMatch.Groups[2].Value);
            var unit = filesizeMatch.Groups[3].Value;
            size = unit switch
            {
                "kb" => size * 1024,
                "mb" => size * 1024 * 1024,
                "gb" => size * 1024L * 1024 * 1024,
                _ => size,
            };
            return filesizeMatch.Groups[1].Value switch
            {
                "==" => fileSize == size,
                "!=" => fileSize != size,
                "<" => fileSize < size,
                ">" => fileSize > size,
                "<=" => fileSize <= size,
                ">=" => fileSize >= size,
                _ => false,
            };
        }

        // "$sN at OFFSET"  (match at specific byte offset)
        var atMatch = Regex.Match(term, @"^(\$\w+)\s+at\s+(\d+)$");
        if (atMatch.Success)
        {
            var id = atMatch.Groups[1].Value;
            int offset = int.Parse(atMatch.Groups[2].Value);
            if (matchPositions.TryGetValue(id, out var positions))
                return positions.Contains(offset);
            return false;
        }

        // Single string reference "$sN"
        if (term.StartsWith("$"))
        {
            var entry = strings.FirstOrDefault(s =>
                s.Identifier.Equals(term, StringComparison.OrdinalIgnoreCase));
            return entry?.IsMatched ?? false;
        }

        // Fallback: treat as "any of them"
        return strings.Any(s => s.IsMatched);
    }

    /// <summary>Check whether a separator exists outside of parentheses in the text.</summary>
    private static bool ContainsOutsideParens(string text, string separator)
    {
        int depth = 0;
        for (int i = 0; i <= text.Length - separator.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            else if (depth == 0 &&
                     string.Compare(text, i, separator, 0, separator.Length, StringComparison.Ordinal) == 0)
                return true;
        }
        return false;
    }

    /// <summary>Split condition text on a separator, respecting parenthesized groups.</summary>
    private static List<string> SplitConditionParts(string condition, string separator)
    {
        var parts = new List<string>();
        int depth = 0;
        int lastSplit = 0;

        for (int i = 0; i <= condition.Length - separator.Length; i++)
        {
            if (condition[i] == '(') depth++;
            else if (condition[i] == ')') depth--;
            else if (depth == 0 &&
                     string.Compare(condition, i, separator, 0, separator.Length, StringComparison.Ordinal) == 0)
            {
                parts.Add(condition[lastSplit..i]);
                i += separator.Length - 1;
                lastSplit = i + 1;
            }
        }
        parts.Add(condition[lastSplit..]);
        return parts;
    }

    // ════════════════════════════════════════════════════════════════════
    //  VALIDATION
    // ════════════════════════════════════════════════════════════════════

    private string ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(EditorName))
            return "Rule name is required.";

        if (!Regex.IsMatch(EditorName.Trim(), @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            return "Rule name must start with a letter/underscore and contain only alphanumeric/underscore characters.";

        // Check for duplicate names (except self when editing)
        var existing = Rules.FirstOrDefault(r =>
            r.Name.Equals(EditorName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null && (EditorIsNew || existing != SelectedRule))
            return $"A rule named \"{EditorName.Trim()}\" already exists.";

        if (EditorStrings.Count == 0)
            return "At least one string entry is required.";

        foreach (var s in EditorStrings)
        {
            if (string.IsNullOrWhiteSpace(s.Identifier))
                return "All string entries must have an identifier.";

            if (!s.Identifier.StartsWith("$"))
                return $"Identifier \"{s.Identifier}\" must start with '$'.";

            if (string.IsNullOrWhiteSpace(s.Value))
                return $"String entry {s.Identifier} has no value.";

            if (s.Type == YaraStringType.Hex)
            {
                var hexError = ValidateHexPattern(s.Value, s.Identifier);
                if (hexError != null) return hexError;
            }

            if (s.Type == YaraStringType.Regex)
            {
                try { _ = new Regex(s.Value); }
                catch (Exception ex) { return $"Invalid regex in {s.Identifier}: {ex.Message}"; }
            }
        }

        if (string.IsNullOrWhiteSpace(EditorCondition))
            return "Condition is required.";

        return string.Empty;
    }

    /// <summary>
    /// Validate a hex pattern string. Returns null if valid, or an error message if invalid.
    /// Supports: two-char hex bytes, ?? wildcards, [N-M] jumps, and ( A | B ) alternation groups.
    /// </summary>
    private static string? ValidateHexPattern(string pattern, string identifier)
    {
        var hex = pattern.Replace("{", "").Replace("}", "").Trim();
        if (string.IsNullOrWhiteSpace(hex))
            return $"Hex pattern in {identifier} is empty.";

        // Check balanced parentheses and brackets
        int parenDepth = 0;
        int bracketDepth = 0;
        foreach (char c in hex)
        {
            if (c == '(') parenDepth++;
            else if (c == ')') { parenDepth--; if (parenDepth < 0) return $"Unbalanced parentheses in {identifier}."; }
            else if (c == '[') bracketDepth++;
            else if (c == ']') { bracketDepth--; if (bracketDepth < 0) return $"Unbalanced brackets in {identifier}."; }
        }
        if (parenDepth != 0) return $"Unbalanced parentheses in {identifier}.";
        if (bracketDepth != 0) return $"Unbalanced brackets in {identifier}.";

        // Tokenize and check for basic validity
        var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            // Skip valid syntax elements
            if (t == "??" || t == "?" || t == "|" || t == "(" || t == ")") continue;

            // Allow jump patterns [N-M] or [N]
            if (t.StartsWith("[") && t.EndsWith("]"))
            {
                var inner = t[1..^1];
                var dash = inner.IndexOf('-');
                if (dash >= 0)
                {
                    if (!int.TryParse(inner[..dash], out _) || !int.TryParse(inner[(dash + 1)..], out _))
                        return $"Invalid jump range \"{t}\" in {identifier}. Use [N-M] format.";
                }
                else
                {
                    if (!int.TryParse(inner, out _))
                        return $"Invalid jump \"{t}\" in {identifier}. Use [N] or [N-M] format.";
                }
                continue;
            }

            // Must be a two-character hex value
            if (t.Length != 2 || !int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out _))
                return $"Invalid hex token \"{t}\" in {identifier}. Use two-character hex values, ?? wildcards, [N-M] jumps, or ( A | B ) alternation.";
        }

        return null; // Valid
    }

    // ════════════════════════════════════════════════════════════════════
    //  PERSISTENCE
    // ════════════════════════════════════════════════════════════════════

    private bool LoadRulesFromDisk()
    {
        try
        {
            if (!File.Exists(DefaultRulesFile)) return false;

            var json = File.ReadAllText(DefaultRulesFile);
            var dto = JsonSerializer.Deserialize<YaraRuleSetDto>(json, JsonOpts);
            if (dto is null || dto.Rules.Count == 0) return false;

            Rules.Clear();
            foreach (var rd in dto.Rules)
                Rules.Add(DtoToRule(rd));

            LogEntry($"Loaded {Rules.Count} rule(s) from disk");
            return true;
        }
        catch (Exception ex)
        {
            LogEntry($"Failed to load rules: {ex.Message}");
            return false;
        }
    }

    private void SaveRulesToDisk()
    {
        try
        {
            var dto = new YaraRuleSetDto
            {
                ExportedAt = DateTime.Now,
                Rules = Rules.Select(RuleToDto).ToList(),
            };

            var json = JsonSerializer.Serialize(dto, JsonOpts);
            File.WriteAllText(DefaultRulesFile, json);
        }
        catch (Exception ex)
        {
            LogEntry($"Failed to save rules: {ex.Message}");
        }
    }

    private static YaraRuleDto RuleToDto(YaraRule r) => new()
    {
        Name = r.Name,
        Description = r.Description,
        Author = r.Author,
        Tags = r.Tags,
        Severity = r.Severity.ToString(),
        Condition = r.Condition,
        Enabled = r.IsEnabled,
        DateCreated = r.DateCreated,
        Strings = r.Strings.Select(s => new YaraStringDto
        {
            Identifier = s.Identifier,
            Value = s.Value,
            Type = s.Type.ToString(),
            Nocase = s.IsNocase,
            Wide = s.IsWide,
            Ascii = s.IsAscii,
            Fullword = s.IsFullword,
        }).ToList(),
    };

    private static YaraRule DtoToRule(YaraRuleDto d)
    {
        var rule = new YaraRule
        {
            Name = d.Name,
            Description = d.Description,
            Author = d.Author,
            Tags = d.Tags,
            Severity = Enum.TryParse<YaraRuleSeverity>(d.Severity, true, out var sev) ? sev : YaraRuleSeverity.Medium,
            Condition = d.Condition,
            IsEnabled = d.Enabled,
            DateCreated = d.DateCreated,
        };

        foreach (var sd in d.Strings)
        {
            rule.Strings.Add(new YaraStringEntry
            {
                Identifier = sd.Identifier,
                Value = sd.Value,
                Type = Enum.TryParse<YaraStringType>(sd.Type, true, out var st) ? st : YaraStringType.Text,
                IsNocase = sd.Nocase,
                IsWide = sd.Wide,
                IsAscii = sd.Ascii,
                IsFullword = sd.Fullword,
            });
        }

        return rule;
    }

    // ════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════

    private YaraRule BuildRuleFromEditor()
    {
        var rule = new YaraRule
        {
            Name = EditorName.Trim(),
            Description = EditorDescription?.Trim() ?? string.Empty,
            Author = EditorAuthor?.Trim() ?? string.Empty,
            Tags = EditorTags?.Trim() ?? string.Empty,
            Severity = EditorSeverity,
            Condition = EditorCondition?.Trim() ?? "any of them",
        };
        foreach (var s in EditorStrings)
            rule.Strings.Add(new YaraStringEntry
            {
                Identifier = s.Identifier,
                Value = s.Value,
                Type = s.Type,
                IsNocase = s.IsNocase,
                IsWide = s.IsWide,
                IsAscii = s.IsAscii,
                IsFullword = s.IsFullword,
            });
        return rule;
    }

    private void RefreshStats()
    {
        TotalRules = Rules.Count;
        EnabledRules = Rules.Count(r => r.IsEnabled);
        TotalMatches = Rules.Sum(r => r.MatchCount);
    }

    private void ApplyFilter()
    {
        // The UI can bind and filter via CollectionView; here we simply update
        // a hint for the view. Actual filtering is handled in the XAML ListBox
        // via ICollectionView if desired, or the user scrolls through the list.
        // The SearchText property itself is sufficient for a simple text filter
        // implemented via DataTriggers or converter on the view side.
    }

    private void LogEntry(string msg) =>
        Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");

    // ════════════════════════════════════════════════════════════════════
    //  YARA TEXT FORMAT PARSER
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parse standard YARA text format rules from a string.
    /// Supports the format:
    /// <code>
    ///   rule rule_name {
    ///       meta:
    ///           description = "..."
    ///           author = "..."
    ///       strings:
    ///           $s1 = "text" nocase fullword
    ///           $s2 = { AA BB CC }
    ///           $s3 = /regex/
    ///       condition:
    ///           any of them
    ///   }
    /// </code>
    /// Also supports tags: <c>rule name : tag1 tag2 { ... }</c>
    /// </summary>
    public static List<YaraRule> ParseYaraTextFormat(string text)
    {
        var rules = new List<YaraRule>();
        if (string.IsNullOrWhiteSpace(text)) return rules;

        // Match rule headers: rule NAME or rule NAME : tag1 tag2
        var ruleHeaderPattern = new Regex(
            @"rule\s+(\w+)(?:\s*:\s*([\w\s]+?))?\s*\{",
            RegexOptions.Compiled);

        foreach (Match header in ruleHeaderPattern.Matches(text))
        {
            var name = header.Groups[1].Value;
            var tags = header.Groups[2].Success ? header.Groups[2].Value.Trim() : string.Empty;

            // Find the matching closing brace using brace-depth counting
            int braceStart = header.Index + header.Length;
            int depth = 1;
            int pos = braceStart;
            while (pos < text.Length && depth > 0)
            {
                if (text[pos] == '{') depth++;
                else if (text[pos] == '}') depth--;
                pos++;
            }
            if (depth != 0) continue; // unbalanced braces, skip this rule

            var body = text[braceStart..(pos - 1)];
            var rule = new YaraRule { Name = name, Tags = tags };

            // ── Parse meta section ──
            var metaMatch = Regex.Match(body,
                @"meta\s*:(.*?)(?=strings\s*:|condition\s*:|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (metaMatch.Success)
            {
                var metaBody = metaMatch.Groups[1].Value;
                var descMatch = Regex.Match(metaBody, @"description\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
                if (descMatch.Success) rule.Description = descMatch.Groups[1].Value;
                var authorMatch = Regex.Match(metaBody, @"author\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
                if (authorMatch.Success) rule.Author = authorMatch.Groups[1].Value;
                var sevMatch = Regex.Match(metaBody, @"severity\s*=\s*""?(\w+)""?", RegexOptions.IgnoreCase);
                if (sevMatch.Success && Enum.TryParse<YaraRuleSeverity>(sevMatch.Groups[1].Value, true, out var sev))
                    rule.Severity = sev;
            }

            // ── Parse strings section ──
            var stringsMatch = Regex.Match(body,
                @"strings\s*:(.*?)(?=condition\s*:|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (stringsMatch.Success)
            {
                var stringsBody = stringsMatch.Groups[1].Value;
                // Match string definitions: $id = "text" modifiers  |  $id = { hex }  |  $id = /regex/ modifiers
                var stringDefPattern = new Regex(
                    @"(\$\w+)\s*=\s*(?:""([^""]*)""|(\{[^}]*\})|/([^/]*)/)\s*(.*?)$",
                    RegexOptions.Multiline | RegexOptions.Compiled);

                foreach (Match sd in stringDefPattern.Matches(stringsBody))
                {
                    var entry = new YaraStringEntry
                    {
                        Identifier = sd.Groups[1].Value,
                    };

                    var modifiersStr = sd.Groups[5].Value.Trim().ToLowerInvariant();

                    if (sd.Groups[2].Success && sd.Groups[2].Length > 0)
                    {
                        // Text string in double quotes
                        entry.Value = sd.Groups[2].Value;
                        entry.Type = YaraStringType.Text;
                    }
                    else if (sd.Groups[3].Success && sd.Groups[3].Length > 0)
                    {
                        // Hex string in curly braces
                        entry.Value = sd.Groups[3].Value;
                        entry.Type = YaraStringType.Hex;
                    }
                    else if (sd.Groups[4].Success)
                    {
                        // Regex in forward slashes
                        entry.Value = sd.Groups[4].Value;
                        entry.Type = YaraStringType.Regex;
                    }

                    entry.IsNocase = modifiersStr.Contains("nocase");
                    entry.IsWide = modifiersStr.Contains("wide");
                    entry.IsFullword = modifiersStr.Contains("fullword");
                    // Default to ASCII unless only wide is specified
                    entry.IsAscii = modifiersStr.Contains("ascii") || !modifiersStr.Contains("wide");

                    rule.Strings.Add(entry);
                }
            }

            // ── Parse condition section ──
            var condMatch = Regex.Match(body,
                @"condition\s*:\s*(.*)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (condMatch.Success)
            {
                rule.Condition = condMatch.Groups[1].Value.Trim();
            }

            rules.Add(rule);
        }

        return rules;
    }

    // ════════════════════════════════════════════════════════════════════
    //  BUILT-IN RULES (15+)
    // ════════════════════════════════════════════════════════════════════

    private void LoadBuiltInRules()
    {
        // 1. MZ DOS Header (PE executable detection)
        Rules.Add(new YaraRule
        {
            Name = "pe_executable_header",
            Description = "Detects PE (Portable Executable) files via MZ header and PE signature",
            Author = "SGL SyntheticAI",
            Tags = "pe executable",
            Severity = YaraRuleSeverity.Info,
            Condition = "all of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$mz", Value = "4D 5A", Type = YaraStringType.Hex },
                new() { Identifier = "$pe", Value = "50 45 00 00", Type = YaraStringType.Hex },
            },
        });

        // 2. Suspicious PE imports -- process injection
        Rules.Add(new YaraRule
        {
            Name = "suspicious_pe_imports_injection",
            Description = "Detects process injection APIs: VirtualAllocEx, WriteProcessMemory, CreateRemoteThread",
            Author = "SGL SyntheticAI",
            Tags = "injection api imports",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$api1", Value = "VirtualAllocEx", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$api2", Value = "WriteProcessMemory", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$api3", Value = "CreateRemoteThread", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$api4", Value = "NtUnmapViewOfSection", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 3. UPX packed executable
        Rules.Add(new YaraRule
        {
            Name = "upx_packed_executable",
            Description = "Detects UPX-packed executables via section header markers",
            Author = "SGL SyntheticAI",
            Tags = "packer upx obfuscation",
            Severity = YaraRuleSeverity.Medium,
            Condition = "$mz and $upx1",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$mz", Value = "4D 5A", Type = YaraStringType.Hex },
                new() { Identifier = "$upx1", Value = "UPX0", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$upx2", Value = "UPX1", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$upx3", Value = "UPX!", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 4. Ransomware file extension patterns
        Rules.Add(new YaraRule
        {
            Name = "ransomware_extension_strings",
            Description = "Detects common ransomware-related strings (payment, encryption, Bitcoin)",
            Author = "SGL SyntheticAI",
            Tags = "ransomware crypto extortion",
            Severity = YaraRuleSeverity.Critical,
            Condition = "3 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$r1", Value = "Your files have been encrypted", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$r2", Value = "bitcoin", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$r3", Value = "decrypt", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$r4", Value = "ransom", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$r5", Value = ".onion", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$r6", Value = "payment", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$r7", Value = "CryptoLocker", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 5. Keylogger indicators
        Rules.Add(new YaraRule
        {
            Name = "keylogger_indicators",
            Description = "Detects keylogger patterns: keyboard hooks, keystroke logging APIs",
            Author = "SGL SyntheticAI",
            Tags = "keylogger spyware",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$k1", Value = "SetWindowsHookEx", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$k2", Value = "GetAsyncKeyState", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$k3", Value = "GetKeyState", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$k4", Value = "keylog", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$k5", Value = "GetKeyboardState", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 6. Crypto miner strings
        Rules.Add(new YaraRule
        {
            Name = "crypto_miner_strings",
            Description = "Detects crypto mining-related strings (stratum, hashrate, XMR pool)",
            Author = "SGL SyntheticAI",
            Tags = "miner crypto cryptojacking",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$m1", Value = "stratum+tcp://", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$m2", Value = "hashrate", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$m3", Value = "xmrig", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$m4", Value = "monero", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$m5", Value = "mining.pool", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$m6", Value = "CryptoNight", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 7. Webshell indicators
        Rules.Add(new YaraRule
        {
            Name = "webshell_indicators",
            Description = "Detects common webshell code patterns (eval, base64_decode, cmd, shell_exec)",
            Author = "SGL SyntheticAI",
            Tags = "webshell backdoor web",
            Severity = YaraRuleSeverity.Critical,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$w1", Value = "eval(base64_decode(", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$w2", Value = "shell_exec(", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$w3", Value = "system($_", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$w4", Value = "passthru(", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$w5", Value = "c99shell", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$w6", Value = "r57shell", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 8. Reverse shell patterns
        Rules.Add(new YaraRule
        {
            Name = "reverse_shell_patterns",
            Description = "Detects reverse shell code patterns (bash -i, /dev/tcp, nc -e, powershell reverse)",
            Author = "SGL SyntheticAI",
            Tags = "reverseshell backdoor network",
            Severity = YaraRuleSeverity.Critical,
            Condition = "any of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$rs1", Value = "bash -i >& /dev/tcp/", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$rs2", Value = "/dev/tcp/", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$rs3", Value = "nc -e /bin/", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$rs4", Value = "TCPClient", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$rs5", Value = "socket.connect", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 9. Suspicious PowerShell
        Rules.Add(new YaraRule
        {
            Name = "suspicious_powershell",
            Description = "Detects obfuscated or malicious PowerShell patterns (encoded commands, bypass, download cradles)",
            Author = "SGL SyntheticAI",
            Tags = "powershell script obfuscation",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$ps1", Value = "-EncodedCommand", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ps2", Value = "-ExecutionPolicy Bypass", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ps3", Value = "Invoke-Expression", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ps4", Value = "IEX(New-Object Net.WebClient).DownloadString", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ps5", Value = "[Convert]::FromBase64String", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$ps6", Value = "powershell -w hidden", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ps7", Value = "-WindowStyle Hidden", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 10. WMI persistence
        Rules.Add(new YaraRule
        {
            Name = "wmi_persistence",
            Description = "Detects WMI-based persistence mechanisms (event subscriptions, permanent consumers)",
            Author = "SGL SyntheticAI",
            Tags = "wmi persistence",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$wmi1", Value = "CommandLineEventConsumer", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$wmi2", Value = "__EventFilter", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$wmi3", Value = "ActiveScriptEventConsumer", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$wmi4", Value = "__FilterToConsumerBinding", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$wmi5", Value = "Win32_ProcessStartTrace", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 11. Scheduled task abuse
        Rules.Add(new YaraRule
        {
            Name = "scheduled_task_abuse",
            Description = "Detects scheduled task creation for persistence via schtasks or Task Scheduler COM",
            Author = "SGL SyntheticAI",
            Tags = "persistence schtasks",
            Severity = YaraRuleSeverity.Medium,
            Condition = "any of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$st1", Value = "schtasks /create", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$st2", Value = "schtasks.exe /create", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$st3", Value = "ITaskService", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$st4", Value = "Register-ScheduledTask", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 12. Credential dumping tools
        Rules.Add(new YaraRule
        {
            Name = "credential_dumping",
            Description = "Detects credential dumping tool signatures (Mimikatz, lsass, SAM dump)",
            Author = "SGL SyntheticAI",
            Tags = "credentials mimikatz lsass",
            Severity = YaraRuleSeverity.Critical,
            Condition = "any of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$cd1", Value = "mimikatz", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$cd2", Value = "sekurlsa::logonpasswords", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$cd3", Value = "lsass.exe", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$cd4", Value = "MiniDumpWriteDump", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$cd5", Value = "procdump", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 13. Suspicious registry modifications
        Rules.Add(new YaraRule
        {
            Name = "suspicious_registry_mods",
            Description = "Detects registry modifications for persistence or defense evasion (Run keys, DisableAntiSpyware)",
            Author = "SGL SyntheticAI",
            Tags = "registry persistence evasion",
            Severity = YaraRuleSeverity.Medium,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$reg1", Value = @"Software\Microsoft\Windows\CurrentVersion\Run", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$reg2", Value = @"Software\Microsoft\Windows\CurrentVersion\RunOnce", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$reg3", Value = "DisableAntiSpyware", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$reg4", Value = "DisableRealtimeMonitoring", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$reg5", Value = "reg add", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 14. Embedded shellcode patterns
        Rules.Add(new YaraRule
        {
            Name = "embedded_shellcode",
            Description = "Detects common x86/x64 shellcode prologues and NOP sleds",
            Author = "SGL SyntheticAI",
            Tags = "shellcode exploit",
            Severity = YaraRuleSeverity.Critical,
            Condition = "any of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                // NOP sled (16 consecutive NOPs)
                new() { Identifier = "$nop", Value = "90 90 90 90 90 90 90 90 90 90 90 90 90 90 90 90", Type = YaraStringType.Hex },
                // Common shellcode stub: push ebp / mov ebp,esp / sub esp
                new() { Identifier = "$prologue32", Value = "55 8B EC 83 EC", Type = YaraStringType.Hex },
                // x64 shellcode: sub rsp
                new() { Identifier = "$sub_rsp", Value = "48 83 EC", Type = YaraStringType.Hex },
                // WinExec shellcode marker
                new() { Identifier = "$winexec", Value = "FF D5 ?? ?? ?? 57 69 6E 45 78 65 63", Type = YaraStringType.Hex },
            },
        });

        // 15. Suspicious network indicators (C2-like patterns)
        Rules.Add(new YaraRule
        {
            Name = "suspicious_c2_indicators",
            Description = "Detects command-and-control patterns (beaconing, user-agent strings, C2 frameworks)",
            Author = "SGL SyntheticAI",
            Tags = "c2 network beacon",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$c2_1", Value = "Mozilla/5.0 (compatible; MSIE", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$c2_2", Value = "Cobalt Strike", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$c2_3", Value = "/beacon/", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$c2_4", Value = "meterpreter", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$c2_5", Value = "empire", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        // 16. Lateral movement tools
        Rules.Add(new YaraRule
        {
            Name = "lateral_movement_tools",
            Description = "Detects lateral movement tool strings (PsExec, WinRM, WMI remote execution)",
            Author = "SGL SyntheticAI",
            Tags = "lateral movement psexec",
            Severity = YaraRuleSeverity.High,
            Condition = "any of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$lat1", Value = "PsExec", Type = YaraStringType.Text, IsAscii = true },
                new() { Identifier = "$lat2", Value = "psexesvc", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$lat3", Value = "Invoke-WmiMethod", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$lat4", Value = "Enter-PSSession", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$lat5", Value = "WinRM", Type = YaraStringType.Text, IsAscii = true },
            },
        });

        // 17. Data exfiltration indicators
        Rules.Add(new YaraRule
        {
            Name = "data_exfiltration_indicators",
            Description = "Detects data exfiltration patterns (DNS tunneling, base64 uploads, archive before exfil)",
            Author = "SGL SyntheticAI",
            Tags = "exfiltration data_theft",
            Severity = YaraRuleSeverity.High,
            Condition = "2 of them",
            Strings = new ObservableCollection<YaraStringEntry>
            {
                new() { Identifier = "$ex1", Value = "Compress-Archive", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ex2", Value = "Invoke-WebRequest", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ex3", Value = "bitsadmin /transfer", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ex4", Value = "certutil -encode", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
                new() { Identifier = "$ex5", Value = "nslookup", Type = YaraStringType.Text, IsNocase = true, IsAscii = true },
            },
        });

        LogEntry($"Loaded {Rules.Count} built-in rules");
    }
}
