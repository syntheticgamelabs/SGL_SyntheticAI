using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security.Scanners;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class PersistenceScannerViewModel : ViewModelBase
{
    private readonly PersistenceScannerService _service = new();

    [ObservableProperty] private int _totalEntries;
    [ObservableProperty] private int _suspiciousEntries;
    [ObservableProperty] private string _lastScanTime = "Never";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanStatus = "Ready";

    public ObservableCollection<PersistenceEntry> Entries => _service.Entries;

    public PersistenceScannerViewModel()
    {
        Title = "Persistence Scanner";
    }

    [RelayCommand]
    private async Task ScanPersistenceAsync()
    {
        IsScanning = true;
        ScanStatus = "Scanning persistence mechanisms...";
        try
        {
            await _service.ScanAsync();
            TotalEntries = _service.TotalEntries;
            SuspiciousEntries = _service.SuspiciousEntries;
            LastScanTime = _service.LastScanTime;
            ScanStatus = $"Scan complete. {TotalEntries} entries found, {SuspiciousEntries} suspicious.";
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
}
