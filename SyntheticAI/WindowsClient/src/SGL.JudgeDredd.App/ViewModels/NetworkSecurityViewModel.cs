using CommunityToolkit.Mvvm.ComponentModel;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class NetworkSecurityViewModel : ViewModelBase
{
    public NetworkIntrusionViewModel NidsVm { get; }
    public NetworkMonitorViewModel NetMonVm { get; }
    public DataLeakViewModel DataLeakVm { get; }
    public BrowserProtectionViewModel BrowserVm { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public NetworkSecurityViewModel(
        NetworkIntrusionViewModel nidsVm,
        NetworkMonitorViewModel netMonVm,
        DataLeakViewModel dataLeakVm,
        BrowserProtectionViewModel browserVm)
    {
        Title = "Network Security";
        NidsVm = nidsVm;
        NetMonVm = netMonVm;
        DataLeakVm = dataLeakVm;
        BrowserVm = browserVm;
    }
}
