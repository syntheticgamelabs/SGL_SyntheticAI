using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ThreatReputationViewModel : ViewModelBase
{
    private readonly ThreatReputationService _service = new();

    [ObservableProperty] private int _totalLookups;
    [ObservableProperty] private int _threatsFound;
    [ObservableProperty] private int _cleanResults;
    [ObservableProperty] private string _lastLookupTime = "Never";
    [ObservableProperty] private string _lookupTarget = string.Empty;
    [ObservableProperty] private string _lookupStatus = "Ready";
    [ObservableProperty] private bool _isLooking;

    public ObservableCollection<ReputationResult> Results => _service.Results;

    public ThreatReputationViewModel()
    {
        Title = "Threat Reputation";
    }

    [RelayCommand]
    private async Task LookupFileAsync()
    {
        if (string.IsNullOrWhiteSpace(LookupTarget)) return;
        IsLooking = true;
        LookupStatus = $"Checking {LookupTarget}...";
        try
        {
            if (System.IO.File.Exists(LookupTarget))
            {
                await _service.CheckFileAsync(LookupTarget);
            }
            else if (System.Net.IPAddress.TryParse(LookupTarget, out _))
            {
                await _service.CheckIpAsync(LookupTarget);
            }
            else if (LookupTarget.Contains('.'))
            {
                await _service.CheckDomainAsync(LookupTarget);
            }
            else
            {
                await _service.CheckHashAsync(LookupTarget);
            }
            TotalLookups = _service.TotalLookupsPerformed;
            ThreatsFound = _service.ThreatsFound;
            CleanResults = _service.CleanResults;
            LastLookupTime = _service.LastLookupTime;
            LookupStatus = $"Done. {TotalLookups} lookups, {ThreatsFound} threats.";
        }
        catch (Exception ex)
        {
            LookupStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsLooking = false;
        }
    }

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(LookupTarget) || !System.IO.Directory.Exists(LookupTarget)) return;
        IsLooking = true;
        LookupStatus = $"Scanning directory {LookupTarget}...";
        try
        {
            await _service.ScanDirectoryAsync(LookupTarget);
            TotalLookups = _service.TotalLookupsPerformed;
            ThreatsFound = _service.ThreatsFound;
            CleanResults = _service.CleanResults;
            LastLookupTime = _service.LastLookupTime;
            LookupStatus = $"Done. {TotalLookups} files scanned, {ThreatsFound} threats.";
        }
        catch (Exception ex)
        {
            LookupStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsLooking = false;
        }
    }
}
