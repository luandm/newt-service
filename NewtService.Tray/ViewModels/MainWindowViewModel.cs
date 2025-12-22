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
    private string _updateButtonText = "Check for Update";
    private bool _updateAvailable;
    private GitHubRelease? _availableUpdate;

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

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand CheckServiceCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand OpenGitHubCommand { get; }

    public MainWindowViewModel()
    {
        StartCommand = new AsyncRelayCommand(StartServiceAsync, () => IsInstalled && !IsRunning);
        StopCommand = new AsyncRelayCommand(StopServiceAsync, () => IsInstalled && IsRunning);
        RestartCommand = new AsyncRelayCommand(RestartServiceAsync, () => IsInstalled && IsRunning);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync);
        CheckServiceCommand = new RelayCommand(CheckService);
        CheckUpdateCommand = new AsyncRelayCommand(CheckOrInstallUpdateAsync);
        OpenGitHubCommand = new RelayCommand(OpenGitHub);

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
        var config = new NewtConfig
        {
            Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint.Trim(),
            Id = string.IsNullOrWhiteSpace(ClientId) ? null : ClientId.Trim(),
            Secret = string.IsNullOrWhiteSpace(Secret) ? null : Secret.Trim()
        };
        config.Save();

        // Auto-install service if config is valid and service not installed
        if (!IsInstalled && !string.IsNullOrEmpty(config.Endpoint) && 
            !string.IsNullOrEmpty(config.Id) && !string.IsNullOrEmpty(config.Secret))
        {
            await InstallServiceAsync();
        }
    }

    private async Task InstallServiceAsync()
    {
        // Ensure newt.exe is downloaded
        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Downloading Newt...";
            using var updater = new NewtUpdater();
            var release = await updater.GetLatestReleaseAsync();
            if (release != null)
            {
                await updater.DownloadAndInstallAsync(release);
            }
        }

        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            Status = "Download failed";
            return;
        }

        var exePath = GetWorkerExePath();
        if (exePath == null)
        {
            Status = "Service exe not found";
            return;
        }

        Status = "Installing service...";
        
        RunScCommand($"create {ServiceConstants.ServiceName} binPath= \"{exePath}\" start= auto DisplayName= \"{ServiceConstants.ServiceDisplayName}\"");
        RunScCommand($"description {ServiceConstants.ServiceName} \"{ServiceConstants.ServiceDescription}\"");
        
        UpdateStatus();
        
        if (IsInstalled)
        {
            await ServiceControlHelper.StartServiceAsync();
            UpdateStatus();
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

    private async Task CheckOrInstallUpdateAsync()
    {
        if (UpdateAvailable && _availableUpdate != null)
        {
            // Install the update
            UpdateButtonText = "Updating...";
            
            var wasRunning = IsRunning;
            if (wasRunning)
                await ServiceControlHelper.StopServiceAsync();

            using var updater = new NewtUpdater();
            await updater.DownloadAndInstallAsync(_availableUpdate);

            _availableUpdate = null;
            UpdateAvailable = false;
            UpdateButtonText = "Check for Update";

            if (wasRunning)
                await ServiceControlHelper.StartServiceAsync();

            UpdateStatus();
        }
        else
        {
            // Check for updates
            UpdateButtonText = "Checking...";
            
            using var updater = new NewtUpdater();
            var current = File.Exists(ServiceConstants.VersionFilePath) 
                ? File.ReadAllText(ServiceConstants.VersionFilePath).Trim() 
                : null;
            var latest = await updater.GetLatestReleaseAsync();

            if (latest == null)
            {
                UpdateButtonText = "Check failed";
                await Task.Delay(2000);
                UpdateButtonText = "Check for Update";
                return;
            }

            if (current != latest.TagName)
            {
                _availableUpdate = latest;
                UpdateAvailable = true;
                UpdateButtonText = $"Install Update ({latest.TagName})";
            }
            else
            {
                UpdateButtonText = "Up to date";
                await Task.Delay(2000);
                UpdateButtonText = "Check for Update";
            }
        }
    }

    private void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/memesalot/newt-service",
            UseShellExecute = true
        });
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

    public void Dispose()
    {
        _statusTimer.Stop();
    }
}
