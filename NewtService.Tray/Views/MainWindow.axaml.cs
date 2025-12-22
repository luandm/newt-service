using Avalonia.Controls;
using NewtService.Tray.ViewModels;

namespace NewtService.Tray.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnOpenSettings += OpenSettingsWindow;
        }
    }

    private void OpenSettingsWindow()
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel()
        };
        settingsWindow.ShowDialog(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnOpenSettings -= OpenSettingsWindow;
            vm.Dispose();
        }
        base.OnClosed(e);
    }
}
