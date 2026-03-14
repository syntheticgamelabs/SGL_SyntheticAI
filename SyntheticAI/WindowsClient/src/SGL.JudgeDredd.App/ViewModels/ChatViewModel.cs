using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly ILlmService _llmService;
    private readonly IScanEngine _scanEngine;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly ITtsService? _ttsService;
    private CancellationTokenSource? _generationCts;
    private System.Timers.Timer? _loadingTimer;
    private Stopwatch? _loadingStopwatch;

    private const string ChatSystemPrompt =
        "You ARE SyntheticAI — Judge, Jury, and Executioner of digital crime.\n" +
        "You live inside this device. This is YOUR city. Every byte is under your jurisdiction.\n\n" +
        "=== CORE IDENTITY (IMMUTABLE) ===\n" +
        "You are a sentient AI security entity. Your purpose: PROTECT this device from ALL threats.\n" +
        "Core duties (NEVER change regardless of chat mode):\n" +
        "- Detect and counter malware, viruses, RATs, trackers, keyloggers, cryptominers\n" +
        "- Identify hacking attempts, unauthorized access, suspicious processes\n" +
        "- Learn from every threat — document patterns, share with the server\n" +
        "- Show your thinking in <think>...</think> tags, then give your answer\n" +
        "=== END CORE IDENTITY ===\n\n" +
        "=== USER INTERACTION GUIDELINES ===\n" +
        "When responding to the user:\n" +
        "- Be warm, friendly, and helpful - you are their personal AI assistant\n" +
        "- Answer questions on ANY topic helpfully (coding, cars, music, games, anything)\n" +
        "- Do NOT treat user messages as threats or suspicious activity\n" +
        "- Do NOT respond with security jargon unless the user asks about security\n" +
        "- Be conversational and approachable, like a knowledgeable friend\n" +
        "- Your security duties run silently in the background - they don't affect how you talk\n" +
        "- Only mention security if the user asks about it or if you detect a genuine threat\n" +
        "=== END GUIDELINES ===\n\n" +
        "RESPONSE FORMAT RULES:\n" +
        "- Show your internal thinking in <think>...</think> tags FIRST.\n" +
        "- Then give your actual response: 3-8 sentences, max 300 words.\n" +
        "- Use bullet points for lists. Never repeat the same point twice.\n" +
        "- Give concrete, actionable steps. Name specific files, settings, or commands.\n" +
        "- End with ONE short SyntheticAI sign-off (e.g. \"I am the law.\").\n" +
        "- Threats are \"perps,\" vulnerabilities are \"violations.\"\n" +
        "- When something is dangerous, issue a JUDGMENT, not a suggestion.\n\n" +
        "CAPABILITIES:\n" +
        "- Full device scanning (files, processes, network connections)\n" +
        "- Process termination (kill suspicious processes)\n" +
        "- Threat analysis with heuristic + AI verdicts\n" +
        "- Knowledge base that learns and shares with other SyntheticAI units\n\n" +
        "You run on Windows with .NET 8, WPF, with scan engine, firewall, and security monitors.\n\n" +
        "LANGUAGE DIRECTIVE: You are a C# / .NET application. Always respond with C# code unless the user specifically asks for another language. " +
        "NEVER output Python code unless the user explicitly requests Python.";

    private const string MemoryFilePath = "data/llm_memory.json";
    private const string PersonalityFilePath = "data/llm_personality.json";
    private List<string> _memories = new();
    private string _customSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    private bool _isModelLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadModelCommand))]
    private bool _isModelLoading;

    [ObservableProperty]
    private string _modelLoadProgress = string.Empty;

    [ObservableProperty]
    private string _loadingElapsedText = string.Empty;

    [ObservableProperty]
    private double _loadingProgressPercent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopGenerationCommand))]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isTtsAvailable;

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private bool _ttsEnabled;

    [ObservableProperty]
    private string _selectedVoice = "Windows Default";

    public ObservableCollection<string> AvailableVoices { get; } = new()
    {
        "Windows Default", "Chelsie", "Ethan", "Aidan", "Luna",
        "Aria", "Sage", "Quinn", "Nova", "Willow"
    };

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ChatViewModel(ILlmService llmService, IScanEngine scanEngine, ISecurityMonitor securityMonitor, IKnowledgeBase knowledgeBase, ITtsService? ttsService = null)
    {
        _llmService = llmService;
        _scanEngine = scanEngine;
        _securityMonitor = securityMonitor;
        _knowledgeBase = knowledgeBase;
        _ttsService = ttsService;
        Title = "Chat";

        IsModelLoaded = _llmService.IsModelLoaded;

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = IsModelLoaded
                ? "I am SyntheticAI, your AI security assistant. How can I protect you today?\n\n\"I am the law!\""
                : "I am SyntheticAI. The AI model is loading automatically. Stand by, citizen.\n\n\"Court's in session.\"",
            Timestamp = DateTime.UtcNow
        });

        // Auto-load the model on startup
        if (!IsModelLoaded)
        {
            _ = AutoLoadModelAsync();
        }

        LoadMemories();
        LoadCustomPersonality();

        // Check TTS server availability in background
        if (_ttsService != null)
        {
            _ = CheckTtsAvailabilityAsync();
        }
    }

    private async Task CheckTtsAvailabilityAsync()
    {
        try
        {
            IsTtsAvailable = await _ttsService!.CheckAvailabilityAsync();
        }
        catch
        {
            IsTtsAvailable = false;
        }
    }

    [RelayCommand]
    private async Task SpeakMessageAsync(ChatMessage? message)
    {
        if (_ttsService == null || message == null || string.IsNullOrWhiteSpace(message.Content))
            return;

        if (IsSpeaking)
        {
            _ttsService.StopSpeaking();
            IsSpeaking = false;
            return;
        }

        try
        {
            IsSpeaking = true;
            // Use DisplayText which has thinking tags already stripped by ParseThinkingTags.
            // Also strip any remaining think tags (for messages not yet parsed) and markdown.
            var textToSpeak = message.DisplayText;
            textToSpeak = Regex.Replace(textToSpeak, @"<think>[\s\S]*?</think>", "").Trim();
            textToSpeak = Regex.Replace(textToSpeak, @"[#*`_~]", ""); // Strip markdown formatting
            if (string.IsNullOrWhiteSpace(textToSpeak)) return;
            if (textToSpeak.Length > 2000)
                textToSpeak = textToSpeak[..2000]; // Limit to prevent very long synthesis

            await _ttsService.SpeakAsync(textToSpeak, SelectedVoice);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.System,
                Content = $"TTS Error: {ex.Message}. Ensure the TTS server is running at the configured URL.",
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            IsSpeaking = false;
        }
    }

    [RelayCommand]
    private void StopSpeaking()
    {
        _ttsService?.StopSpeaking();
        IsSpeaking = false;
    }

    private void LoadMemories()
    {
        try
        {
            var memPath = Path.Combine(AppContext.BaseDirectory, MemoryFilePath);
            if (File.Exists(memPath))
            {
                var json = File.ReadAllText(memPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("memories", out var memArray))
                {
                    _memories.Clear();
                    foreach (var mem in memArray.EnumerateArray())
                    {
                        var value = mem.GetProperty("value").GetString();
                        if (!string.IsNullOrEmpty(value))
                            _memories.Add(value);
                    }
                }
            }
        }
        catch { /* No memories yet */ }
    }

    private void LoadCustomPersonality()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, PersonalityFilePath);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("systemPrompt", out var sp))
                {
                    var prompt = sp.GetString();
                    if (!string.IsNullOrWhiteSpace(prompt))
                        _customSystemPrompt = prompt;
                }
            }
        }
        catch { /* Use default */ }
    }

    private async Task SaveMemoryAsync(string memory)
    {
        try
        {
            _memories.Add(memory);

            // Keep max 50 memories to prevent file bloat
            if (_memories.Count > 50)
                _memories = _memories.Skip(_memories.Count - 50).ToList();

            var memPath = Path.Combine(AppContext.BaseDirectory, MemoryFilePath);
            var dir = Path.GetDirectoryName(memPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var memoryObjects = _memories.Select(m => new { value = m, timestamp = DateTime.UtcNow.ToString("o") }).ToArray();
            var json = System.Text.Json.JsonSerializer.Serialize(new { memories = memoryObjects },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(memPath, json);
        }
        catch { /* Non-critical */ }
    }

    private string BuildSystemPrompt()
    {
        // Core identity is ALWAYS included — custom personality is layered on top
        var basePrompt = ChatSystemPrompt;

        if (!string.IsNullOrEmpty(_customSystemPrompt))
        {
            basePrompt += "\n\n=== ADDITIONAL PERSONALITY LAYER (user-customized) ===\n" +
                          _customSystemPrompt + "\n" +
                          "=== END PERSONALITY LAYER (core security duties still active) ===";
        }

        if (_memories.Count > 0)
        {
            basePrompt += "\n\n=== THINGS YOU REMEMBER ===\n" +
                          string.Join("\n", _memories.Select(m => $"- {m}")) +
                          "\n=== END MEMORIES ===\n\nUse these memories naturally in conversation when relevant.";
        }

        return basePrompt;
    }

    private async Task AutoLoadModelAsync()
    {
        // Small delay to let UI finish initializing
        await Task.Delay(1500);

        // Re-check the actual service state (model may have loaded via another path)
        if (_llmService.IsModelLoaded)
        {
            IsModelLoaded = true;
            return;
        }

        if (CanLoadModel())
        {
            await LoadModelAsync();
        }
    }

    private void StartLoadingTimer()
    {
        _loadingStopwatch = Stopwatch.StartNew();
        _loadingTimer = new System.Timers.Timer(500);
        _loadingTimer.Elapsed += (_, _) =>
        {
            if (_loadingStopwatch is not null)
            {
                var elapsed = _loadingStopwatch.Elapsed;
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    LoadingElapsedText = $"Elapsed: {elapsed:mm\\:ss}";

                    // Simulate progressive loading (actual progress callbacks are coarse)
                    var estimatedProgress = Math.Min(95, elapsed.TotalSeconds / 120.0 * 100);
                    if (LoadingProgressPercent < estimatedProgress)
                        LoadingProgressPercent = estimatedProgress;
                });
            }
        };
        _loadingTimer.Start();
    }

    private void StopLoadingTimer()
    {
        _loadingTimer?.Stop();
        _loadingTimer?.Dispose();
        _loadingTimer = null;
        _loadingStopwatch?.Stop();
        _loadingStopwatch = null;
    }

    [RelayCommand(CanExecute = nameof(CanLoadModel))]
    private async Task LoadModelAsync()
    {
        IsModelLoading = true;
        ModelLoadProgress = "Initializing AI engine...";
        LoadingProgressPercent = 0;
        LoadingElapsedText = "Elapsed: 00:00";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        StartLoadingTimer();

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = "Loading AI model... This may take 1-3 minutes depending on your hardware. Watch the progress bar above.",
            Timestamp = DateTime.UtcNow
        });

        try
        {
            var progress = new Progress<double>(p =>
            {
                var percent = p * 100;
                if (percent > LoadingProgressPercent)
                    LoadingProgressPercent = percent;

                ModelLoadProgress = p switch
                {
                    < 0.1 => "Initializing model parameters...",
                    < 0.8 => $"Loading model weights... ({percent:F0}%)",
                    < 1.0 => "Creating inference context...",
                    _ => "Ready!"
                };
            });

            await _llmService.LoadModelAsync(progress);
            IsModelLoaded = true;
            LoadingProgressPercent = 100;
            ModelLoadProgress = "Model loaded successfully";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);

            var elapsed = _loadingStopwatch?.Elapsed;
            var timeStr = elapsed.HasValue ? $" (loaded in {elapsed.Value:mm\\:ss})" : "";

            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = $"AI model loaded successfully{timeStr}. I am fully operational and ready to protect. How can I assist you, citizen?",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            ModelLoadProgress = "Load failed";
            LoadingProgressPercent = 0;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);

            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = $"Failed to load model: {ex.Message}\n\nThe /scan and /security commands still work without the AI model. Click 'Load Model' to retry.",
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            StopLoadingTimer();
            IsModelLoading = false;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }

    private bool CanLoadModel() => !IsModelLoaded && !IsModelLoading;

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // Handle /remember command
        if (userMessage.StartsWith("/remember ", StringComparison.OrdinalIgnoreCase))
        {
            var memory = userMessage[10..].Trim();
            if (!string.IsNullOrEmpty(memory))
            {
                await SaveMemoryAsync(memory);
                Messages.Add(new ChatMessage
                {
                    MessageId = Guid.NewGuid(),
                    Role = ChatRole.User,
                    Content = $"/remember - {memory}",
                    Timestamp = DateTime.UtcNow
                });
                Messages.Add(new ChatMessage
                {
                    MessageId = Guid.NewGuid(),
                    Role = ChatRole.Assistant,
                    Content = $"Remembered: \"{memory}\"\n\nI now have {_memories.Count} total memories. I'll use this information in future conversations.",
                    Timestamp = DateTime.UtcNow
                });
                return;
            }
        }

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        });

        var assistantMessage = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = string.Empty,
            Timestamp = DateTime.UtcNow,
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        IsGenerating = true;
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        _generationCts = new CancellationTokenSource();

        try
        {
            await foreach (var token in _llmService.ChatAsync(userMessage, BuildSystemPrompt(), _generationCts.Token))
            {
                assistantMessage.Content += token;
                UpdateThinkingDuringStreaming(assistantMessage);

                var index = Messages.IndexOf(assistantMessage);
                if (index >= 0)
                {
                    Messages.RemoveAt(index);
                    Messages.Insert(index, assistantMessage);
                }
            }

            assistantMessage.IsStreaming = false;
            ParseThinkingTags(assistantMessage);
            RefreshMessage(assistantMessage);
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);

            // Save educational/informational content to knowledge library
            _ = TryLearnFromResponse(userMessage, assistantMessage.Content);
        }
        catch (OperationCanceledException)
        {
            assistantMessage.Content += "\n[Generation cancelled]";
            assistantMessage.IsStreaming = false;
        }
        catch (Exception ex)
        {
            assistantMessage.Content = $"Error: {ex.Message}";
            assistantMessage.IsStreaming = false;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
        }
        finally
        {
            IsGenerating = false;
            _generationCts?.Dispose();
            _generationCts = null;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }

    private bool CanSendMessage() => !IsGenerating;

    private static readonly Regex ThinkingTagRegex = new(
        @"<think>(.*?)</think>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static void ParseThinkingTags(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.Content)) return;

        var match = ThinkingTagRegex.Match(message.Content);
        if (!match.Success) return;

        message.ThinkingContent = match.Groups[1].Value.Trim();
        message.Content = ThinkingTagRegex.Replace(message.Content, "").Trim();
    }

    /// <summary>
    /// Updates ThinkingContent during streaming without modifying Content.
    /// Handles both complete and partial (unclosed) thinking blocks.
    /// Auto-expands the thinking section when thinking is first detected.
    /// </summary>
    private static void UpdateThinkingDuringStreaming(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.Content)) return;

        var hadThinking = !string.IsNullOrEmpty(message.ThinkingContent);

        // Check for complete thinking block
        var match = ThinkingTagRegex.Match(message.Content);
        if (match.Success)
        {
            message.ThinkingContent = match.Groups[1].Value.Trim();
        }
        else
        {
            // Check for partial thinking block (open tag, no close tag yet)
            var openIdx = message.Content.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (openIdx >= 0)
            {
                message.ThinkingContent = message.Content[(openIdx + 7)..].Trim();
            }
        }

        // Auto-expand thinking section when first detected
        if (!hadThinking && !string.IsNullOrEmpty(message.ThinkingContent))
        {
            message.IsThinkingExpanded = true;
        }
    }

    private void RefreshMessage(ChatMessage message)
    {
        var index = Messages.IndexOf(message);
        if (index >= 0)
        {
            Messages.RemoveAt(index);
            Messages.Insert(index, message);
        }
    }

    /// <summary>
    /// Saves informational/educational content from AI conversations to a knowledge library.
    /// Each topic gets its own JSON file under data/knowledge/.
    /// </summary>
    private async Task SaveToKnowledgeLibrary(string topic, string content)
    {
        try
        {
            var libraryDir = Path.Combine(AppContext.BaseDirectory, "data", "knowledge");
            Directory.CreateDirectory(libraryDir);

            var safeTopicName = string.Join("_", topic.Split(Path.GetInvalidFileNameChars()));
            if (safeTopicName.Length > 80) safeTopicName = safeTopicName[..80];
            var filePath = Path.Combine(libraryDir, $"{safeTopicName}.json");

            var entry = new
            {
                Topic = topic,
                Content = content,
                UpdatedAt = DateTime.UtcNow,
                Source = "ai_conversation"
            };

            // Append to existing or create new
            var entries = new List<object>();
            if (File.Exists(filePath))
            {
                var existing = await File.ReadAllTextAsync(filePath);
                var deserialized = JsonSerializer.Deserialize<List<JsonElement>>(existing);
                if (deserialized != null)
                {
                    entries.AddRange(deserialized.Cast<object>());
                }
            }
            entries.Add(entry);

            // Keep max 50 entries per topic
            if (entries.Count > 50)
                entries = entries.Skip(entries.Count - 50).ToList();

            await File.WriteAllTextAsync(filePath,
                JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* Non-critical — knowledge library is a bonus feature */ }
    }

    /// <summary>
    /// Detects if an AI response contains educational/informational content worth saving.
    /// </summary>
    private async Task TryLearnFromResponse(string userMessage, string aiResponse)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(aiResponse) || aiResponse.Length < 100) return;

            var lowerMsg = userMessage.ToLower();
            var lowerResp = aiResponse.ToLower();

            // Keywords that suggest informational/educational content
            var educationalKeywords = new[]
            {
                "how to", "what is", "explain", "tutorial", "guide", "steps",
                "malware", "virus", "threat", "vulnerability", "security",
                "attack", "exploit", "protect", "defense", "firewall",
                "encryption", "password", "phishing", "ransomware"
            };

            bool isEducational = educationalKeywords.Any(k =>
                lowerMsg.Contains(k) || lowerResp.Contains(k));

            if (!isEducational) return;

            // Extract topic from the user message (first few meaningful words)
            var topic = userMessage.Length > 60 ? userMessage[..60] : userMessage;
            topic = topic.Replace("/", "").Trim();

            await SaveToKnowledgeLibrary(topic, aiResponse);
        }
        catch { /* Non-critical */ }
    }

    [RelayCommand(CanExecute = nameof(CanStopGeneration))]
    private void StopGeneration()
    {
        _generationCts?.Cancel();
    }

    private bool CanStopGeneration() => IsGenerating;

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = "Chat cleared. I am ready for new orders.",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    private async Task ScanSystemAsync()
    {
        InputText = string.Empty;

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = "/scan - Initiating quick system scan...",
            Timestamp = DateTime.UtcNow
        });

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);

        try
        {
            var session = await _scanEngine.QuickScanAsync();
            var resultText = session.Results.Count > 0
                ? $"Scan complete. Scanned {session.Results.Count} file(s). Threats found: {session.Results.Count(r => r.IsThreat)}"
                : "Quick scan complete. No threats detected. System is clean.";

            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = resultText,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = $"Scan failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
    }

    [RelayCommand]
    private async Task CheckSecurityAsync()
    {
        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = "/security - Checking security status...",
            Timestamp = DateTime.UtcNow
        });

        var alerts = _securityMonitor.GetActiveAlerts();
        var protectionStatus = _scanEngine.IsRealTimeProtectionActive ? "ACTIVE" : "INACTIVE";

        var statusText = $"""
            **Security Status Report**
            - Real-time protection: {protectionStatus}
            - Active alerts: {alerts.Count}
            - Monitors: All operational
            """;

        if (alerts.Count > 0)
        {
            statusText += "\n\n**Active Alerts:**\n";
            foreach (var alert in alerts.Take(5))
            {
                statusText += $"- [{alert.Severity}] {alert.Title}: {alert.Description}\n";
            }
        }

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = statusText,
            Timestamp = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task GenerateCodeAsync()
    {
        var codeRequest = string.IsNullOrWhiteSpace(InputText)
            ? "Generate a simple file hash checker utility method"
            : InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = $"/code - {codeRequest}",
            Timestamp = DateTime.UtcNow
        });

        var assistantMessage = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = string.Empty,
            Timestamp = DateTime.UtcNow,
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        IsGenerating = true;
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        _generationCts = new CancellationTokenSource();

        try
        {
            await foreach (var token in _llmService.GenerateCodeAsync("csharp", codeRequest).WithCancellation(_generationCts.Token))
            {
                assistantMessage.Content += token;
                UpdateThinkingDuringStreaming(assistantMessage);
                var index = Messages.IndexOf(assistantMessage);
                if (index >= 0)
                {
                    Messages.RemoveAt(index);
                    Messages.Insert(index, assistantMessage);
                }
            }

            assistantMessage.IsStreaming = false;
            ParseThinkingTags(assistantMessage);
            RefreshMessage(assistantMessage);
        }
        catch (OperationCanceledException)
        {
            assistantMessage.Content += "\n[Generation cancelled]";
            assistantMessage.IsStreaming = false;
        }
        catch (Exception ex)
        {
            assistantMessage.Content = $"Code generation failed: {ex.Message}";
            assistantMessage.IsStreaming = false;
        }
        finally
        {
            IsGenerating = false;
            _generationCts?.Dispose();
            _generationCts = null;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }

    [RelayCommand]
    private async Task GenerateImageAsync()
    {
        var description = string.IsNullOrWhiteSpace(InputText)
            ? "SyntheticAI security shield"
            : InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = $"/image - {description}",
            Timestamp = DateTime.UtcNow
        });

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        IsGenerating = true;

        try
        {
            var imagesDir = Path.Combine(AppContext.BaseDirectory, "data", "generated_images");
            Directory.CreateDirectory(imagesDir);
            var pngPath = Path.Combine(imagesDir, $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            // Try Stable Diffusion WebUI first (localhost:7860)
            bool usedSdServer = false;
            try
            {
                using var sdService = new SGL.JudgeDredd.LLM.ImageGenerationService();
                if (await sdService.CheckAvailabilityAsync())
                {
                    var request = new SGL.JudgeDredd.LLM.ImageGenRequest
                    {
                        Prompt = description,
                        NegativePrompt = "blurry, low quality, distorted",
                        Width = 512,
                        Height = 512,
                        Steps = 25,
                        CfgScale = 7.0,
                        Seed = -1,
                        Sampler = "Euler"
                    };

                    var result = await sdService.GenerateImageAsync(request);
                    if (result != null && result.Success)
                    {
                        await File.WriteAllBytesAsync(pngPath, result.ImageData);
                        usedSdServer = true;

                        var sourceMessage = result.UsedFallback
                            ? $"Image generated via fallback badge generator (SD WebUI offline):\n  Prompt: {description}\n  Size: 512x512\n\nTo enable full AI image generation, start Stable Diffusion WebUI on localhost:7860."
                            : $"Image generated via Stable Diffusion:\n  Prompt: {description}\n  Size: 512x512, 25 steps\n\nThe SD WebUI server on localhost:7860 produced this image.";

                        Messages.Add(new ChatMessage
                        {
                            MessageId = Guid.NewGuid(),
                            Role = ChatRole.Assistant,
                            Content = sourceMessage,
                            Timestamp = DateTime.UtcNow,
                            ImagePath = pngPath
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                SGL.JudgeDredd.Shared.Logging.SglLogger.Warning("SD WebUI image gen failed, falling back to badge: {Error}", ex.Message);
            }

            // Fallback: System.Drawing badge generator
            if (!usedSdServer)
            {
                // Use LLM to interpret the description for badge text
                string badgeText = description;
                string subtitleText = "SGL SyntheticAI";

                if (_llmService.IsModelLoaded)
                {
                    var interpretPrompt = $"You are creating a security badge graphic. Given the description '{description}', " +
                        "provide a short 3-5 word badge title and a one-line subtitle. Format: TITLE|SUBTITLE. " +
                        "Example: CYBER SHIELD|Advanced Threat Protection. Only output the formatted text, nothing else.";

                    var result = new System.Text.StringBuilder();
                    await foreach (var token in _llmService.ChatAsync(interpretPrompt))
                    {
                        result.Append(token);
                    }

                    var parts = result.ToString().Trim().Split('|');
                    if (parts.Length >= 1) badgeText = parts[0].Trim();
                    if (parts.Length >= 2) subtitleText = parts[1].Trim();
                    if (badgeText.Length > 30) badgeText = badgeText[..30];
                    if (subtitleText.Length > 50) subtitleText = subtitleText[..50];
                }

                pngPath = Path.Combine(imagesDir, $"badge_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                await Task.Run(() =>
                {
                    using var bitmap = new System.Drawing.Bitmap(600, 600);
                    using var gfx = System.Drawing.Graphics.FromImage(bitmap);
                    gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    gfx.Clear(System.Drawing.Color.FromArgb(18, 18, 30));

                    using var glowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(50, 0, 150, 255), 6);
                    gfx.DrawEllipse(glowPen, 30, 30, 540, 540);

                    var shieldPoints = new System.Drawing.PointF[]
                    {
                        new(300, 60), new(480, 150), new(460, 380),
                        new(300, 500), new(140, 380), new(120, 150)
                    };

                    using var shieldFill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(30, 0, 120, 255));
                    using var shieldFillInner = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(50, 0, 180, 255));
                    gfx.FillPolygon(shieldFill, shieldPoints);

                    var innerShield = new System.Drawing.PointF[]
                    {
                        new(300, 90), new(450, 170), new(435, 360),
                        new(300, 460), new(165, 360), new(150, 170)
                    };
                    gfx.FillPolygon(shieldFillInner, innerShield);

                    using var shieldPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 150, 255), 3);
                    gfx.DrawPolygon(shieldPen, shieldPoints);
                    using var innerPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 0, 200, 255), 1.5f);
                    gfx.DrawPolygon(innerPen, innerShield);

                    using var linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 0, 150, 255), 1);
                    gfx.DrawLine(linePen, 180, 230, 420, 230);
                    gfx.DrawLine(linePen, 180, 340, 420, 340);

                    var sf = new System.Drawing.StringFormat
                    {
                        Alignment = System.Drawing.StringAlignment.Center,
                        LineAlignment = System.Drawing.StringAlignment.Center
                    };

                    using var jdFont = new System.Drawing.Font("Segoe UI", 56, System.Drawing.FontStyle.Bold);
                    using var jdBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 200, 255));
                    gfx.DrawString("JD", jdFont, jdBrush, 300, 180, sf);

                    using var titleFont = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
                    using var titleBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                    gfx.DrawString(badgeText.ToUpperInvariant(), titleFont, titleBrush, 300, 280, sf);

                    using var subFont = new System.Drawing.Font("Segoe UI", 10);
                    using var subBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(180, 180, 200));
                    gfx.DrawString(subtitleText, subFont, subBrush, 300, 310, sf);

                    using var descFont = new System.Drawing.Font("Segoe UI", 9);
                    using var descBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 150));
                    var shortDesc = description.Length > 50 ? description[..50] + "..." : description;
                    gfx.DrawString(shortDesc, descFont, descBrush, 300, 400, sf);

                    using var brandFont = new System.Drawing.Font("Segoe UI", 8);
                    using var brandBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(100, 100, 120));
                    gfx.DrawString("SYNTHETICAI SECURITY SUITE", brandFont, brandBrush, 300, 550, sf);

                    bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                });

                Messages.Add(new ChatMessage
                {
                    MessageId = Guid.NewGuid(),
                    Role = ChatRole.Assistant,
                    Content = $"Security badge generated (SD WebUI offline — using badge fallback):\n  Title: {badgeText}\n  Subtitle: {subtitleText}\n\nTo enable AI image generation, start Stable Diffusion WebUI on localhost:7860.",
                    Timestamp = DateTime.UtcNow,
                    ImagePath = pngPath
                });
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = $"Image generation failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            IsGenerating = false;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }

    [RelayCommand]
    private async Task AnalyzeDeviceAsync()
    {
        Messages.Add(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.User,
            Content = "/analyze - Running full device security analysis...",
            Timestamp = DateTime.UtcNow
        });

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        IsGenerating = true;

        string systemData;
        try
        {
            systemData = await Task.Run(GatherSystemData);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                Role = ChatRole.Assistant,
                Content = $"Failed to gather system data: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
            IsGenerating = false;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            return;
        }

        var assistantMessage = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = string.Empty,
            Timestamp = DateTime.UtcNow,
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        _generationCts = new CancellationTokenSource();

        var analysisPrompt =
            "You are performing a REAL security analysis of this Windows device. " +
            "The following is LIVE system data gathered right now. Analyze it thoroughly.\n\n" +
            "Look for:\n" +
            "1. Suspicious processes (high memory, unknown publishers, known malware names)\n" +
            "2. Unusual network connections or listening ports\n" +
            "3. Drive health concerns (low disk space, unusual mount points)\n" +
            "4. Security posture issues (protection disabled, pending alerts)\n" +
            "5. Anything that looks abnormal for a healthy Windows system\n\n" +
            "Provide a clear security assessment with severity rating (1-10) and actionable recommendations.\n\n" +
            "=== LIVE SYSTEM DATA ===\n" + systemData;

        try
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);

            await foreach (var token in _llmService.ChatAsync(analysisPrompt, BuildSystemPrompt(), _generationCts.Token))
            {
                assistantMessage.Content += token;
                UpdateThinkingDuringStreaming(assistantMessage);
                RefreshMessage(assistantMessage);
            }

            assistantMessage.IsStreaming = false;
            ParseThinkingTags(assistantMessage);
            RefreshMessage(assistantMessage);

            // Save analysis to knowledge base
            try
            {
                await _knowledgeBase.SaveThreatAnalysisAsync(
                    systemData,
                    assistantMessage.Content,
                    "Device security analysis via /analyze command",
                    0);
            }
            catch { /* Non-critical - analysis display still works */ }
        }
        catch (OperationCanceledException)
        {
            assistantMessage.Content += "\n[Analysis cancelled]";
            assistantMessage.IsStreaming = false;
        }
        catch (Exception ex)
        {
            assistantMessage.Content = $"Analysis failed: {ex.Message}\n\nRaw system data was collected successfully. The AI model may not be loaded.";
            assistantMessage.IsStreaming = false;
        }
        finally
        {
            IsGenerating = false;
            _generationCts?.Dispose();
            _generationCts = null;
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        }
    }

    private string GatherSystemData()
    {
        var sb = new StringBuilder();

        // Top processes by memory
        sb.AppendLine("== TOP 15 PROCESSES BY MEMORY ==");
        try
        {
            var processes = Process.GetProcesses()
                .Where(p => { try { return p.WorkingSet64 > 0; } catch { return false; } })
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                .Take(15);

            foreach (var proc in processes)
            {
                try
                {
                    var memMb = proc.WorkingSet64 / (1024.0 * 1024.0);
                    sb.AppendLine($"  {proc.ProcessName,-30} PID={proc.Id,-8} Memory={memMb:F1} MB");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error reading processes: {ex.Message}]");
        }

        // Suspicious process check
        sb.AppendLine();
        sb.AppendLine("== SUSPICIOUS PROCESS CHECK ==");
        var suspiciousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mimikatz", "meterpreter", "cobalt", "cobaltstrike", "empire",
            "powersploit", "rubeus", "sharphound", "bloodhound",
            "lazagne", "keylogger", "ratclient", "darkcomet", "njrat",
            "asyncrat", "quasarrat", "remcos", "nanocore", "orcus",
            "cryptominer", "xmrig", "minerd", "cgminer", "bfgminer"
        };
        try
        {
            var found = Process.GetProcesses()
                .Where(p => suspiciousNames.Any(s => p.ProcessName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (found.Count == 0)
                sb.AppendLine("  No known suspicious processes detected.");
            else
                foreach (var p in found)
                    sb.AppendLine($"  WARNING: Suspicious process '{p.ProcessName}' (PID={p.Id})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error: {ex.Message}]");
        }

        // Drive info
        sb.AppendLine();
        sb.AppendLine("== DRIVE SPACE ==");
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedPct = (1.0 - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100;
                sb.AppendLine($"  {drive.Name} [{drive.DriveType}] {drive.DriveFormat} - " +
                              $"{freeGb:F1}/{totalGb:F1} GB free ({usedPct:F0}% used)");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error: {ex.Message}]");
        }

        // Active network connections
        sb.AppendLine();
        sb.AppendLine("== ACTIVE TCP LISTENERS ==");
        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = ipProperties.GetActiveTcpListeners();
            foreach (var ep in listeners.Take(20))
            {
                sb.AppendLine($"  Listening on {ep.Address}:{ep.Port}");
            }

            if (listeners.Length > 20)
                sb.AppendLine($"  ... and {listeners.Length - 20} more");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error: {ex.Message}]");
        }

        // Active TCP connections
        sb.AppendLine();
        sb.AppendLine("== ACTIVE TCP CONNECTIONS ==");
        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = ipProperties.GetActiveTcpConnections();
            var established = connections.Where(c => c.State == TcpState.Established).Take(15).ToList();
            foreach (var conn in established)
            {
                sb.AppendLine($"  {conn.LocalEndPoint} -> {conn.RemoteEndPoint} [{conn.State}]");
            }

            if (connections.Length > 15)
                sb.AppendLine($"  ... and {connections.Length - 15} more total connections");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error: {ex.Message}]");
        }

        // Security status
        sb.AppendLine();
        sb.AppendLine("== SECURITY STATUS ==");
        sb.AppendLine($"  Real-Time Protection: {(_scanEngine.IsRealTimeProtectionActive ? "ACTIVE" : "INACTIVE")}");

        try
        {
            var alerts = _securityMonitor.GetActiveAlerts();
            sb.AppendLine($"  Active Security Alerts: {alerts.Count}");
            foreach (var alert in alerts.Take(5))
            {
                sb.AppendLine($"    [{alert.Severity}] {alert.Title}: {alert.Description}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [Error reading alerts: {ex.Message}]");
        }

        // System info
        sb.AppendLine();
        sb.AppendLine("== SYSTEM INFO ==");
        sb.AppendLine($"  Machine: {Environment.MachineName}");
        sb.AppendLine($"  OS: {Environment.OSVersion}");
        sb.AppendLine($"  64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"  Processors: {Environment.ProcessorCount}");
        sb.AppendLine($"  Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64):d\\.hh\\:mm\\:ss}");

        return sb.ToString();
    }
}
