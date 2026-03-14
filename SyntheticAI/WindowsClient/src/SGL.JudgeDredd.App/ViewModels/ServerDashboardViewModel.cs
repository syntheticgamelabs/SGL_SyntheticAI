using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Configuration;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.App.ViewModels;

/// <summary>
/// ViewModel for the Server Dashboard view. Shows connected clients,
/// server status, and real-time monitoring of the API server.
/// </summary>
public partial class ServerDashboardViewModel : ViewModelBase
{
    private readonly ConnectedClientTracker _tracker;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private int _onlineClientCount;

    [ObservableProperty]
    private int _totalRegisteredClients;

    [ObservableProperty]
    private string _serverStatus = "Running";

    [ObservableProperty]
    private string _serverUptime = "00:00:00";

    [ObservableProperty]
    private string _listenAddress = "0.0.0.0:5000";

    [ObservableProperty]
    private string _publicUrl = "https://syntheticgamelabs.dpdns.org";

    [ObservableProperty]
    private int _totalThreatsDetected;

    [ObservableProperty]
    private int _totalFilesScanned;

    [ObservableProperty]
    private bool _isActive;

    public ObservableCollection<ClientDisplayItem> ConnectedClients { get; } = [];
    public ObservableCollection<string> ServerLog { get; } = [];

    private readonly DateTime _serverStartedAt = DateTime.UtcNow;

    public ServerDashboardViewModel(ConnectedClientTracker tracker, int port = 5000, ServerSettings? serverSettings = null)
    {
        Title = "Server Dashboard";
        _tracker = tracker;
        ListenAddress = $"0.0.0.0:{port}";

        if (serverSettings != null)
        {
            PublicUrl = serverSettings.PublicUrl;
        }

        // Create timer but don't start — controlled by IsActive
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += (_, _) => RefreshData();

        AddLogEntry("Server Dashboard initialized.");
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
        {
            RefreshData();
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    [RelayCommand]
    private void RefreshData()
    {
        try
        {
            // Update uptime
            var uptime = DateTime.UtcNow - _serverStartedAt;
            ServerUptime = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            // Prune stale clients
            _tracker.PruneStaleClients();

            // Update counts
            OnlineClientCount = _tracker.OnlineCount;
            TotalRegisteredClients = _tracker.TotalCount;

            // Update client list
            var clients = _tracker.GetAllClients();
            ConnectedClients.Clear();

            int totalThreats = 0;
            int totalScanned = 0;

            foreach (var client in clients)
            {
                totalThreats += client.ThreatsDetected;
                totalScanned += client.FilesScanned;

                ConnectedClients.Add(new ClientDisplayItem
                {
                    Username = client.Username,
                    MachineName = client.MachineName,
                    Platform = client.Platform,
                    DeviceModel = client.DeviceModel,
                    OsVersion = client.OsVersion,
                    ClientVersion = client.ClientVersion,
                    Status = client.IsOnline ? "Online" : "Offline",
                    LastSeen = FormatRelativeTime(client.LastHeartbeat),
                    ThreatsDetected = client.ThreatsDetected,
                    FilesScanned = client.FilesScanned,
                    RealTimeProtection = client.RealTimeProtectionActive ? "Active" : "Inactive",
                    LlmLoaded = client.LlmModelLoaded ? "Yes" : "No",
                    Uptime = FormatDuration(TimeSpan.FromMinutes(client.UptimeMinutes))
                });
            }

            TotalThreatsDetected = totalThreats;
            TotalFilesScanned = totalScanned;
        }
        catch (Exception ex)
        {
            SglLogger.Error("Server Dashboard RefreshData failed: " + ex.Message);
            ServerStatus = "Error: " + ex.Message;
        }
    }

    private void AddLogEntry(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ServerLog.Insert(0, entry);

        // Keep log at reasonable size
        while (ServerLog.Count > 200)
            ServerLog.RemoveAt(ServerLog.Count - 1);
    }

    private static string FormatRelativeTime(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{duration.Minutes}m";
    }
}

/// <summary>
/// Display model for a connected client in the DataGrid.
/// </summary>
public class ClientDisplayItem
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastSeen { get; set; } = string.Empty;
    public int ThreatsDetected { get; set; }
    public int FilesScanned { get; set; }
    public string RealTimeProtection { get; set; } = string.Empty;
    public string LlmLoaded { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
}
