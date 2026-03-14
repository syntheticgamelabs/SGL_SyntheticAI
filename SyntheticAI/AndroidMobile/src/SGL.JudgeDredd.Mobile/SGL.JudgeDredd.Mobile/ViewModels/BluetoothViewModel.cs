using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class BluetoothViewModel : ObservableObject
{
    private readonly BluetoothService _bluetoothService;
    private readonly ThreatKnowledgeService _knowledgeService;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready to scan";
    [ObservableProperty] private string _emergencyMessage = string.Empty;
    [ObservableProperty] private BluetoothDeviceInfo? _selectedDevice;
    [ObservableProperty] private string _analysisResult = string.Empty;
    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private bool _isBroadcasting;
    [ObservableProperty] private int _deviceCount;

    public ObservableCollection<BluetoothDeviceInfo> Devices { get; } = new();

    public BluetoothViewModel(BluetoothService bluetoothService, ThreatKnowledgeService knowledgeService)
    {
        _bluetoothService = bluetoothService;
        _knowledgeService = knowledgeService;

        _bluetoothService.DeviceDiscovered += (_, device) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var existing = Devices.FirstOrDefault(d => d.Id == device.Id);
                if (existing != null)
                    Devices.Remove(existing);
                Devices.Add(device);
                DeviceCount = Devices.Count;
                StatusText = $"Found {Devices.Count} devices...";
            });
        };

        _bluetoothService.StatusChanged += (_, status) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = status);

        _bluetoothService.ErrorOccurred += (_, error) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"Error: {error}");
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        Devices.Clear();
        DeviceCount = 0;
        IsScanning = true;
        StatusText = "Requesting permissions...";

        // Request Bluetooth permissions (handles BLUETOOTH_SCAN/BLUETOOTH_CONNECT on Android 12+)
        var btStatus = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (btStatus != PermissionStatus.Granted)
        {
            btStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
            if (btStatus != PermissionStatus.Granted)
            {
                StatusText = "Bluetooth scan requires Bluetooth permission";
                IsScanning = false;
                return;
            }
        }

        // Location permission required for BLE scanning on Android 11 and below
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locStatus != PermissionStatus.Granted)
            {
                locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locStatus != PermissionStatus.Granted)
                {
                    StatusText = "Bluetooth scan requires Location permission on this Android version";
                    IsScanning = false;
                    return;
                }
            }
        }

        if (!_bluetoothService.IsAvailable)
        {
            StatusText = "Bluetooth is not available on this device";
            IsScanning = false;
            return;
        }

        if (!_bluetoothService.IsOn)
        {
            StatusText = "Bluetooth is turned off. Please enable it.";
            IsScanning = false;
            return;
        }

        StatusText = "Scanning for Bluetooth devices...";
        await _bluetoothService.StartScanAsync();
        DeviceCount = Devices.Count;
        StatusText = $"Scan complete. Found {DeviceCount} devices.";
        IsScanning = false;
    }

    [RelayCommand]
    private async Task StopScanAsync()
    {
        await _bluetoothService.StopScanAsync();
        DeviceCount = Devices.Count;
        IsScanning = false;
    }

    [RelayCommand]
    private async Task AnalyzeDeviceAsync(BluetoothDeviceInfo? device)
    {
        if (device == null) return;
        IsAnalyzing = true;
        SelectedDevice = device;

        var result = await _bluetoothService.AnalyzeDeviceAsync(device);
        AnalysisResult = result.AnalysisResult;
        SelectedDevice = result;

        if (result.ThreatLevel != "Safe")
        {
            await _knowledgeService.AddThreatEntryAsync(new ThreatKnowledgeEntry
            {
                ThreatType = "bluetooth_threat",
                ThreatName = device.Name,
                Description = $"Suspicious BLE device: {result.ThreatLevel}",
                Severity = result.ThreatLevel == "Suspicious" ? "Medium" : "Low",
                Source = "bluetooth_scan"
            });
        }

        IsAnalyzing = false;

        // Show analysis result in a popup
        var details = $"Name: {result.Name}\n" +
                      $"Type: {result.DeviceType}\n" +
                      $"Signal: {result.Rssi} dBm ({result.SignalStrength})\n" +
                      $"Threat Level: {result.ThreatLevel}\n" +
                      $"Connected: {(result.IsConnected ? "Yes" : "No")}\n\n" +
                      $"Analysis:\n{result.AnalysisResult}";

        await Shell.Current.DisplayAlert($"Device Analysis - {result.Name}", details, "OK");
    }

    [RelayCommand]
    private async Task ConnectToDeviceAsync(BluetoothDeviceInfo? device)
    {
        if (device == null) return;
        StatusText = $"Connecting to {device.Name}...";
        var success = await _bluetoothService.ConnectToDeviceAsync(device.Id);
        if (success)
        {
            device.IsConnected = true;
            StatusText = $"Connected to {device.Name}";
        }
        else
        {
            StatusText = $"Failed to connect to {device.Name}";
        }
    }

    [RelayCommand]
    private async Task ShowDeviceDetailsAsync(BluetoothDeviceInfo? device)
    {
        if (device == null) return;

        var details = $"Name: {device.Name}\n" +
                      $"ID: {device.Id}\n" +
                      $"Type: {device.DeviceType}\n" +
                      $"RSSI: {device.Rssi} dBm\n" +
                      $"Signal: {device.SignalStrength}\n" +
                      $"Threat Level: {device.ThreatLevel}\n" +
                      $"Connected: {(device.IsConnected ? "Yes" : "No")}";

        await Shell.Current.DisplayAlert($"Device Details - {device.Name}", details, "OK");
    }

    [RelayCommand]
    private async Task ToggleBroadcastAsync()
    {
        if (IsBroadcasting)
        {
            _bluetoothService.StopBroadcast();
            IsBroadcasting = false;
            StatusText = "BLE broadcast stopped";
        }
        else
        {
            StatusText = "Starting BLE broadcast...";
            await _bluetoothService.StartBroadcastAsync("SyntheticAI");
            IsBroadcasting = _bluetoothService.IsBroadcasting;
            StatusText = IsBroadcasting
                ? "BLE broadcasting - SyntheticAI beacon active"
                : "Failed to start BLE broadcast. Check Bluetooth permissions.";
        }
    }

    [RelayCommand]
    private async Task BroadcastEmergencyAsync()
    {
        if (string.IsNullOrWhiteSpace(EmergencyMessage))
        {
            StatusText = "Please enter a broadcast message.";
            return;
        }

        StatusText = "Broadcasting message...";
        await _bluetoothService.BroadcastEmergencyAsync(EmergencyMessage);
        StatusText = "Message broadcast sent";
    }
}
