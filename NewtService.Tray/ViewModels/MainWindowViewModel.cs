using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Input;
using Avalonia.Threading;
using NewtService.Core;

namespace NewtService.Tray.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _statusTimer;
    private string _status = "Checking...";
    private string _version = "Unknown";
    private bool _isRunning;
    private bool _isInstalled;
    private string _endpoint = "";
    private string _clientId = "";
    private string _secret = "";
    private string _updateButtonText = "Newt: Check";
    private bool _updateAvailable;
    private GitHubRelease? _availableUpdate;
    private string _appUpdateButtonText = "App: Check";
    private AppRelease? _availableAppUpdate;

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RestartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        set => SetProperty(ref _endpoint, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public string Secret
    {
        get => _secret;
        set => SetProperty(ref _secret, value);
    }

    public string UpdateButtonText
    {
        get => _updateButtonText;
        set => SetProperty(ref _updateButtonText, value);
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set => SetProperty(ref _updateAvailable, value);
    }

    public string AppUpdateButtonText
    {
        get => _appUpdateButtonText;
        set => SetProperty(ref _appUpdateButtonText, value);
    }

    public string AppVersion => AppUpdater.GetCurrentVersion() ?? "Unknown";

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand CheckServiceCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand CheckAppUpdateCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenNewtLogsCommand { get; }
    public ICommand OpenAppLogsCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event Action? OnOpenSettings;
    public event Action? OnRequestExit;

    public MainWindowViewModel()
    {
        StartCommand = new AsyncRelayCommand(StartServiceAsync, () => IsInstalled && !IsRunning);
        StopCommand = new AsyncRelayCommand(StopServiceAsync, () => IsInstalled && IsRunning);
        RestartCommand = new AsyncRelayCommand(RestartServiceAsync, () => IsInstalled && IsRunning);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync);
        CheckServiceCommand = new AsyncRelayCommand(CheckServiceAsync);
        CheckUpdateCommand = new AsyncRelayCommand(CheckOrInstallNewtUpdateAsync);
        CheckAppUpdateCommand = new AsyncRelayCommand(CheckOrInstallAppUpdateAsync);
        OpenGitHubCommand = new RelayCommand(OpenGitHub);
        OpenNewtLogsCommand = new RelayCommand(OpenNewtLogs);
        OpenAppLogsCommand = new RelayCommand(OpenAppLogs);
        OpenSettingsCommand = new RelayCommand(() => OnOpenSettings?.Invoke());

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _statusTimer.Start();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await Task.Run(() => AppLogger.Info("MainWindow opened")).ConfigureAwait(false);
        await LoadConfigAsync().ConfigureAwait(false);
        await RefreshStatusAsync().ConfigureAwait(false);
    }

    private async Task RefreshStatusAsync()
    {
        var (status, statusText, version) = await Task.Run(() =>
        {
            var s = ServiceControlHelper.GetServiceStatus();
            var text = s switch
            {
                ServiceControllerStatus.Running => "Running",
                ServiceControllerStatus.Stopped => "Stopped",
                ServiceControllerStatus.Paused => "Paused",
                ServiceControllerStatus.StartPending => "Starting...",
                ServiceControllerStatus.StopPending => "Stopping...",
                null => "Not Installed",
                _ => "Unknown"
            };
            var ver = File.Exists(ServiceConstants.VersionFilePath)
                ? File.ReadAllText(ServiceConstants.VersionFilePath).Trim()
                : "Not installed";
            return (s, text, ver);
        }).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Status = statusText;
            IsRunning = status == ServiceControllerStatus.Running;
            IsInstalled = status != null;
            Version = version;
        });
    }

    private async Task StartServiceAsync()
    {
        Status = "Starting...";
        await ServiceControlHelper.StartServiceAsync().ConfigureAwait(false);
        await RefreshStatusAsync().ConfigureAwait(false);
    }

    private async Task StopServiceAsync()
    {
        Status = "Stopping...";
        await ServiceControlHelper.StopServiceAsync().ConfigureAwait(false);
        await RefreshStatusAsync().ConfigureAwait(false);
    }

    private async Task RestartServiceAsync()
    {
        Status = "Restarting...";
        await ServiceControlHelper.RestartServiceAsync().ConfigureAwait(false);
        await RefreshStatusAsync().ConfigureAwait(false);
    }

    private async Task LoadConfigAsync()
    {
        var config = await Task.Run(() => NewtConfig.Load()).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Endpoint = config.Endpoint ?? "";
            ClientId = config.Id ?? "";
            Secret = config.Secret ?? "";
        });
    }

    private async Task SaveConfigAsync()
    {
        var config = new NewtConfig
        {
            Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint.Trim(),
            Id = string.IsNullOrWhiteSpace(ClientId) ? null : ClientId.Trim(),
            Secret = string.IsNullOrWhiteSpace(Secret) ? null : Secret.Trim()
        };

        await Task.Run(() =>
        {
            AppLogger.Info("Saving config...");
            config.Save();
            AppLogger.Info("Config saved");
        }).ConfigureAwait(false);

        if (!IsInstalled && !string.IsNullOrEmpty(config.Endpoint) &&
            !string.IsNullOrEmpty(config.Id) && !string.IsNullOrEmpty(config.Secret))
        {
            await Task.Run(() => AppLogger.Info("Service not installed, initiating install...")).ConfigureAwait(false);
            await InstallServiceAsync().ConfigureAwait(false);
        }
    }

    private async Task InstallServiceAsync()
    {
        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Downloading Newt...";
            await Task.Run(() => AppLogger.Info("Newt.exe not found, downloading...")).ConfigureAwait(false);

            using var updater = new NewtUpdater();
            updater.OnLog += msg => Dispatcher.UIThread.Post(() => Status = msg);

            var release = await updater.GetLatestReleaseAsync().ConfigureAwait(false);
            if (release == null)
            {
                Status = "Failed to fetch release info";
                await Task.Run(() => AppLogger.Error("Failed to fetch release info from GitHub")).ConfigureAwait(false);
                return;
            }

            var success = await updater.DownloadAndInstallAsync(release).ConfigureAwait(false);
            if (!success)
            {
                await Task.Run(() => AppLogger.Error($"Download failed. Status: {Status}")).ConfigureAwait(false);
                return;
            }
            await Task.Run(() => AppLogger.Info("Newt.exe downloaded successfully")).ConfigureAwait(false);
        }

        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Newt.exe not found after download";
            await Task.Run(() => AppLogger.Error("Newt.exe still not found after download attempt")).ConfigureAwait(false);
            return;
        }

        var exePath = GetWorkerExePath();
        if (exePath == null)
        {
            Status = "Service exe not found";
            await Task.Run(() => AppLogger.Error("Worker exe not found")).ConfigureAwait(false);
            return;
        }

        Status = "Installing service...";
        await Task.Run(() => AppLogger.Info($"Installing service from: {exePath}")).ConfigureAwait(false);

        var result = ServiceControlHelper.InstallService(exePath);
        if (!result.success)
        {
            Status = result.message;
            await Task.Run(() => AppLogger.Error($"Service install failed: {result.message}")).ConfigureAwait(false);
            return;
        }

        await Task.Run(() => AppLogger.Info("Service installed successfully")).ConfigureAwait(false);
        await RefreshStatusAsync().ConfigureAwait(false);

        if (IsInstalled)
        {
            Status = "Starting service...";
            await Task.Run(() => AppLogger.Info("Starting service...")).ConfigureAwait(false);
            await ServiceControlHelper.StartServiceAsync().ConfigureAwait(false);
            await RefreshStatusAsync().ConfigureAwait(false);
            await Task.Run(() => AppLogger.Info($"Service status: {Status}")).ConfigureAwait(false);
        }
    }

    private async Task CheckServiceAsync()
    {
        Status = "Checking...";

        var result = await Task.Run(() =>
        {
            var status = ServiceControlHelper.GetServiceStatus();
            var isInstalled = status != null;
            var isRunning = status == ServiceControllerStatus.Running;

            if (!isInstalled)
                return "Service not installed";

            var exePath = GetWorkerExePath();
            if (exePath == null || !File.Exists(exePath))
                return "Service exe missing!";

            if (!File.Exists(ServiceConstants.NewtExecutablePath))
                return "Newt.exe missing!";

            var config = NewtConfig.Load();
            if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.Id) || string.IsNullOrEmpty(config.Secret))
                return "Config incomplete!";

            return isRunning ? "Running - OK" : "Stopped - OK";
        }).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() => Status = result);
    }

    private async Task CheckOrInstallNewtUpdateAsync()
    {
        if (UpdateAvailable && _availableUpdate != null)
        {
            UpdateButtonText = "Newt: Updating...";

            var wasRunning = IsRunning;
            if (wasRunning)
                await ServiceControlHelper.StopServiceAsync().ConfigureAwait(false);

            using var updater = new NewtUpdater();
            await updater.DownloadAndInstallAsync(_availableUpdate).ConfigureAwait(false);

            _availableUpdate = null;
            UpdateAvailable = false;
            UpdateButtonText = "Newt: Check";

            if (wasRunning)
                await ServiceControlHelper.StartServiceAsync().ConfigureAwait(false);

            await RefreshStatusAsync().ConfigureAwait(false);
        }
        else
        {
            UpdateButtonText = "Newt: Checking...";

            using var updater = new NewtUpdater();
            var current = await Task.Run(() =>
                File.Exists(ServiceConstants.VersionFilePath)
                    ? File.ReadAllText(ServiceConstants.VersionFilePath).Trim()
                    : null
            ).ConfigureAwait(false);

            var latest = await updater.GetLatestReleaseAsync().ConfigureAwait(false);

            if (latest == null)
            {
                UpdateButtonText = "Newt: Check failed";
                await Task.Delay(2000).ConfigureAwait(false);
                UpdateButtonText = "Newt: Check";
                return;
            }

            if (current != latest.TagName)
            {
                _availableUpdate = latest;
                UpdateAvailable = true;
                UpdateButtonText = $"Newt: Install ({latest.TagName})";
            }
            else
            {
                UpdateButtonText = "Newt: Up to date";
                await Task.Delay(2000).ConfigureAwait(false);
                UpdateButtonText = "Newt: Check";
            }
        }
    }

    private async Task CheckOrInstallAppUpdateAsync()
    {
        if (_availableAppUpdate != null)
        {
            AppUpdateButtonText = "App: Downloading...";

            using var updater = new AppUpdater();
            updater.OnRequestExit += () => OnRequestExit?.Invoke();

            var success = await updater.DownloadAndInstallAsync(_availableAppUpdate).ConfigureAwait(false);

            if (!success)
            {
                AppUpdateButtonText = "App: Update failed";
                await Task.Delay(2000).ConfigureAwait(false);
                updater.OpenReleasesPage();
            }
        }
        else
        {
            AppUpdateButtonText = "App: Checking...";

            using var updater = new AppUpdater();
            var current = AppUpdater.GetCurrentVersion();
            var latest = await updater.GetLatestReleaseAsync().ConfigureAwait(false);

            if (latest == null || current == null)
            {
                AppUpdateButtonText = "App: Check failed";
                await Task.Delay(2000).ConfigureAwait(false);
                AppUpdateButtonText = "App: Check";
                return;
            }

            if (CompareVersions(latest.Version, current) > 0)
            {
                _availableAppUpdate = latest;
                AppUpdateButtonText = $"App: Install ({latest.TagName})";
            }
            else
            {
                AppUpdateButtonText = "App: Up to date";
                await Task.Delay(2000).ConfigureAwait(false);
                AppUpdateButtonText = "App: Check";
            }
        }
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

    private static void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/memesalot/newt-service",
            UseShellExecute = true
        });
    }

    private static void OpenNewtLogs() => NewtLogger.OpenLogFile();

    private static void OpenAppLogs() => AppLogger.OpenLogFile();

    private static string? GetWorkerExePath()
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

    public void Dispose()
    {
        _statusTimer.Stop();
    }
}
