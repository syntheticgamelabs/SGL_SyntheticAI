using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class DeviceProtectionViewModel : ObservableObject
{
    private readonly DeviceProtectionService _protectionService;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready to scan";
    [ObservableProperty] private int _micAccessCount;
    [ObservableProperty] private int _camAccessCount;
    [ObservableProperty] private string _selectedTab = "Microphone";

    public ObservableCollection<DeviceProtectionService.AppAccessInfo> MicrophoneApps { get; } = new();
    public ObservableCollection<DeviceProtectionService.AppAccessInfo> CameraApps { get; } = new();

    public DeviceProtectionViewModel(DeviceProtectionService protectionService)
    {
        _protectionService = protectionService;
        _protectionService.StatusChanged += (_, s) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = s);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for microphone and camera access...";
        MicrophoneApps.Clear();
        CameraApps.Clear();

        var status = await _protectionService.ScanDeviceAccessAsync();

        foreach (var app in status.MicrophoneAccessApps)
            MicrophoneApps.Add(app);
        foreach (var app in status.CameraAccessApps)
            CameraApps.Add(app);

        MicAccessCount = status.MicrophoneAccessApps.Count;
        CamAccessCount = status.CameraAccessApps.Count;

        StatusText = $"Found {MicAccessCount} mic access apps, {CamAccessCount} camera access apps";
        IsScanning = false;
    }

    [RelayCommand]
    private async Task KillAppAsync(DeviceProtectionService.AppAccessInfo? app)
    {
        if (app == null) return;
        await _protectionService.KillAppAsync(app.PackageName);
        StatusText = $"Killed {app.AppName}";
    }

    [RelayCommand]
    private async Task ManagePermissionsAsync(DeviceProtectionService.AppAccessInfo? app)
    {
        if (app == null) return;
        await _protectionService.OpenPermissionSettingsAsync(app.PackageName);
    }

    [RelayCommand]
    private async Task OpenMicSettingsAsync()
    {
        await _protectionService.OpenMicrophoneSettingsAsync();
    }

    [RelayCommand]
    private async Task OpenCamSettingsAsync()
    {
        await _protectionService.OpenCameraSettingsAsync();
    }

    [RelayCommand]
    private void SwitchTab(string tab)
    {
        SelectedTab = tab;
    }
}
