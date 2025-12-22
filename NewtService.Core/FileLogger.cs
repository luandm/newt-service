namespace NewtService.Core;

public class FileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private const long MaxLogSize = 50 * 1024 * 1024; // 50MB
    private const long TruncateToSize = 25 * 1024 * 1024; // Truncate to 25MB

    public string LogFilePath => _logFilePath;

    public FileLogger(string logFileName)
    {
        _logFilePath = Path.Combine(ServiceConstants.LogPath, logFileName);
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        var logDir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);
    }

    public void Log(string message, string level = "INFO")
    {
        lock (_lock)
        {
            try
            {
                EnsureDirectoryExists();
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{timestamp}] [{level}] {message}";
                
                // Prepend new line to file (newest first)
                var existingContent = File.Exists(_logFilePath) ? File.ReadAllText(_logFilePath) : "";
                var newContent = line + Environment.NewLine + existingContent;
                
                // Truncate if too large (keep newest lines at top)
                if (newContent.Length > MaxLogSize)
                {
                    newContent = newContent.Substring(0, (int)TruncateToSize);
                    var lastNewline = newContent.LastIndexOf(Environment.NewLine);
                    if (lastNewline > 0)
                        newContent = newContent.Substring(0, lastNewline);
                }
                
                File.WriteAllText(_logFilePath, newContent);
            }
            catch
            {
            }
        }
    }

    public void OpenFile()
    {
        EnsureDirectoryExists();
        
        if (!File.Exists(_logFilePath))
            File.WriteAllText(_logFilePath, "");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _logFilePath,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
    }
}

