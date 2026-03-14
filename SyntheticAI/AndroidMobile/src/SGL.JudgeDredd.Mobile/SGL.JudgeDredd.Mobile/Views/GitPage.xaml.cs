using SGL.JudgeDredd.Mobile.ViewModels;

namespace SGL.JudgeDredd.Mobile.Views;

public partial class GitPage : ContentPage
{
    public GitPage(GitViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
