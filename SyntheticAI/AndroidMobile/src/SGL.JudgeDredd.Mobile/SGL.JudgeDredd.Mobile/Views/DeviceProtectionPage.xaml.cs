using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class DeviceProtectionPage : ContentPage
{
    public DeviceProtectionPage(DeviceProtectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
