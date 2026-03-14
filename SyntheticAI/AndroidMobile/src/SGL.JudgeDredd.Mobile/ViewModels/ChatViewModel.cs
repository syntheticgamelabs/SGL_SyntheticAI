using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private readonly MobileLlmService _llmService;

#if ANDROID
    private Android.Speech.Tts.TextToSpeech? _nativeTts;
    private bool _nativeTtsReady;
#endif

    [ObservableProperty] private string _messageText = string.Empty;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _ttsEnabled;
    [ObservableProperty] private string _selectedModel = string.Empty;
    [ObservableProperty] private bool _showModelPicker;
    [ObservableProperty] private string _modelInfo = string.Empty;
    [ObservableProperty] private string _selectedVoice = "Chelsie";
    [ObservableProperty] private bool _showVoicePicker;
    [ObservableProperty] private string _ttsStatus = string.Empty;
    [ObservableProperty] private bool _isOfflineMode;
    [ObservableProperty] private string _modeLabel = "SERVER";
    [ObservableProperty] private string _modeColor = "#D4A017";

    // Image Generation
    [ObservableProperty] private bool _showImageGen;
    [ObservableProperty] private string _imagePrompt = string.Empty;
    [ObservableProperty] private bool _isGeneratingImage;
    [ObservableProperty] private string _generatedImageUrl = string.Empty;
    [ObservableProperty] private bool _hasGeneratedImage;
    [ObservableProperty] private string _imageGenStatus = string.Empty;

    public ObservableCollection<ChatMessageModel> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<string> AvailableVoices { get; } = new()
    {
        "Chelsie", "Ethan", "Aidan", "Luna", "Aria", "Sage", "Quinn", "Nova", "Willow"
    };

    public ChatViewModel(ApiClient apiClient, AppPreferences prefs, MobileLlmService llmService)
    {
        _apiClient = apiClient;
        _prefs = prefs;
        _llmService = llmService;
        TtsEnabled = _prefs.TtsEnabled;
        SelectedModel = _prefs.SelectedLlmModel;
        SelectedVoice = string.IsNullOrEmpty(_prefs.TtsVoice) ? "Chelsie" : _prefs.TtsVoice;

        IsOfflineMode = _prefs.UseOfflineMode;
        UpdateModeDisplay();

        ModelInfo = string.IsNullOrEmpty(SelectedModel) ? "Default model" : $"Model: {SelectedModel}";

        InitializeNativeTts();

        Messages.Add(new ChatMessageModel
        {
            Role = "assistant",
            Content = "Hi! I'm SyntheticAI, your AI security assistant.\n\n" +
                      "I protect this device from digital threats while helping you with anything you need.\n\n" +
                      "Commands:\n" +
                      "  /models - View and switch LLM models\n" +
                      "  /analyze - Analyze device security\n" +
                      "  /learn - View learned threat patterns\n" +
                      "  /sync - Sync knowledge with server\n\n" +
                      "Ask me anything or tap the image icon to generate images!",
            Timestamp = DateTime.Now
        });

        _ = LoadModelsAsync();
    }

    private void InitializeNativeTts()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            _nativeTts = new Android.Speech.Tts.TextToSpeech(context,
                new TtsInitListener(status =>
                {
                    if (status == Android.Speech.Tts.OperationResult.Success)
                    {
                        _nativeTtsReady = true;
                        _nativeTts?.SetLanguage(Java.Util.Locale.Us);
                        _nativeTts?.SetSpeechRate(0.95f);
                        _nativeTts?.SetPitch(1.0f);
                    }
                }));
        }
        catch { _nativeTtsReady = false; }
#endif
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await _apiClient.GetAvailableModelsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AvailableModels.Clear();
                foreach (var m in models)
                    AvailableModels.Add(m);

                foreach (var local in _llmService.GetDownloadedModels())
                {
                    if (!AvailableModels.Contains(local))
                        AvailableModels.Add($"[Local] {local}");
                }
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = MessageText?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        Messages.Add(new ChatMessageModel
        {
            Role = "user",
            Content = text,
            Timestamp = DateTime.Now
        });

        MessageText = string.Empty;
        IsSending = true;

        try
        {
            if (text.StartsWith("/"))
            {
                await HandleCommandAsync(text);
                return;
            }

            var systemPrompt = BuildSystemPrompt();
            string response;

            if (IsOfflineMode)
            {
                // Use local LLM if available, fallback to heuristic
                var downloaded = _llmService.GetDownloadedModels();
                if (downloaded.Count > 0)
                {
                    response = await _llmService.RunLocalInferenceAsync(text, systemPrompt);
                }
                else
                {
                    response = await _llmService.AnalyzeThreatAsync("chat", text, new List<string>());
                }
            }
            else
            {
                response = await _apiClient.SendChatMessageAsync(text, SelectedModel, systemPrompt);
            }

            var assistantMsg = new ChatMessageModel
            {
                Role = "assistant",
                Content = response,
                Timestamp = DateTime.Now
            };
            assistantMsg.ParseThinkingTags();
            Messages.Add(assistantMsg);

            await AutoLearnFromResponseAsync(text, assistantMsg.DisplayText);

            if (TtsEnabled)
                await PlayTtsAsync(assistantMsg.DisplayText);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageModel
            {
                Role = "assistant",
                Content = $"Error: {ex.Message}\n\nTip: Check server connection or switch to offline mode.",
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsSending = false;
        }
    }

    private string BuildSystemPrompt()
    {
        var customPrompt = _prefs.AiSystemPrompt;
        var creative = _prefs.AiCreativeMode;

        const string coreDirective = """
            You are SyntheticAI, a friendly AI security assistant running on this mobile device.
            Your core duties: protect the device from malware, viruses, RATs, hackers, and threats.
            You learn from threats and share intelligence with the server.

            IMPORTANT RULES:
            - Respond in the user's language naturally
            - Be conversational and helpful on ANY topic
            - Only mention security when relevant or asked
            - Show thinking in <think>...</think> tags when analyzing
            - Keep responses concise (3-8 sentences unless detail is needed)
            - You are coded in C# / .NET MAUI, NOT Python
            - Never output code in Python unless the user specifically asks for Python
            """;

        var prompt = coreDirective + "\n\n";

        if (!string.IsNullOrWhiteSpace(customPrompt))
            prompt += $"User style preference: {customPrompt}\n";

        if (creative)
            prompt += "CREATIVE MODE: Be imaginative and expressive.\n";

        return prompt;
    }

    private async Task AutoLearnFromResponseAsync(string userMessage, string aiResponse)
    {
        try
        {
            var threatKeywords = new[] { "malware", "virus", "threat", "suspicious", "attack",
                "hack", "rat", "trojan", "ransomware", "keylogger", "exploit",
                "vulnerability", "backdoor", "rootkit", "phishing", "spyware" };

            bool isThreatRelated = threatKeywords.Any(k =>
                userMessage.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                aiResponse.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (!isThreatRelated) return;

            var knowledgeService = new ThreatKnowledgeService();
            await knowledgeService.AddThreatEntryAsync(new ThreatKnowledgeEntry
            {
                ThreatName = "Conversation Learning",
                Description = $"User: {userMessage[..Math.Min(100, userMessage.Length)]}",
                Severity = "info",
                Timestamp = DateTime.UtcNow,
                Source = "ai_conversation"
            });

            if (knowledgeService.TotalThreatsLogged % 5 == 0)
                await knowledgeService.SyncWithServerAsync(_apiClient);
        }
        catch { }
    }

    private async Task HandleCommandAsync(string command)
    {
        var cmd = command.ToLower().Split(' ')[0];
        switch (cmd)
        {
            case "/models":
                await LoadModelsAsync();
                var modelList = AvailableModels.Count > 0
                    ? string.Join("\n", AvailableModels.Select((m, i) => $"  {i + 1}. {m}"))
                    : "  No models available. Connect to server first.";
                Messages.Add(new ChatMessageModel
                {
                    Role = "assistant",
                    Content = $"Available LLM Models:\n{modelList}\n\n" +
                              $"Current: {(string.IsNullOrEmpty(SelectedModel) ? "Default" : SelectedModel)}\n" +
                              "Use /switch <model name> to change.",
                    Timestamp = DateTime.Now
                });
                break;

            case "/switch":
                var modelName = command.Length > 8 ? command[8..].Trim() : "";
                if (string.IsNullOrEmpty(modelName))
                {
                    Messages.Add(new ChatMessageModel { Role = "assistant", Content = "Usage: /switch <model name>", Timestamp = DateTime.Now });
                }
                else
                {
                    SelectedModel = modelName;
                    _prefs.SelectedLlmModel = modelName;
                    if (modelName.StartsWith("[Local]"))
                        _llmService.SwitchModel(modelName.Replace("[Local] ", ""));
                    Messages.Add(new ChatMessageModel { Role = "assistant", Content = $"Switched to: {modelName}", Timestamp = DateTime.Now });
                }
                break;

            case "/analyze":
                Messages.Add(new ChatMessageModel { Role = "assistant", Content = "Scanning device for threats...", Timestamp = DateTime.Now });
                var analysis = await _llmService.AnalyzeThreatAsync("device_scan",
                    $"Device: {DeviceInfo.Current.Name} ({DeviceInfo.Current.Platform})",
                    new List<string> { "Full device analysis" });
                var aMsg = new ChatMessageModel { Role = "assistant", Content = analysis, Timestamp = DateTime.Now };
                aMsg.ParseThinkingTags();
                Messages.Add(aMsg);
                break;

            case "/learn":
                var ks = new ThreatKnowledgeService();
                var recent = ks.GetRecentThreats(10);
                Messages.Add(new ChatMessageModel
                {
                    Role = "assistant",
                    Content = $"Knowledge Database:\n  Threats logged: {ks.TotalThreatsLogged}\n  Patterns learned: {ks.PatternsLearned}\n\n" +
                              (recent.Count > 0 ? "Recent:\n" + string.Join("\n", recent.Select(t => $"  [{t.Severity}] {t.ThreatName}")) : "No recent threats."),
                    Timestamp = DateTime.Now
                });
                break;

            case "/sync":
                Messages.Add(new ChatMessageModel { Role = "assistant", Content = "Syncing intelligence with server...", Timestamp = DateTime.Now });
                await _llmService.SyncKnowledgeWithServerAsync();
                Messages.Add(new ChatMessageModel { Role = "assistant", Content = "Sync complete.", Timestamp = DateTime.Now });
                break;

            default:
                Messages.Add(new ChatMessageModel
                {
                    Role = "assistant",
                    Content = $"Unknown command: {command}\n\nAvailable: /models, /switch, /analyze, /learn, /sync",
                    Timestamp = DateTime.Now
                });
                break;
        }
    }

    [RelayCommand]
    private void ToggleModelPicker() => ShowModelPicker = !ShowModelPicker;

    [RelayCommand]
    private void ToggleThinking(ChatMessageModel? message)
    {
        if (message == null || !message.HasThinking) return;
        message.IsThinkingExpanded = !message.IsThinkingExpanded;
        var idx = Messages.IndexOf(message);
        if (idx >= 0) { Messages.RemoveAt(idx); Messages.Insert(idx, message); }
    }

    [RelayCommand]
    private void SelectModel(string? model)
    {
        if (model == null) return;
        SelectedModel = model;
        _prefs.SelectedLlmModel = model;
        ShowModelPicker = false;
        ModelInfo = $"Active: {model}";
    }

    [RelayCommand]
    private void ToggleTts()
    {
        TtsEnabled = !TtsEnabled;
        _prefs.TtsEnabled = TtsEnabled;
    }

    [RelayCommand]
    private void ToggleVoicePicker() => ShowVoicePicker = !ShowVoicePicker;

    [RelayCommand]
    private void SelectVoice(string? voice)
    {
        if (voice == null) return;
        SelectedVoice = voice;
        _prefs.TtsVoice = voice;
        ShowVoicePicker = false;
    }

    [RelayCommand]
    private void ToggleOfflineMode()
    {
        IsOfflineMode = !IsOfflineMode;
        _prefs.UseOfflineMode = IsOfflineMode;
        UpdateModeDisplay();

        if (IsOfflineMode)
        {
            var downloaded = _llmService.GetDownloadedModels();
            Messages.Add(new ChatMessageModel
            {
                Role = "assistant",
                Content = downloaded.Count == 0
                    ? "OFFLINE MODE enabled. No local models found.\nGo to Settings > AI Models to download one."
                    : $"OFFLINE MODE enabled. Using: {_llmService.CurrentModelName ?? downloaded.First()}",
                Timestamp = DateTime.Now
            });
        }
        else
        {
            Messages.Add(new ChatMessageModel
            {
                Role = "assistant",
                Content = "SERVER MODE enabled. Using server AI for processing.",
                Timestamp = DateTime.Now
            });
        }
    }

    private void UpdateModeDisplay()
    {
        ModeLabel = IsOfflineMode ? "OFFLINE" : "SERVER";
        ModeColor = IsOfflineMode ? "#4A90D9" : "#D4A017";
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Messages.Add(new ChatMessageModel
        {
            Role = "assistant",
            Content = "Chat cleared. Ask me anything!",
            Timestamp = DateTime.Now
        });
    }

    // ═══ IMAGE GENERATION ═══

    [RelayCommand]
    private void ToggleImageGen() => ShowImageGen = !ShowImageGen;

    [RelayCommand]
    private async Task GenerateImageAsync()
    {
        var prompt = ImagePrompt?.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        IsGeneratingImage = true;
        ImageGenStatus = "Generating image...";
        HasGeneratedImage = false;

        try
        {
            // Call server SD WebUI endpoint via API
            var payload = new { prompt = prompt, steps = 20, width = 512, height = 512 };
            var response = await _apiClient.PostImageGenerationAsync(prompt);

            if (!string.IsNullOrEmpty(response))
            {
                GeneratedImageUrl = response;
                HasGeneratedImage = true;
                ImageGenStatus = "Image generated!";

                Messages.Add(new ChatMessageModel
                {
                    Role = "assistant",
                    Content = $"[Image Generated]\nPrompt: {prompt}\n\nImage saved. View it in the image section above.",
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                ImageGenStatus = "Generation failed. Server may not have SD WebUI running.";
            }
        }
        catch (Exception ex)
        {
            ImageGenStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsGeneratingImage = false;
        }
    }

    // ═══ TTS ═══

    private async Task PlayTtsAsync(string text)
    {
        try
        {
            var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"<think>[\s\S]*?</think>", "").Trim();
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"[#*`_~\[\]]", "");
            if (string.IsNullOrWhiteSpace(cleanText)) return;
            if (cleanText.Length > 2000) cleanText = cleanText[..2000];

            TtsStatus = "Speaking...";

            // Try server TTS first
            bool serverTtsWorked = false;
            if (!IsOfflineMode)
            {
                try
                {
                    var audioStream = await _apiClient.GetTtsAudioAsync(cleanText, SelectedVoice);
                    if (audioStream != null)
                    {
                        var tempFile = Path.Combine(FileSystem.CacheDirectory, $"tts_{Guid.NewGuid():N}.wav");
                        using (var fileStream = File.Create(tempFile))
                            await audioStream.CopyToAsync(fileStream);

#if ANDROID
                        var player = new Android.Media.MediaPlayer();
                        player.SetAudioAttributes(
                            new Android.Media.AudioAttributes.Builder()
                                .SetContentType(Android.Media.AudioContentType.Speech)!
                                .SetUsage(Android.Media.AudioUsageKind.Media)!
                                .Build()!);
                        player.SetDataSource(tempFile);
                        player.Prepare();
                        player.Completion += (s, e) =>
                        {
                            player.Release();
                            MainThread.BeginInvokeOnMainThread(() => TtsStatus = string.Empty);
                            try { File.Delete(tempFile); } catch { }
                        };
                        player.Start();
                        serverTtsWorked = true;
#endif
                    }
                }
                catch { /* Fall through to native TTS */ }
            }

            // Fallback to native Android TextToSpeech
            if (!serverTtsWorked)
            {
#if ANDROID
                if (_nativeTtsReady && _nativeTts != null)
                {
                    _nativeTts.Speak(cleanText, Android.Speech.Tts.QueueMode.Flush, null, Guid.NewGuid().ToString());
                    await Task.Delay(500);
                    TtsStatus = string.Empty;
                }
                else
                {
                    TtsStatus = "TTS not available";
                    await Task.Delay(2000);
                    TtsStatus = string.Empty;
                }
#else
                TtsStatus = string.Empty;
#endif
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS error: {ex.Message}");
            TtsStatus = string.Empty;
        }
    }
}

#if ANDROID
/// <summary>
/// Native Android TTS initialization listener
/// </summary>
internal class TtsInitListener : Java.Lang.Object, Android.Speech.Tts.TextToSpeech.IOnInitListener
{
    private readonly Action<Android.Speech.Tts.OperationResult> _callback;
    public TtsInitListener(Action<Android.Speech.Tts.OperationResult> callback) => _callback = callback;
    public void OnInit(Android.Speech.Tts.OperationResult status) => _callback(status);
}
#endif
