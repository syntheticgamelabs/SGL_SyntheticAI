using CommunityToolkit.Mvvm.ComponentModel;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ToolsViewModel : ViewModelBase
{
    public GitViewModel GitVm { get; }
    public VpnViewModel VpnVm { get; }
    public TestSpeedViewModel TestSpeedVm { get; }
    public SystemCleanerViewModel CleanerVm { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public ToolsViewModel(
        GitViewModel gitVm,
        VpnViewModel vpnVm,
        TestSpeedViewModel testSpeedVm,
        SystemCleanerViewModel cleanerVm)
    {
        Title = "Tools";
        GitVm = gitVm;
        VpnVm = vpnVm;
        TestSpeedVm = testSpeedVm;
        CleanerVm = cleanerVm;
    }
}
