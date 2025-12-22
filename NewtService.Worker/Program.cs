using NewtService.Core;
using NewtService.Worker;

try
{
    AppLogger.Info("=== NewtService Worker starting ===");
    AppLogger.Info($"Executable: {Environment.ProcessPath}");
    AppLogger.Info($"Working Dir: {Environment.CurrentDirectory}");
    AppLogger.Info($"Args: {string.Join(" ", args)}");

    Directory.CreateDirectory(ServiceConstants.AppDataPath);
    Directory.CreateDirectory(ServiceConstants.LogPath);

    AppLogger.Info("Directories created, building host...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<NewtWorker>();
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = ServiceConstants.ServiceName;
    });

    AppLogger.Info("Host built, starting...");

    var host = builder.Build();

    AppLogger.Info("Running host...");
    host.Run();

    AppLogger.Info("Host exited normally");
}
catch (Exception ex)
{
    AppLogger.Error($"FATAL ERROR: {ex}");
    throw;
}
