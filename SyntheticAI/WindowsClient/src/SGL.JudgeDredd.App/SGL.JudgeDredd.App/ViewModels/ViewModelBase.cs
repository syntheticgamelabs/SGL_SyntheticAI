using CommunityToolkit.Mvvm.ComponentModel;

namespace SGL.JudgeDredd.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;
}
