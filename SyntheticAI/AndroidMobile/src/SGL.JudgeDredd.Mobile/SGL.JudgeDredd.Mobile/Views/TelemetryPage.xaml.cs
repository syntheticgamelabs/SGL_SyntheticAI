using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class TelemetryPage : ContentPage
{
    public TelemetryPage(TelemetryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
