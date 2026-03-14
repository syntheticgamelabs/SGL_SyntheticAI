using CommunityToolkit.Mvvm.ComponentModel;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class SystemMonitorViewModel : ViewModelBase
{
    public EndpointMonitorViewModel EndpointVm { get; }
    public PersistenceScannerViewModel PersistenceVm { get; }
    public BehavioralAnomalyViewModel AnomalyVm { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public SystemMonitorViewModel(
        EndpointMonitorViewModel endpointVm,
        PersistenceScannerViewModel persistenceVm,
        BehavioralAnomalyViewModel anomalyVm)
    {
        Title = "System Monitor";
        EndpointVm = endpointVm;
        PersistenceVm = persistenceVm;
        AnomalyVm = anomalyVm;
    }
}
