using NewtService.Core;

namespace NewtService.Worker;

public class NewtWorker : BackgroundService
{
    private readonly ILogger<NewtWorker> _logger;
    private readonly NewtProcessManager _processManager;
    private readonly NewtUpdater _updater;
    private readonly NewtLogger _newtLogger;
    private NewtConfig _config;

    public NewtWorker(ILogger<NewtWorker> logger)
    {
        _logger = logger;
        _processManager = new NewtProcessManager();
        _updater = new NewtUpdater();
        _newtLogger = new NewtLogger();
        _config = new NewtConfig();

        _processManager.OnOutput += msg =>
        {
            _logger.LogInformation("[newt] {Message}", msg);
            _newtLogger.LogOutput(msg);
        };
        _processManager.OnError += msg =>
        {
            _logger.LogWarning("[newt] {Message}", msg);
            _newtLogger.LogError(msg);
        };
        _processManager.OnExit += code =>
        {
            _logger.LogWarning("Newt process exited with code {ExitCode}", code);
            _newtLogger.LogInfo($"Process exited with code {code}");
        };

        _updater.OnLog += msg =>
        {
            _logger.LogInformation("[updater] {Message}", msg);
            _newtLogger.LogInfo(msg);
        };
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Newt VPN Service starting");

        await Task.Run(() =>
        {
            Directory.CreateDirectory(ServiceConstants.AppDataPath);
            Directory.CreateDirectory(ServiceConstants.LogPath);
            _config = NewtConfig.Load();
        }, cancellationToken).ConfigureAwait(false);

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureNewtInstalledAsync().ConfigureAwait(false);

        if (!File.Exists(ServiceConstants.NewtExecutablePath))
        {
            _logger.LogError("Newt executable not found, service cannot start");
            return;
        }

        StartNewt();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_processManager.IsRunning)
            {
                _logger.LogWarning("Newt process not running, restarting...");
                _config = await Task.Run(() => NewtConfig.Load(), stoppingToken).ConfigureAwait(false);
                StartNewt();
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Newt VPN Service stopping");
        await Task.Run(() => _processManager.Stop(), cancellationToken).ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private void StartNewt()
    {
        _logger.LogInformation("Config - Endpoint: {Endpoint}, Id: {Id}, Secret: {Secret}",
            _config.Endpoint ?? "(null)",
            _config.Id ?? "(null)",
            string.IsNullOrEmpty(_config.Secret) ? "(null)" : "[redacted]");

        var args = _config.BuildCommandLineArgs();
        _logger.LogInformation("Starting newt with args: {Args}", string.Join(" ", args));
        _processManager.Start(args);
    }

    private async Task EnsureNewtInstalledAsync()
    {
        if (File.Exists(ServiceConstants.NewtExecutablePath))
        {
            _logger.LogInformation("Newt found at {Path}", ServiceConstants.NewtExecutablePath);
            return;
        }

        _logger.LogInformation("Newt not found, downloading latest version...");

        var release = await _updater.GetLatestReleaseAsync().ConfigureAwait(false);
        if (release == null)
        {
            _logger.LogError("Failed to get latest release");
            return;
        }

        var progress = new Progress<double>(p =>
            _logger.LogInformation("Download progress: {Progress:F1}%", p));

        await _updater.DownloadAndInstallAsync(release, progress).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _processManager.Dispose();
        _updater.Dispose();
        _newtLogger.Dispose();
        base.Dispose();
    }
}
