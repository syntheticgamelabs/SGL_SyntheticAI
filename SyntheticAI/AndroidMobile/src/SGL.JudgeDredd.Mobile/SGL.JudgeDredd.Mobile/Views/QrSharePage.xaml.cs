using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class QrSharePage : ContentPage
{
    public QrSharePage(QrShareViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
