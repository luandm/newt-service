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
    private bool _showSecret;

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

    public bool ShowSecret
    {
        get => _showSecret;
        set => SetProperty(ref _showSecret, value);
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveConfigCommand { get; }

    public MainWindowViewModel()
    {
        StartCommand = new AsyncRelayCommand(StartServiceAsync, () => IsInstalled && !IsRunning);
        StopCommand = new AsyncRelayCommand(StopServiceAsync, () => IsInstalled && IsRunning);
        RestartCommand = new AsyncRelayCommand(RestartServiceAsync, () => IsInstalled && IsRunning);
        SaveConfigCommand = new RelayCommand(SaveConfig);

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

    private void SaveConfig()
    {
        var config = new NewtConfig
        {
            Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint.Trim(),
            Id = string.IsNullOrWhiteSpace(ClientId) ? null : ClientId.Trim(),
            Secret = string.IsNullOrWhiteSpace(Secret) ? null : Secret.Trim()
        };
        config.Save();
    }

    public void Dispose()
    {
        _statusTimer.Stop();
    }
}
