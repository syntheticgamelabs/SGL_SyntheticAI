using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class WifiScannerPage : ContentPage
{
    public WifiScannerPage(WifiScannerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
