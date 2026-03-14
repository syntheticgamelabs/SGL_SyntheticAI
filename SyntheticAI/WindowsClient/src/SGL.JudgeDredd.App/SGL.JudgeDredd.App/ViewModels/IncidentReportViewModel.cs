using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Security.Reports;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class IncidentReportViewModel : ViewModelBase
{
    private readonly IncidentReportService _service = new();

    [ObservableProperty] private int _totalReports;
    [ObservableProperty] private string _lastReportTime = "Never";
    [ObservableProperty] private string _selectedReportContent = string.Empty;
    [ObservableProperty] private string _reportStatus = "Ready";
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private IncidentReport? _selectedReport;

    public ObservableCollection<IncidentReport> Reports => _service.Reports;

    public IncidentReportViewModel()
    {
        Title = "Incident Reports";
        // Load any saved reports
        _service.LoadSavedReports();
        TotalReports = _service.TotalReports;
    }

    partial void OnSelectedReportChanged(IncidentReport? value)
    {
        SelectedReportContent = value?.Content ?? string.Empty;
    }

    [RelayCommand]
    private async Task GenerateFullReportAsync()
    {
        IsGenerating = true;
        ReportStatus = "Generating full system report...";
        try
        {
            var report = await _service.GenerateFullSystemReportAsync();
            TotalReports = _service.TotalReports;
            LastReportTime = _service.LastReportTime;
            SelectedReport = report;
            ReportStatus = $"Report generated: {report.Title}";
        }
        catch (Exception ex)
        {
            ReportStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void RefreshReports()
    {
        _service.LoadSavedReports();
        TotalReports = _service.TotalReports;
        ReportStatus = $"Loaded {TotalReports} saved reports.";
    }
}
