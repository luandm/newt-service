namespace NewtService.Core;

public class NewtLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private long _currentSize;
    private const long MaxLogSize = 100 * 1024 * 1024; // 100MB
    private const long TruncateToSize = 50 * 1024 * 1024; // Truncate to 50MB when limit hit

    public static string LogFilePath => Path.Combine(ServiceConstants.LogPath, "newt.log");

    public NewtLogger()
    {
        _logFilePath = LogFilePath;
        Initialize();
    }

    private void Initialize()
    {
        var logDir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        _currentSize = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0;
        
        _writer = new StreamWriter(_logFilePath, append: true)
        {
            AutoFlush = true
        };
    }

    public void Log(string message, string level = "INFO")
    {
        lock (_lock)
        {
            if (_writer == null) return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{timestamp}] [{level}] {message}";
                
                _writer.WriteLine(line);
                _currentSize += line.Length + Environment.NewLine.Length;

                if (_currentSize >= MaxLogSize)
                    TruncateLog();
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    public void LogOutput(string message) => Log(message, "OUT");
    public void LogError(string message) => Log(message, "ERR");
    public void LogInfo(string message) => Log(message, "INFO");

    private void TruncateLog()
    {
        try
        {
            _writer?.Close();
            _writer = null;

            var lines = File.ReadAllLines(_logFilePath);
            
            // Keep approximately the last 50MB worth of lines
            long totalSize = 0;
            int startIndex = lines.Length;
            
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                totalSize += lines[i].Length + Environment.NewLine.Length;
                if (totalSize >= TruncateToSize)
                {
                    startIndex = i + 1;
                    break;
                }
            }

            var keepLines = lines.Skip(startIndex).ToArray();
            File.WriteAllLines(_logFilePath, keepLines);
            _currentSize = keepLines.Sum(l => l.Length + Environment.NewLine.Length);

            _writer = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
        }
        catch
        {
            // If truncation fails, just start fresh
            try
            {
                File.Delete(_logFilePath);
                _currentSize = 0;
                _writer = new StreamWriter(_logFilePath, append: false) { AutoFlush = true };
            }
            catch { }
        }
    }

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

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

