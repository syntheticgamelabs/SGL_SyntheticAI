using System.Windows;
using System.Windows.Controls;
using SGL.JudgeDredd.App.ViewModels;

namespace SGL.JudgeDredd.App.Views;

public partial class AdminPanelView : UserControl
{
    public AdminPanelView()
    {
        InitializeComponent();
    }

    private void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminPanelViewModel vm)
        {
            vm.ChangePasswordNewPassword = NewPasswordBox.Password;
            vm.ChangePasswordConfirm = ConfirmPasswordBox.Password;
            vm.ChangePasswordCommand.Execute(null);
        }
    }
}
