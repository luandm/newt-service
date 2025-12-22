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
    private NativeMenuItem? _newtUpdateItem;
    private NativeMenuItem? _appUpdateItem;
    private GitHubRelease? _availableNewtUpdate;
    private AppRelease? _availableAppUpdate;
    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _midnightCheckTimer;
    private bool _isDownloading;

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
            
            SetupMidnightCheck();
            
            EnsureNewtInstalledAsync();
            
            UpdateMenuState();

            if (Program.ShowConfigOnStartup)
            {
                Dispatcher.UIThread.Post(ShowMainWindow, DispatcherPriority.Background);
            }

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) =>
            {
                _statusTimer?.Stop();
                _midnightCheckTimer?.Stop();
                _trayIcon?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void EnsureNewtInstalledAsync()
    {
        if (File.Exists(ServiceConstants.NewtExecutablePath))
            return;

        await DownloadNewtAsync();
    }

    private async Task DownloadNewtAsync()
    {
        if (_isDownloading) return;
        _isDownloading = true;

        try
        {
            Directory.CreateDirectory(ServiceConstants.AppDataPath);
            
            ShowWindowsNotification("Newt VPN", "Downloading Newt client...");
            
            using var updater = new NewtUpdater();
            var release = await updater.GetLatestReleaseAsync();
            
            if (release == null)
            {
                ShowWindowsNotification("Newt VPN", "Failed to download Newt client. Check your internet connection.");
                return;
            }

            var success = await updater.DownloadAndInstallAsync(release);
            
            if (success)
            {
                ShowWindowsNotification("Newt VPN", $"Newt {release.TagName} installed successfully.");
            }
            else
            {
                ShowWindowsNotification("Newt VPN", "Failed to install Newt client.");
            }
        }
        catch (Exception ex)
        {
            ShowWindowsNotification("Newt VPN", $"Error: {ex.Message}");
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private void SetupMidnightCheck()
    {
        _midnightCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _midnightCheckTimer.Tick += async (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Hour == 0 && now.Minute == 0)
            {
                await CheckForNewtUpdateAsync(showNotification: true);
                await CheckForAppUpdateAsync(showNotification: true);
            }
        };
        _midnightCheckTimer.Start();
        
        // Check on startup after delay
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await CheckForNewtUpdateAsync(showNotification: true);
                await CheckForAppUpdateAsync(showNotification: true);
            });
        });
    }

    private void SetupTrayIcon()
    {
        var menu = new NativeMenu();
        
        _installItem = new NativeMenuItem("Install Service");
        _installItem.Click += async (_, _) => await InstallServiceAsync();
        menu.Items.Add(_installItem);
        
        _uninstallItem = new NativeMenuItem("Uninstall Service");
        _uninstallItem.Click += (_, _) => UninstallService();
        menu.Items.Add(_uninstallItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        var checkNewtUpdateItem = new NativeMenuItem("Check for Newt Update");
        checkNewtUpdateItem.Click += async (_, _) => await CheckForNewtUpdateAsync(showNotification: false);
        menu.Items.Add(checkNewtUpdateItem);
        
        _newtUpdateItem = new NativeMenuItem("Update Newt");
        _newtUpdateItem.Click += async (_, _) => await PerformNewtUpdateAsync();
        _newtUpdateItem.IsVisible = false;
        menu.Items.Add(_newtUpdateItem);
        
        menu.Items.Add(new NativeMenuItemSeparator());
        
        var checkAppUpdateItem = new NativeMenuItem("Check for App Update");
        checkAppUpdateItem.Click += async (_, _) => await CheckForAppUpdateAsync(showNotification: false);
        menu.Items.Add(checkAppUpdateItem);
        
        _appUpdateItem = new NativeMenuItem("Update NewtService");
        _appUpdateItem.Click += async (_, _) => await PerformAppUpdateAsync();
        _appUpdateItem.IsVisible = false;
        menu.Items.Add(_appUpdateItem);
        
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

    private async Task CheckForNewtUpdateAsync(bool showNotification)
    {
        try
        {
            using var updater = new NewtUpdater();
            var current = GetInstalledNewtVersion();
            var latest = await updater.GetLatestReleaseAsync();
            
            if (latest == null) return;
            
            if (current != latest.TagName)
            {
                _availableNewtUpdate = latest;
                if (_newtUpdateItem != null)
                {
                    _newtUpdateItem.Header = $"Update Newt ({latest.TagName})";
                    _newtUpdateItem.IsVisible = true;
                }
                
                if (showNotification)
                {
                    ShowWindowsNotification(
                        "Newt Update Available",
                        $"Newt {latest.TagName} is available. Right-click tray icon to update.");
                }
            }
            else
            {
                _availableNewtUpdate = null;
                if (_newtUpdateItem != null)
                    _newtUpdateItem.IsVisible = false;
            }
        }
        catch { }
    }

    private async Task CheckForAppUpdateAsync(bool showNotification)
    {
        try
        {
            using var updater = new AppUpdater();
            var current = AppUpdater.GetCurrentVersion();
            var latest = await updater.GetLatestReleaseAsync();
            
            if (latest == null || current == null) return;
            
            if (latest.Version != current && CompareVersions(latest.Version, current) > 0)
            {
                _availableAppUpdate = latest;
                if (_appUpdateItem != null)
                {
                    _appUpdateItem.Header = $"Update NewtService ({latest.TagName})";
                    _appUpdateItem.IsVisible = true;
                }
                
                if (showNotification)
                {
                    ShowWindowsNotification(
                        "NewtService Update Available",
                        $"NewtService {latest.TagName} is available. Right-click tray icon to update.");
                }
            }
            else
            {
                _availableAppUpdate = null;
                if (_appUpdateItem != null)
                    _appUpdateItem.IsVisible = false;
            }
        }
        catch { }
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        
        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 != p2) return p1.CompareTo(p2);
        }
        return 0;
    }

    private void ShowWindowsNotification(string title, string message)
    {
        if (_trayIcon != null)
        {
            Task.Run(() =>
            {
                try
                {
                    var ps = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; " +
                                    $"$template = [Windows.UI.Notifications.ToastTemplateType]::ToastText02; " +
                                    $"$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template); " +
                                    $"$text = $xml.GetElementsByTagName('text'); " +
                                    $"$text[0].AppendChild($xml.CreateTextNode('{EscapeForPowerShell(title)}')) | Out-Null; " +
                                    $"$text[1].AppendChild($xml.CreateTextNode('{EscapeForPowerShell(message)}')) | Out-Null; " +
                                    $"$toast = [Windows.UI.Notifications.ToastNotification]::new($xml); " +
                                    $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Newt VPN').Show($toast)\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(ps)?.WaitForExit(5000);
                }
                catch
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_trayIcon != null)
                            _trayIcon.ToolTipText = message;
                    });
                }
            });
        }
    }

    private static string EscapeForPowerShell(string text)
    {
        return text.Replace("'", "''").Replace("\"", "`\"");
    }

    private async Task PerformNewtUpdateAsync()
    {
        if (_availableNewtUpdate == null) return;
        
        var wasRunning = ServiceControlHelper.IsServiceRunning();
        
        if (wasRunning)
            await ServiceControlHelper.StopServiceAsync();
        
        using var updater = new NewtUpdater();
        await updater.DownloadAndInstallAsync(_availableNewtUpdate);
        
        var version = _availableNewtUpdate.TagName;
        _availableNewtUpdate = null;
        if (_newtUpdateItem != null)
            _newtUpdateItem.IsVisible = false;
        
        if (wasRunning)
            await ServiceControlHelper.StartServiceAsync();
        
        ShowWindowsNotification("Newt Updated", $"Successfully updated to {version}");
        
        UpdateMenuState();
    }

    private async Task PerformAppUpdateAsync()
    {
        if (_availableAppUpdate == null) return;
        
        ShowWindowsNotification("NewtService", "Downloading update...");
        
        using var updater = new AppUpdater();
        var success = await updater.DownloadAndInstallAsync(_availableAppUpdate);
        
        if (success)
        {
            // The MSI installer will handle stopping/restarting
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
        else
        {
            ShowWindowsNotification("NewtService", "Update failed. Opening releases page...");
            updater.OpenReleasesPage();
        }
    }

    private async Task InstallServiceAsync()
    {
        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            await DownloadNewtAsync();
            
            if (!File.Exists(ServiceConstants.NewtExecutablePath))
            {
                ShowWindowsNotification("Newt VPN", "Cannot install service: Newt client not downloaded.");
                return;
            }
        }
        
        var exePath = GetWorkerExePath();
        if (exePath == null)
        {
            ShowWindowsNotification("Newt VPN", "Cannot find service executable.");
            return;
        }

        var result = ServiceControlHelper.InstallService(exePath);
        ShowWindowsNotification("Newt VPN", result.message);
        UpdateMenuState();
    }

    private void UninstallService()
    {
        var result = ServiceControlHelper.UninstallService();
        ShowWindowsNotification("Newt VPN", result.message);
        UpdateMenuState();
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

    private string? GetInstalledNewtVersion()
    {
        if (!File.Exists(ServiceConstants.VersionFilePath))
            return null;
        return File.ReadAllText(ServiceConstants.VersionFilePath).Trim();
    }

    private WindowIcon CreateTrayIcon(bool running)
    {
        // Load base icon from embedded resource
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("NewtService.Tray.Assets.icon.png");
        
        if (stream != null)
        {
            using var bitmap = new System.Drawing.Bitmap(stream);
            using var result = new System.Drawing.Bitmap(32, 32);
            using var g = System.Drawing.Graphics.FromImage(result);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(System.Drawing.Color.Transparent);
            
            // Clip to circle for round icon
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, 32, 32);
            g.SetClip(path);
            g.DrawImage(bitmap, 0, 0, 32, 32);
            g.ResetClip();
            
            // Draw status indicator dot in corner
            var statusColor = running 
                ? System.Drawing.Color.FromArgb(76, 175, 80)
                : System.Drawing.Color.FromArgb(200, 200, 200);
            using var brush = new System.Drawing.SolidBrush(statusColor);
            g.FillEllipse(brush, 20, 20, 11, 11);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.5f);
            g.DrawEllipse(pen, 20, 20, 11, 11);
            
            using var ms = new MemoryStream();
            result.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new WindowIcon(ms);
        }
        
        // Fallback to simple colored circle
        using var fallback = new System.Drawing.Bitmap(32, 32);
        using var gFallback = System.Drawing.Graphics.FromImage(fallback);
        gFallback.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        gFallback.Clear(System.Drawing.Color.Transparent);
        var color = running 
            ? System.Drawing.Color.FromArgb(76, 175, 80)
            : System.Drawing.Color.FromArgb(158, 158, 158);
        using var fallbackBrush = new System.Drawing.SolidBrush(color);
        gFallback.FillEllipse(fallbackBrush, 2, 2, 28, 28);
        
        using var msf = new MemoryStream();
        fallback.Save(msf, System.Drawing.Imaging.ImageFormat.Png);
        msf.Position = 0;
        return new WindowIcon(msf);
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
