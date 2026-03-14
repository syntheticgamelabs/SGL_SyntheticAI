using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class BluetoothPage : ContentPage
{
    public BluetoothPage(BluetoothViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
