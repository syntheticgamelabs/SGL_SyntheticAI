using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class BroadcastViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppPreferences _prefs;
    private Timer? _refreshTimer;
    private bool _isPageVisible;

    [ObservableProperty] private string _broadcastInput = string.Empty;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private string _statusText = "Broadcast Board - messages visible to all connected clients";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _chatMessageText = string.Empty;
    [ObservableProperty] private int _connectedClients;
    [ObservableProperty] private string _connectionStatus = "Checking...";

    public ObservableCollection<BroadcastMessageItem> Messages { get; } = new();
    public ObservableCollection<ChatMessageItem> ChatMessages { get; } = new();

    public BroadcastViewModel(ApiClient apiClient, AppPreferences prefs)
    {
        _apiClient = apiClient;
        _prefs = prefs;
    }

    public void OnAppearing()
    {
        _isPageVisible = true;
        _ = SafeLoadMessagesAsync();
        StartRefreshTimer();
    }

    public void OnDisappearing()
    {
        _isPageVisible = false;
        StopRefreshTimer();
    }

    private void StartRefreshTimer()
    {
        StopRefreshTimer();
        _refreshTimer = new Timer(_ =>
        {
            if (!_isPageVisible) return;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await SafeLoadMessagesAsync(); }
                catch { /* suppress */ }
            });
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void StopRefreshTimer()
    {
        try
        {
            _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _refreshTimer?.Dispose();
        }
        catch { }
        _refreshTimer = null;
    }

    private async Task SafeLoadMessagesAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var broadcast = await _apiClient.GetBroadcastAsync();
            if (broadcast != null && !string.IsNullOrEmpty(broadcast.Message))
            {
                var existing = Messages.FirstOrDefault(m =>
                    m.Message == broadcast.Message && m.Username == (broadcast.SentBy ?? "server"));

                if (existing == null)
                {
                    var isAdmin = broadcast.SentBy?.ToLowerInvariant() == "admin" ||
                                  broadcast.SentBy?.ToLowerInvariant() == "server";

                    Messages.Insert(0, new BroadcastMessageItem
                    {
                        Username = broadcast.SentBy ?? "server",
                        Message = broadcast.Message,
                        Timestamp = broadcast.SentAt,
                        IsAdmin = isAdmin
                    });
                }
            }

            ConnectionStatus = "Connected";
            
            // Try to get client count from server status
            try
            {
                var status = await _apiClient.GetServerStatusAsync();
                if (status != null)
                    ConnectedClients = status.OnlineClients;
            }
            catch { }
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Disconnected";
            StatusText = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        await SafeLoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SendBroadcastMessageAsync()
    {
        var text = BroadcastInput?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        IsSending = true;
        try
        {
            var username = string.IsNullOrEmpty(_prefs.Username) ? "Mobile User" : _prefs.Username;
            await _apiClient.SendBroadcastAsync(text, "info");

            Messages.Insert(0, new BroadcastMessageItem
            {
                Username = username,
                Message = text,
                Timestamp = DateTime.Now,
                IsAdmin = false
            });

            BroadcastInput = string.Empty;
            StatusText = "Broadcast sent to all connected clients";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to send: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task SendChatMessageAsync()
    {
        var text = ChatMessageText?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            var username = string.IsNullOrEmpty(_prefs.Username) ? "Mobile User" : _prefs.Username;

            // Send via proper ChatRoom endpoint instead of broadcast hack
            var sent = await _apiClient.SendChatRoomMessageAsync("general", text, username);

            ChatMessages.Add(new ChatMessageItem
            {
                Sender = username,
                Text = text,
                Timestamp = DateTime.Now
            });

            ChatMessageText = string.Empty;

            if (!sent)
                StatusText = "Message may not have reached server";
        }
        catch (Exception ex)
        {
            StatusText = $"Chat send failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearMessagesAsync()
    {
        try
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Clear Messages",
                "Clear all broadcast messages from view?",
                "Clear", "Cancel");

            if (confirm)
            {
                Messages.Clear();
                StatusText = "Messages cleared locally";
            }
        }
        catch { }
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
    }
}
