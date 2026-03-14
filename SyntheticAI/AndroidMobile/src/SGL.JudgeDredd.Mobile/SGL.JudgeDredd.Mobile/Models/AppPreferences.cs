namespace SGL.JudgeDredd.Mobile.Models;

/// <summary>
/// Persistent app preferences stored via MAUI Preferences API.
/// </summary>
public class AppPreferences
{
    private const string KeyServerUrl = "server_url";
    private const string KeyAuthToken = "auth_token";
    private const string KeyClientId = "client_id";
    private const string KeyUsername = "username";
    private const string KeyRememberMe = "remember_me";
    private const string KeyNotificationsEnabled = "notifications_enabled";
    private const string KeyTtsEnabled = "tts_enabled";
    private const string KeySelectedLlmModel = "selected_llm_model";
    private const string KeyLastScanTime = "last_scan_time";
    private const string KeyThreatsBlocked = "threats_blocked";
    private const string KeyAiAssistantName = "ai_assistant_name";
    private const string KeyAiSystemPrompt = "ai_system_prompt";
    private const string KeyAiCreativeMode = "ai_creative_mode";
    private const string KeyTtsVoice = "tts_voice";
    private const string KeyUseOfflineMode = "use_offline_mode";
    private const string KeyHasSeenLlmPrompt = "has_seen_llm_prompt";

    private const string DefaultServerUrl = "https://syntheticgamelabs.dpdns.org";

    public string ServerUrl
    {
        get => Preferences.Default.Get(KeyServerUrl, DefaultServerUrl);
        set => Preferences.Default.Set(KeyServerUrl, value);
    }

    public string AuthToken
    {
        get => Preferences.Default.Get(KeyAuthToken, string.Empty);
        set => Preferences.Default.Set(KeyAuthToken, value);
    }

    public string ClientId
    {
        get => Preferences.Default.Get(KeyClientId, string.Empty);
        set => Preferences.Default.Set(KeyClientId, value);
    }

    public string Username
    {
        get => Preferences.Default.Get(KeyUsername, string.Empty);
        set => Preferences.Default.Set(KeyUsername, value);
    }

    public bool RememberMe
    {
        get => Preferences.Default.Get(KeyRememberMe, false);
        set => Preferences.Default.Set(KeyRememberMe, value);
    }

    public bool NotificationsEnabled
    {
        get => Preferences.Default.Get(KeyNotificationsEnabled, true);
        set => Preferences.Default.Set(KeyNotificationsEnabled, value);
    }

    public bool TtsEnabled
    {
        get => Preferences.Default.Get(KeyTtsEnabled, false);
        set => Preferences.Default.Set(KeyTtsEnabled, value);
    }

    public string SelectedLlmModel
    {
        get => Preferences.Default.Get(KeySelectedLlmModel, string.Empty);
        set => Preferences.Default.Set(KeySelectedLlmModel, value);
    }

    public string LastScanTime
    {
        get => Preferences.Default.Get(KeyLastScanTime, "Never");
        set => Preferences.Default.Set(KeyLastScanTime, value);
    }

    public int ThreatsBlocked
    {
        get => Preferences.Default.Get(KeyThreatsBlocked, 0);
        set => Preferences.Default.Set(KeyThreatsBlocked, value);
    }

    public string AiAssistantName
    {
        get => Preferences.Default.Get(KeyAiAssistantName, "SyntheticAI");
        set => Preferences.Default.Set(KeyAiAssistantName, value);
    }

    public string AiSystemPrompt
    {
        get => Preferences.Default.Get(KeyAiSystemPrompt, string.Empty);
        set => Preferences.Default.Set(KeyAiSystemPrompt, value);
    }

    public bool AiCreativeMode
    {
        get => Preferences.Default.Get(KeyAiCreativeMode, false);
        set => Preferences.Default.Set(KeyAiCreativeMode, value);
    }

    public string TtsVoice
    {
        get => Preferences.Default.Get(KeyTtsVoice, "Chelsie");
        set => Preferences.Default.Set(KeyTtsVoice, value);
    }

    public bool UseOfflineMode
    {
        get => Preferences.Default.Get(KeyUseOfflineMode, false);
        set => Preferences.Default.Set(KeyUseOfflineMode, value);
    }

    public bool HasSeenLlmPrompt
    {
        get => Preferences.Default.Get(KeyHasSeenLlmPrompt, false);
        set => Preferences.Default.Set(KeyHasSeenLlmPrompt, value);
    }

    /// <summary>
    /// Clears all stored preferences (logout).
    /// </summary>
    public void ClearAll()
    {
        var serverUrl = ServerUrl; // preserve server URL
        Preferences.Default.Clear();
        ServerUrl = serverUrl;
    }

    /// <summary>
    /// Clears authentication data only.
    /// </summary>
    public void ClearAuth()
    {
        AuthToken = string.Empty;
        ClientId = string.Empty;
    }
}
