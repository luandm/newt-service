using Avalonia.Controls;
using NewtService.Tray.ViewModels;

namespace NewtService.Tray.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosed(e);
    }
}
