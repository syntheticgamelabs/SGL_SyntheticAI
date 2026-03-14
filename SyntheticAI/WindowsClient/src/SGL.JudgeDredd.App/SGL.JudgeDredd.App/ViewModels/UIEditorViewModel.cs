using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class UIEditorViewModel : ViewModelBase
{
    private const string SettingsFilePath = "data/ui_settings.json";

    // Font settings
    [ObservableProperty]
    private string _selectedFontFamily = "Segoe UI";

    [ObservableProperty]
    private double _selectedFontSize = 14;

    // Color settings
    [ObservableProperty]
    private string _selectedFontColor = "#E0E0E0";

    [ObservableProperty]
    private string _selectedAccentColor = "#00FF88";

    [ObservableProperty]
    private string _selectedBackgroundColor = "#0D0D0D";

    [ObservableProperty]
    private string _selectedSurfaceColor = "#1A1A2E";

    [ObservableProperty]
    private string _selectedPrimaryColor = "#00FF88";

    [ObservableProperty]
    private string _selectedDangerColor = "#FF4444";

    [ObservableProperty]
    private string _selectedWarningColor = "#FFB800";

    // Image paths
    [ObservableProperty]
    private string _backgroundImagePath = string.Empty;

    // LLM Personality settings
    [ObservableProperty]
    private string _llmSystemPrompt = "You are an expert cybersecurity AI assistant integrated into the SGL SyntheticAI Security Suite. Your PRIMARY purpose is to provide genuinely useful, accurate, and actionable technical help.";

    [ObservableProperty]
    private string _llmPersonalityName = "SyntheticAI";

    [ObservableProperty]
    private string _chatAvatarImagePath = string.Empty;

    // Available fonts
    public ObservableCollection<string> AvailableFonts { get; } = new();

    // Font size constraints
    public double MinFontSize => 10;
    public double MaxFontSize => 24;

    public UIEditorViewModel()
    {
        Title = "UI Editor";

        AvailableFonts.Add("Segoe UI");
        AvailableFonts.Add("Consolas");
        AvailableFonts.Add("Courier New");
        AvailableFonts.Add("Arial");
        AvailableFonts.Add("Verdana");
        AvailableFonts.Add("Tahoma");
        AvailableFonts.Add("Trebuchet MS");
        AvailableFonts.Add("Georgia");
        AvailableFonts.Add("Times New Roman");
        AvailableFonts.Add("Comic Sans MS");

        if (File.Exists(SettingsFilePath))
        {
            LoadSettingsFromFile();
        }

        LoadLlmPersonality();
    }

    [RelayCommand]
    private async Task ApplyThemeAsync()
    {
        try
        {
            var resources = Application.Current.Resources;

            resources["BackgroundBrush"] = CreateBrush(SelectedBackgroundColor);
            resources["SurfaceBrush"] = CreateBrush(SelectedSurfaceColor);
            resources["PrimaryBrush"] = CreateBrush(SelectedPrimaryColor);
            resources["DangerBrush"] = CreateBrush(SelectedDangerColor);
            resources["WarningBrush"] = CreateBrush(SelectedWarningColor);
            resources["AccentBrush"] = CreateBrush(SelectedAccentColor);
            resources["TextPrimaryBrush"] = CreateBrush(SelectedFontColor);

            var fontFamily = new FontFamily(SelectedFontFamily);
            resources["DefaultFontFamily"] = fontFamily;
            resources["DefaultFontSize"] = SelectedFontSize;

            // Apply background image if set
            if (!string.IsNullOrEmpty(BackgroundImagePath) && File.Exists(BackgroundImagePath))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(BackgroundImagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    var imageBrush = new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.15 // Subtle background, not overpowering
                    };
                    imageBrush.Freeze();
                    resources["BackgroundBrush"] = imageBrush;
                }
                catch { /* Invalid image, keep color background */ }
            }

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble("Theme applied successfully.");
        }
        catch (Exception ex)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Failed to apply theme: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        SelectedFontFamily = "Segoe UI";
        SelectedFontSize = 14;
        SelectedFontColor = "#E0E0E0";
        SelectedAccentColor = "#00FF88";
        SelectedBackgroundColor = "#0D0D0D";
        SelectedSurfaceColor = "#1A1A2E";
        SelectedPrimaryColor = "#00FF88";
        SelectedDangerColor = "#FF4444";
        SelectedWarningColor = "#FFB800";
        BackgroundImagePath = string.Empty;
        LlmSystemPrompt = "You are an expert cybersecurity AI assistant integrated into the SGL SyntheticAI Security Suite. Your PRIMARY purpose is to provide genuinely useful, accurate, and actionable technical help.";
        LlmPersonalityName = "SyntheticAI";
        ChatAvatarImagePath = string.Empty;
    }

    [RelayCommand]
    private void BrowseBackground()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Background Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|PNG Files|*.png|JPEG Files|*.jpg;*.jpeg|BMP Files|*.bmp",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            BackgroundImagePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseChatAvatar()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Chat AI Avatar Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|PNG Files|*.png|JPEG Files|*.jpg;*.jpeg|BMP Files|*.bmp",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ChatAvatarImagePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task SaveLlmPersonalityAsync()
    {
        try
        {
            var personalityDir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(personalityDir);
            var personalityPath = Path.Combine(personalityDir, "llm_personality.json");

            var personality = new
            {
                systemPrompt = LlmSystemPrompt,
                personalityName = LlmPersonalityName,
                chatAvatarPath = ChatAvatarImagePath,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(personality, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(personalityPath, json);

            await AvatarViewModel.Instance.ShowSpeechBubble("LLM personality settings saved. Restart chat to apply.");
        }
        catch (Exception ex)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble($"Failed to save personality: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadLlmPersonality()
    {
        try
        {
            var personalityPath = Path.Combine(AppContext.BaseDirectory, "data", "llm_personality.json");
            if (!File.Exists(personalityPath)) return;

            var json = File.ReadAllText(personalityPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("systemPrompt", out var sp))
                LlmSystemPrompt = sp.GetString() ?? LlmSystemPrompt;
            if (root.TryGetProperty("personalityName", out var pn))
                LlmPersonalityName = pn.GetString() ?? LlmPersonalityName;
            if (root.TryGetProperty("chatAvatarPath", out var ca))
                ChatAvatarImagePath = ca.GetString() ?? string.Empty;
        }
        catch { /* Use defaults */ }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new UISettings
            {
                FontFamily = SelectedFontFamily,
                FontSize = SelectedFontSize,
                FontColor = SelectedFontColor,
                AccentColor = SelectedAccentColor,
                BackgroundColor = SelectedBackgroundColor,
                SurfaceColor = SelectedSurfaceColor,
                PrimaryColor = SelectedPrimaryColor,
                DangerColor = SelectedDangerColor,
                WarningColor = SelectedWarningColor,
                BackgroundImagePath = BackgroundImagePath,
                LlmSystemPrompt = LlmSystemPrompt,
                LlmPersonalityName = LlmPersonalityName,
                ChatAvatarImagePath = ChatAvatarImagePath
            };

            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(SettingsFilePath, json);

            await AvatarViewModel.Instance.ShowSpeechBubble("UI settings saved.");
        }
        catch (Exception ex)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Failed to save settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            LoadSettingsFromFile();
            await AvatarViewModel.Instance.ShowSpeechBubble("UI settings loaded.");
        }
        catch (Exception ex)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Failed to load settings: {ex.Message}");
        }
    }

    private void LoadSettingsFromFile()
    {
        if (!File.Exists(SettingsFilePath))
            return;

        var json = File.ReadAllText(SettingsFilePath);
        var settings = JsonSerializer.Deserialize<UISettings>(json);

        if (settings is null)
            return;

        SelectedFontFamily = settings.FontFamily;
        SelectedFontSize = settings.FontSize;
        SelectedFontColor = settings.FontColor;
        SelectedAccentColor = settings.AccentColor;
        SelectedBackgroundColor = settings.BackgroundColor;
        SelectedSurfaceColor = settings.SurfaceColor;
        SelectedPrimaryColor = settings.PrimaryColor;
        SelectedDangerColor = settings.DangerColor;
        SelectedWarningColor = settings.WarningColor;
        BackgroundImagePath = settings.BackgroundImagePath;
        LlmSystemPrompt = settings.LlmSystemPrompt;
        LlmPersonalityName = settings.LlmPersonalityName;
        ChatAvatarImagePath = settings.ChatAvatarImagePath;
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexColor);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed class UISettings
    {
        [JsonPropertyName("fontFamily")]
        public string FontFamily { get; set; } = "Segoe UI";

        [JsonPropertyName("fontSize")]
        public double FontSize { get; set; } = 14;

        [JsonPropertyName("fontColor")]
        public string FontColor { get; set; } = "#E0E0E0";

        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#00FF88";

        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#0D0D0D";

        [JsonPropertyName("surfaceColor")]
        public string SurfaceColor { get; set; } = "#1A1A2E";

        [JsonPropertyName("primaryColor")]
        public string PrimaryColor { get; set; } = "#00FF88";

        [JsonPropertyName("dangerColor")]
        public string DangerColor { get; set; } = "#FF4444";

        [JsonPropertyName("warningColor")]
        public string WarningColor { get; set; } = "#FFB800";

        [JsonPropertyName("backgroundImagePath")]
        public string BackgroundImagePath { get; set; } = string.Empty;

        [JsonPropertyName("llmSystemPrompt")]
        public string LlmSystemPrompt { get; set; } = "";

        [JsonPropertyName("llmPersonalityName")]
        public string LlmPersonalityName { get; set; } = "SyntheticAI";

        [JsonPropertyName("chatAvatarImagePath")]
        public string ChatAvatarImagePath { get; set; } = string.Empty;
    }
}
