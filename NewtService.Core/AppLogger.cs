namespace NewtService.Core;

public static class AppLogger
{
    private static readonly Lazy<FileLogger> _instance = new(() => new FileLogger("app.log"));

    public static string LogFilePath => _instance.Value.LogFilePath;

    public static void Log(string message) => _instance.Value.Log(message, "INFO");
    public static void Info(string message) => _instance.Value.Log(message, "INFO");
    public static void Error(string message) => _instance.Value.Log(message, "ERROR");
    public static void Warn(string message) => _instance.Value.Log(message, "WARN");
    public static void Debug(string message) => _instance.Value.Log(message, "DEBUG");

    public static void OpenLogFile() => _instance.Value.OpenFile();

    public static void OpenLogFolder()
    {
        var logDir = ServiceConstants.LogPath;
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }
}

