using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security.Monitors;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class NetworkMonitorViewModel : ViewModelBase
{
    private readonly NetworkMonitorService _service = new();

    [ObservableProperty] private int _totalConnections;
    [ObservableProperty] private int _establishedConnections;
    [ObservableProperty] private int _listeningPorts;
    [ObservableProperty] private long _bytesSent;
    [ObservableProperty] private long _bytesReceived;
    [ObservableProperty] private int _suspiciousConnections;
    [ObservableProperty] private int _interfaceCount;
    [ObservableProperty] private int _dnsCacheEntries;
    [ObservableProperty] private string _lastRefreshTime = "Never";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _refreshStatus = "Ready";

    public ObservableCollection<NetworkConnectionInfo> ActiveConnections => _service.ActiveConnections;
    public ObservableCollection<NetworkInterfaceInfo> Interfaces => _service.Interfaces;
    public ObservableCollection<DnsCacheEntry> DnsCache => _service.DnsCache;

    public NetworkMonitorViewModel()
    {
        Title = "Network Monitor";
    }

    [RelayCommand]
    private async Task RefreshNetworkAsync()
    {
        IsRefreshing = true;
        RefreshStatus = "Scanning network...";
        try
        {
            await _service.RefreshAsync();
            TotalConnections = _service.TotalConnections;
            EstablishedConnections = _service.EstablishedConnections;
            ListeningPorts = _service.ListeningPorts;
            BytesSent = _service.BytesSent;
            BytesReceived = _service.BytesReceived;
            SuspiciousConnections = _service.SuspiciousConnections;
            InterfaceCount = Interfaces.Count;
            DnsCacheEntries = DnsCache.Count;
            LastRefreshTime = _service.LastRefreshTime;
            RefreshStatus = $"Scan complete. {TotalConnections} connections, {SuspiciousConnections} suspicious.";
        }
        catch (Exception ex)
        {
            RefreshStatus = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
