using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NewtService.Tray.ViewModels;
using NewtService.Tray.Views;
using System.Diagnostics;
using NewtService.Core;

namespace NewtService.Tray;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _installItem;
    private NativeMenuItem? _uninstallItem;
    private NativeMenuItem? _updateNowItem;
    private GitHubRelease? _availableUpdate;
    private DispatcherTimer? _statusTimer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SetupTrayIcon();
            
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (_, _) => UpdateMenuState();
            _statusTimer.Start();
            
            UpdateMenuState();

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) =>
            {
                _statusTimer?.Stop();
                _trayIcon?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        var menu = new NativeMenu();
        
        _installItem = new NativeMenuItem("Install Service");
        _installItem.Click += (_, _) => InstallService();
        menu.Items.Add(_installItem);
        
        _uninstallItem = new NativeMenuItem("Uninstall Service");
        _uninstallItem.Click += (_, _) => UninstallService();
        menu.Items.Add(_uninstallItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        var checkUpdateItem = new NativeMenuItem("Check for Updates");
        checkUpdateItem.Click += async (_, _) => await CheckForUpdatesAsync();
        menu.Items.Add(checkUpdateItem);
        
        _updateNowItem = new NativeMenuItem("Update Now");
        _updateNowItem.Click += async (_, _) => await PerformUpdateAsync();
        _updateNowItem.IsVisible = false;
        menu.Items.Add(_updateNowItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        var configItem = new NativeMenuItem("Config");
        configItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(configItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Newt VPN Service",
            Menu = menu,
            Icon = CreateTrayIcon(false)
        };
        
        _trayIcon.Clicked += (_, _) => ShowMainWindow();
        _trayIcon.IsVisible = true;
    }

    private void UpdateMenuState()
    {
        var isInstalled = ServiceControlHelper.IsServiceInstalled();
        var isRunning = ServiceControlHelper.IsServiceRunning();
        
        if (_installItem != null)
            _installItem.IsVisible = !isInstalled;
        
        if (_uninstallItem != null)
            _uninstallItem.IsVisible = isInstalled;
        
        if (_trayIcon != null)
        {
            _trayIcon.Icon = CreateTrayIcon(isRunning);
            _trayIcon.ToolTipText = isRunning ? "Newt VPN - Running" : "Newt VPN - Stopped";
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        using var updater = new NewtUpdater();
        var current = GetInstalledVersion();
        var latest = await updater.GetLatestReleaseAsync();
        
        if (latest == null)
        {
            ShowMainWindow();
            return;
        }
        
        if (current != latest.TagName)
        {
            _availableUpdate = latest;
            if (_updateNowItem != null)
            {
                _updateNowItem.Header = $"Update Now ({latest.TagName})";
                _updateNowItem.IsVisible = true;
            }
        }
        else
        {
            _availableUpdate = null;
            if (_updateNowItem != null)
                _updateNowItem.IsVisible = false;
        }
    }

    private async Task PerformUpdateAsync()
    {
        if (_availableUpdate == null) return;
        
        var wasRunning = ServiceControlHelper.IsServiceRunning();
        
        if (wasRunning)
            await ServiceControlHelper.StopServiceAsync();
        
        using var updater = new NewtUpdater();
        await updater.DownloadAndInstallAsync(_availableUpdate);
        
        _availableUpdate = null;
        if (_updateNowItem != null)
            _updateNowItem.IsVisible = false;
        
        if (wasRunning)
            await ServiceControlHelper.StartServiceAsync();
        
        UpdateMenuState();
    }

    private void InstallService()
    {
        var exePath = GetWorkerExePath();
        if (exePath == null) return;

        RunScCommand($"create {ServiceConstants.ServiceName} binPath= \"{exePath}\" start= auto DisplayName= \"{ServiceConstants.ServiceDisplayName}\"");
        RunScCommand($"description {ServiceConstants.ServiceName} \"{ServiceConstants.ServiceDescription}\"");
        UpdateMenuState();
    }

    private void UninstallService()
    {
        _ = ServiceControlHelper.StopServiceAsync().Result;
        RunScCommand($"delete {ServiceConstants.ServiceName}");
        UpdateMenuState();
    }

    private bool RunScCommand(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            process?.WaitForExit(10000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string? GetWorkerExePath()
    {
        var currentDir = AppContext.BaseDirectory;
        var workerPath = Path.Combine(currentDir, "NewtService.Worker.exe");
        if (File.Exists(workerPath)) return workerPath;
        
        var parentDir = Path.GetDirectoryName(currentDir);
        if (parentDir != null)
        {
            workerPath = Path.Combine(parentDir, "NewtService.Worker", "bin", "Debug", "net8.0-windows", "NewtService.Worker.exe");
            if (File.Exists(workerPath)) return workerPath;
            
            workerPath = Path.Combine(parentDir, "NewtService.Worker", "bin", "Release", "net8.0-windows", "NewtService.Worker.exe");
            if (File.Exists(workerPath)) return workerPath;
        }
        
        return null;
    }

    private string? GetInstalledVersion()
    {
        if (!File.Exists(ServiceConstants.VersionFilePath))
            return null;
        return File.ReadAllText(ServiceConstants.VersionFilePath).Trim();
    }

    private WindowIcon CreateTrayIcon(bool running)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        
        var color = running 
            ? System.Drawing.Color.FromArgb(76, 175, 80)
            : System.Drawing.Color.FromArgb(158, 158, 158);
        
        using var brush = new System.Drawing.SolidBrush(color);
        g.FillEllipse(brush, 2, 2, 28, 28);
        
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2.5f);
        if (running)
        {
            g.DrawLine(pen, 10, 16, 14, 20);
            g.DrawLine(pen, 14, 20, 22, 12);
        }
        else
        {
            g.DrawLine(pen, 11, 11, 21, 21);
            g.DrawLine(pen, 21, 11, 11, 21);
        }
        
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        return new WindowIcon(ms);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsVisible)
        {
            _mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Activate();
        }
    }
}
