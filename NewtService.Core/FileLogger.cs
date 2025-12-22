using System.Text;

namespace NewtService.Core;

public class FileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private const long MaxLogSize = 50 * 1024 * 1024; // 50MB
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 50;

    public string LogFilePath => _logFilePath;

    public FileLogger(string logFileName)
    {
        _logFilePath = Path.Combine(ServiceConstants.LogPath, logFileName);
        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        try
        {
            var logDir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
        }
        catch { }
    }

    public void Log(string message, string level = "INFO")
    {
        lock (_lock)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    EnsureDirectoryExists();
                    
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var line = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                    
                    // Append to file with shared access
                    using var stream = new FileStream(
                        _logFilePath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite);
                    
                    var bytes = Encoding.UTF8.GetBytes(line);
                    stream.Write(bytes, 0, bytes.Length);
                    
                    // Check size and truncate if needed (only occasionally)
                    if (stream.Length > MaxLogSize)
                    {
                        stream.Close();
                        TruncateLog();
                    }
                    
                    return;
                }
                catch (IOException) when (retry < MaxRetries - 1)
                {
                    Thread.Sleep(RetryDelayMs * (retry + 1));
                }
                catch
                {
                    return;
                }
            }
        }
    }

    private void TruncateLog()
    {
        try
        {
            var lines = File.ReadAllLines(_logFilePath);
            var halfLines = lines.Length / 2;
            if (halfLines > 0)
            {
                File.WriteAllLines(_logFilePath, lines.Skip(halfLines));
            }
        }
        catch { }
    }

    public void OpenFile()
    {
        EnsureDirectoryExists();
        
        try
        {
            if (!File.Exists(_logFilePath))
            {
                using var stream = new FileStream(
                    _logFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _logFilePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void Dispose()
    {
    }
}

