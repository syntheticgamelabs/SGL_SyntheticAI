using System.Windows.Controls;

namespace SGL.JudgeDredd.App.Views;

public partial class EulaView : UserControl
{
    public EulaView()
    {
        InitializeComponent();
    }

    private void EulaScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            var atBottom = sv.VerticalOffset >= sv.ScrollableHeight - 20;
            if (atBottom && DataContext is ViewModels.EulaViewModel vm)
            {
                vm.HasScrolledToBottom = true;
                ScrollHintText.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}
