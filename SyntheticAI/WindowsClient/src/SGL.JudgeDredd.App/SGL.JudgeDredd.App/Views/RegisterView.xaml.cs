using System.Windows;
using System.Windows.Controls;

namespace SGL.JudgeDredd.App.Views;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }

    private void BackToLogin_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.NavigateToLoginActionCommand.Execute(null);
        }
    }
}
