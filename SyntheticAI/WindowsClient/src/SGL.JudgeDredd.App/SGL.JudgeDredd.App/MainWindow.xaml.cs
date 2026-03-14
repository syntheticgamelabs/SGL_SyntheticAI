using System.Windows;
using System.Windows.Input;
using SGL.JudgeDredd.App.ViewModels;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App;

/// <summary>
/// Main shell window with custom chrome, sidebar navigation, and content area.
/// Minimizes to system tray on close instead of shutting down (when enabled in settings).
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettings _appSettings;

    public MainWindow(MainViewModel viewModel, AppSettings appSettings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _appSettings = appSettings;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If navigating (logout), allow the close
        if (Application.Current is App app && app.IsNavigating)
            return;

        // Only minimize to tray if the setting is enabled; otherwise close normally
        if (_appSettings.General.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>
    /// Enables dragging the window by clicking on the custom title bar.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                // Restore before dragging from maximized state
                var point = PointToScreen(e.GetPosition(this));
                WindowState = WindowState.Normal;
                Left = point.X - (ActualWidth / 2);
                Top = point.Y - 20;
            }
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// Update the maximize/restore icon when window state changes.
    /// Also constrain maximized bounds to the work area to avoid covering the taskbar.
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            // Constrain to work area so taskbar remains visible
            var workArea = SystemParameters.WorkArea;
            MaxHeight = workArea.Height + 8;
            MaxWidth = workArea.Width + 8;
            MaximizeIcon.Text = "\u29C9"; // Two overlapping squares (restore icon)
        }
        else
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;
            MaximizeIcon.Text = "\u25A1"; // Square (maximize icon)
        }
    }
}
