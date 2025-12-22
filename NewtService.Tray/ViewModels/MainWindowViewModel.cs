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

    public MainWindowViewModel()
    {
        AppLogger.Info("MainWindow opened");
        
        StartCommand = new AsyncRelayCommand(StartServiceAsync, () => IsInstalled && !IsRunning);
        StopCommand = new AsyncRelayCommand(StopServiceAsync, () => IsInstalled && IsRunning);
        RestartCommand = new AsyncRelayCommand(RestartServiceAsync, () => IsInstalled && IsRunning);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync);
        CheckServiceCommand = new RelayCommand(CheckService);
        CheckUpdateCommand = new AsyncRelayCommand(CheckOrInstallNewtUpdateAsync);
        CheckAppUpdateCommand = new AsyncRelayCommand(CheckOrInstallAppUpdateAsync);
        OpenGitHubCommand = new RelayCommand(OpenGitHub);
        OpenNewtLogsCommand = new RelayCommand(OpenNewtLogs);
        OpenAppLogsCommand = new RelayCommand(OpenAppLogs);
        OpenSettingsCommand = new RelayCommand(() => OnOpenSettings?.Invoke());

        LoadConfig();
        UpdateStatus();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
    }

    private void UpdateStatus()
    {
        var status = ServiceControlHelper.GetServiceStatus();
        Status = ServiceControlHelper.GetStatusText();
        IsRunning = status == ServiceControllerStatus.Running;
        IsInstalled = status != null;
        
        if (File.Exists(ServiceConstants.VersionFilePath))
            Version = File.ReadAllText(ServiceConstants.VersionFilePath).Trim();
        else
            Version = "Not installed";
    }

    private async Task StartServiceAsync()
    {
        Status = "Starting...";
        await ServiceControlHelper.StartServiceAsync();
        UpdateStatus();
    }

    private async Task StopServiceAsync()
    {
        Status = "Stopping...";
        await ServiceControlHelper.StopServiceAsync();
        UpdateStatus();
    }

    private async Task RestartServiceAsync()
    {
        Status = "Restarting...";
        await ServiceControlHelper.RestartServiceAsync();
        UpdateStatus();
    }

    private void LoadConfig()
    {
        var config = NewtConfig.Load();
        Endpoint = config.Endpoint ?? "";
        ClientId = config.Id ?? "";
        Secret = config.Secret ?? "";
    }

    private async Task SaveConfigAsync()
    {
        AppLogger.Info("Saving config...");
        
        var config = new NewtConfig
        {
            Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint.Trim(),
            Id = string.IsNullOrWhiteSpace(ClientId) ? null : ClientId.Trim(),
            Secret = string.IsNullOrWhiteSpace(Secret) ? null : Secret.Trim()
        };
        config.Save();
        AppLogger.Info("Config saved");

        // Auto-install service if config is valid and service not installed
        if (!IsInstalled && !string.IsNullOrEmpty(config.Endpoint) && 
            !string.IsNullOrEmpty(config.Id) && !string.IsNullOrEmpty(config.Secret))
        {
            AppLogger.Info("Service not installed, initiating install...");
            await InstallServiceAsync();
        }
    }

    private async Task InstallServiceAsync()
    {
        // Ensure newt.exe is downloaded
        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Downloading Newt...";
            AppLogger.Info("Newt.exe not found, downloading...");
            
            using var updater = new NewtUpdater();
            updater.OnLog += msg => Status = msg;
            
            var release = await updater.GetLatestReleaseAsync();
            if (release == null)
            {
                Status = "Failed to fetch release info";
                AppLogger.Error("Failed to fetch release info from GitHub");
                return;
            }
            
            var success = await updater.DownloadAndInstallAsync(release);
            if (!success)
            {
                AppLogger.Error($"Download failed. Status: {Status}");
                return;
            }
            AppLogger.Info("Newt.exe downloaded successfully");
        }

        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Newt.exe not found after download";
            AppLogger.Error("Newt.exe still not found after download attempt");
            return;
        }

        var exePath = GetWorkerExePath();
        if (exePath == null)
        {
            Status = "Service exe not found";
            AppLogger.Error("Worker exe not found");
            return;
        }

        Status = "Installing service...";
        AppLogger.Info($"Installing service from: {exePath}");
        
        var result = ServiceControlHelper.InstallService(exePath);
        if (!result.success)
        {
            Status = result.message;
            AppLogger.Error($"Service install failed: {result.message}");
            return;
        }
        
        AppLogger.Info("Service installed successfully");
        UpdateStatus();
        
        if (IsInstalled)
        {
            Status = "Starting service...";
            AppLogger.Info("Starting service...");
            await ServiceControlHelper.StartServiceAsync();
            UpdateStatus();
            AppLogger.Info($"Service status: {Status}");
        }
    }

    private void CheckService()
    {
        UpdateStatus();
        
        if (!IsInstalled)
        {
            Status = "Service not installed";
            return;
        }

        // Check if service executable exists
        var exePath = GetWorkerExePath();
        if (exePath == null || !File.Exists(exePath))
        {
            Status = "Service exe missing!";
            return;
        }

        // Check if newt.exe exists
        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Newt.exe missing!";
            return;
        }

        // Check config
        var config = NewtConfig.Load();
        if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.Id) || string.IsNullOrEmpty(config.Secret))
        {
            Status = "Config incomplete!";
            return;
        }

        Status = IsRunning ? "Running - OK" : "Stopped - OK";
    }

    private async Task CheckOrInstallNewtUpdateAsync()
    {
        if (UpdateAvailable && _availableUpdate != null)
        {
            UpdateButtonText = "Newt: Updating...";
            
            var wasRunning = IsRunning;
            if (wasRunning)
                await ServiceControlHelper.StopServiceAsync();

            using var updater = new NewtUpdater();
            await updater.DownloadAndInstallAsync(_availableUpdate);

            _availableUpdate = null;
            UpdateAvailable = false;
            UpdateButtonText = "Newt: Check";

            if (wasRunning)
                await ServiceControlHelper.StartServiceAsync();

            UpdateStatus();
        }
        else
        {
            UpdateButtonText = "Newt: Checking...";
            
            using var updater = new NewtUpdater();
            var current = File.Exists(ServiceConstants.VersionFilePath) 
                ? File.ReadAllText(ServiceConstants.VersionFilePath).Trim() 
                : null;
            var latest = await updater.GetLatestReleaseAsync();

            if (latest == null)
            {
                UpdateButtonText = "Newt: Check failed";
                await Task.Delay(2000);
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
                await Task.Delay(2000);
                UpdateButtonText = "Newt: Check";
            }
        }
    }

    public event Action? OnRequestExit;

    private async Task CheckOrInstallAppUpdateAsync()
    {
        if (_availableAppUpdate != null)
        {
            AppUpdateButtonText = "App: Downloading...";
            
            using var updater = new AppUpdater();
            updater.OnRequestExit += () => OnRequestExit?.Invoke();
            
            var success = await updater.DownloadAndInstallAsync(_availableAppUpdate);
            
            if (!success)
            {
                AppUpdateButtonText = "App: Update failed";
                await Task.Delay(2000);
                updater.OpenReleasesPage();
            }
        }
        else
        {
            AppUpdateButtonText = "App: Checking...";
            
            using var updater = new AppUpdater();
            var current = AppUpdater.GetCurrentVersion();
            var latest = await updater.GetLatestReleaseAsync();

            if (latest == null || current == null)
            {
                AppUpdateButtonText = "App: Check failed";
                await Task.Delay(2000);
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
                await Task.Delay(2000);
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

    private void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/memesalot/newt-service",
            UseShellExecute = true
        });
    }

    private void OpenNewtLogs()
    {
        NewtLogger.OpenLogFile();
    }

    private void OpenAppLogs()
    {
        AppLogger.OpenLogFile();
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

    public void Dispose()
    {
        _statusTimer.Stop();
    }
}
