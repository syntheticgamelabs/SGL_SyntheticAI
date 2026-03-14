using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security.Monitors;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class EndpointMonitorViewModel : ViewModelBase
{
    private readonly EndpointMonitorService _service = new();
    private DispatcherTimer? _refreshTimer;

    [ObservableProperty] private int _totalServices;
    [ObservableProperty] private int _runningServices;
    [ObservableProperty] private int _stoppedServices;
    [ObservableProperty] private int _openPorts;
    [ObservableProperty] private int _installedDrivers;
    [ObservableProperty] private int _startupPrograms;
    [ObservableProperty] private int _scheduledTasks;
    [ObservableProperty] private int _suspiciousCount;
    [ObservableProperty] private string _lastScanTime = "Never";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanStatus = "Ready";
    [ObservableProperty] private bool _autoRefreshEnabled;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _selectedTypeFilter = "All";
    [ObservableProperty] private bool _showSuspiciousOnly;

    [ObservableProperty] private EndpointInfo? _selectedEndpoint;

    public ObservableCollection<EndpointInfo> Endpoints => _service.Endpoints;

    public ObservableCollection<EndpointInfo> FilteredEndpoints { get; } = new();

    // Filtered sub-collections for tabbed views
    public ObservableCollection<EndpointInfo> Services { get; } = new();
    public ObservableCollection<EndpointInfo> Ports { get; } = new();
    public ObservableCollection<EndpointInfo> Drivers { get; } = new();
    public ObservableCollection<EndpointInfo> StartupItems { get; } = new();
    public ObservableCollection<EndpointInfo> ScheduledTaskItems { get; } = new();
    public ObservableCollection<EndpointInfo> SuspiciousItems { get; } = new();

    public string[] TypeFilters { get; } = { "All", "Service", "Port", "Driver", "Startup", "ScheduledTask", "Suspicious" };

    public EndpointMonitorViewModel()
    {
        Title = "Endpoint Monitor";
    }

    [RelayCommand]
    private async Task ScanEndpointsAsync()
    {
        IsScanning = true;
        ScanStatus = "Scanning endpoints...";
        try
        {
            await _service.ScanEndpointsAsync();
            TotalServices = _service.TotalServices;
            RunningServices = _service.RunningServices;
            StoppedServices = _service.StoppedServices;
            OpenPorts = _service.OpenPorts;
            InstalledDrivers = _service.InstalledDrivers;
            StartupPrograms = _service.StartupPrograms;
            ScheduledTasks = _service.ScheduledTasks;
            SuspiciousCount = _service.GetSuspiciousEndpoints().Count;
            LastScanTime = _service.LastScanTime;

            RebuildFilteredCollections();

            ScanStatus = $"Scan complete. {Endpoints.Count} endpoints found, {SuspiciousCount} suspicious.";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void RebuildFilteredCollections()
    {
        Services.Clear();
        Ports.Clear();
        Drivers.Clear();
        StartupItems.Clear();
        ScheduledTaskItems.Clear();
        SuspiciousItems.Clear();
        FilteredEndpoints.Clear();

        foreach (var ep in Endpoints)
        {
            switch (ep.Type)
            {
                case "Service": Services.Add(ep); break;
                case "Port": Ports.Add(ep); break;
                case "Driver": Drivers.Add(ep); break;
                case "Startup": StartupItems.Add(ep); break;
                case "ScheduledTask": ScheduledTaskItems.Add(ep); break;
            }

            if (ep.IsSuspicious)
                SuspiciousItems.Add(ep);

            if (MatchesFilter(ep))
                FilteredEndpoints.Add(ep);
        }
    }

    private bool MatchesFilter(EndpointInfo ep)
    {
        if (ShowSuspiciousOnly && !ep.IsSuspicious)
            return false;

        if (SelectedTypeFilter != "All")
        {
            if (SelectedTypeFilter == "Suspicious")
            {
                if (!ep.IsSuspicious) return false;
            }
            else if (ep.Type != SelectedTypeFilter)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var search = FilterText.Trim();
            if (!ep.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !ep.Details.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !ep.Status.Contains(search, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilter();
    partial void OnShowSuspiciousOnlyChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEndpoints.Clear();
        foreach (var ep in Endpoints)
        {
            if (MatchesFilter(ep))
                FilteredEndpoints.Add(ep);
        }
    }

    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        AutoRefreshEnabled = !AutoRefreshEnabled;

        if (AutoRefreshEnabled)
        {
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (_, _) =>
            {
                if (!IsScanning)
                    await ScanEndpointsAsync();
            };
            _refreshTimer.Start();
            ScanStatus = "Auto-refresh enabled (30s interval)";
        }
        else
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
            ScanStatus = "Auto-refresh disabled";
        }
    }

    [RelayCommand]
    private void StopService()
    {
        if (SelectedEndpoint == null || SelectedEndpoint.Type != "Service") return;

        try
        {
            using var sc = new ServiceController(SelectedEndpoint.Name);
            if (sc.Status == ServiceControllerStatus.Running)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                SelectedEndpoint.Status = "Stopped";
                ScanStatus = $"Service '{SelectedEndpoint.Name}' stopped.";
            }
        }
        catch (Exception ex)
        {
            ScanStatus = $"Failed to stop service: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartService()
    {
        if (SelectedEndpoint == null || SelectedEndpoint.Type != "Service") return;

        try
        {
            using var sc = new ServiceController(SelectedEndpoint.Name);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                SelectedEndpoint.Status = "Running";
                ScanStatus = $"Service '{SelectedEndpoint.Name}' started.";
            }
        }
        catch (Exception ex)
        {
            ScanStatus = $"Failed to start service: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BlockPort()
    {
        if (SelectedEndpoint == null || SelectedEndpoint.Type != "Port") return;

        try
        {
            var portStr = SelectedEndpoint.Name.Split(':').LastOrDefault();
            if (string.IsNullOrEmpty(portStr)) return;

            var ruleName = $"SyntheticAI_Block_{SelectedEndpoint.Name.Replace(':', '_')}";
            var protocol = SelectedEndpoint.Name.StartsWith("TCP") ? "TCP" : "UDP";

            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=block protocol={protocol} localport={portStr}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);

            ScanStatus = $"Firewall rule created to block {SelectedEndpoint.Name}.";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Failed to block port: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportReport()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SGL SyntheticAI - Endpoint Monitor Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Last Scan: {LastScanTime}");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"Services: {TotalServices} (Running: {RunningServices}, Stopped: {StoppedServices})");
            sb.AppendLine($"Open Ports: {OpenPorts}");
            sb.AppendLine($"Drivers: {InstalledDrivers}");
            sb.AppendLine($"Startup Programs: {StartupPrograms}");
            sb.AppendLine($"Scheduled Tasks: {ScheduledTasks}");
            sb.AppendLine($"Suspicious Items: {SuspiciousCount}");
            sb.AppendLine(new string('=', 60));

            if (SuspiciousItems.Count > 0)
            {
                sb.AppendLine("\nSUSPICIOUS ITEMS:");
                foreach (var item in SuspiciousItems)
                {
                    sb.AppendLine($"  [{item.Type}] {item.Name} - {item.Status}");
                    sb.AppendLine($"    {item.Details}");
                }
            }

            var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "reports");
            System.IO.Directory.CreateDirectory(logDir);
            var filePath = System.IO.Path.Combine(logDir, $"endpoint_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            System.IO.File.WriteAllText(filePath, sb.ToString());

            ScanStatus = $"Report exported to {filePath}";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Export failed: {ex.Message}";
        }
    }
}
