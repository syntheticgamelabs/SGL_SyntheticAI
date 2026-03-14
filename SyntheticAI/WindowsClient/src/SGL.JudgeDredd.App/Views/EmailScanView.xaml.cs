using System.Windows;
using System.Windows.Controls;
using SGL.JudgeDredd.App.ViewModels;

namespace SGL.JudgeDredd.App.Views;

public partial class EmailScanView : UserControl
{
    public EmailScanView()
    {
        InitializeComponent();
        Loaded += EmailScanView_Loaded;
    }

    private void EmailScanView_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate the PasswordBox with the saved password if available
        if (DataContext is EmailScanViewModel vm && !string.IsNullOrEmpty(vm.ImapPassword))
        {
            ImapPasswordBox.Password = vm.ImapPassword;
        }
    }

    private void ImapPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is EmailScanViewModel vm)
        {
            vm.ImapPassword = ImapPasswordBox.Password;
        }
    }
}
