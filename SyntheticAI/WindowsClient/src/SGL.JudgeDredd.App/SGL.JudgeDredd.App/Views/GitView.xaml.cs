using System.Windows;
using System.Windows.Controls;
using SGL.JudgeDredd.App.ViewModels;

namespace SGL.JudgeDredd.App.Views;

public partial class GitView : UserControl
{
    public GitView()
    {
        InitializeComponent();
    }

    private void ClonePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is GitViewModel vm && sender is PasswordBox pb)
        {
            vm.ClonePassword = pb.Password;
        }
    }

    private void CancelCloneDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is GitViewModel vm)
        {
            vm.IsCloneDialogOpen = false;
        }
    }

    private void CancelNewRepoDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is GitViewModel vm)
        {
            vm.IsNewRepoDialogOpen = false;
        }
    }

    private void RefreshChanges_Click(object sender, RoutedEventArgs e)
    {
        // Force refresh by re-selecting current repo
        if (DataContext is GitViewModel vm && vm.IsRepositoryOpen)
        {
            var current = vm.SelectedRepository;
            if (current != null)
            {
                vm.SelectedRepository = null;
                vm.SelectedRepository = current;
            }
        }
    }
}
