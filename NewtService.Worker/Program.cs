using NewtService.Core;
using NewtService.Worker;

var serviceLogPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "NewtService", "logs", "service.log");

void Log(string message)
{
    try
    {
        var dir = Path.GetDirectoryName(serviceLogPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
        var existing = File.Exists(serviceLogPath) ? File.ReadAllText(serviceLogPath) : "";
        File.WriteAllText(serviceLogPath, line + existing);
    }
    catch { }
}

try
{
    Log("=== NewtService Worker starting ===");
    Log($"Executable: {Environment.ProcessPath}");
    Log($"Working Dir: {Environment.CurrentDirectory}");
    Log($"Args: {string.Join(" ", args)}");
    
    Directory.CreateDirectory(ServiceConstants.AppDataPath);
    Directory.CreateDirectory(ServiceConstants.LogPath);
    
    Log("Directories created, building host...");
    
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<NewtWorker>();
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = ServiceConstants.ServiceName;
    });

    Log("Host built, starting...");
    
    var host = builder.Build();
    
    Log("Running host...");
    host.Run();
    
    Log("Host exited normally");
}
catch (Exception ex)
{
    Log($"FATAL ERROR: {ex}");
    throw;
}
