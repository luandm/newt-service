namespace NewtService.Core;

public class NewtLogger : IDisposable
{
    private readonly FileLogger _logger;

    public static string LogFilePath => Path.Combine(ServiceConstants.LogPath, "newt.log");

    public NewtLogger()
    {
        _logger = new FileLogger("newt.log");
    }

    public void Log(string message, string level = "INFO") => _logger.Log(message, level);
    public void LogOutput(string message) => _logger.Log(message, "OUT");
    public void LogError(string message) => _logger.Log(message, "ERR");
    public void LogInfo(string message) => _logger.Log(message, "INFO");

    public static void OpenLogFile()
    {
        var logFile = LogFilePath;
        if (!File.Exists(logFile))
        {
            var logDir = Path.GetDirectoryName(logFile);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            File.WriteAllText(logFile, "");
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = logFile,
            UseShellExecute = true
        });
    }

    public static void OpenLogFolder() => AppLogger.OpenLogFolder();

    public void Dispose() => _logger.Dispose();
}
