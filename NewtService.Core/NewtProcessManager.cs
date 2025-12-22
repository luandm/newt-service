using System.Diagnostics;

namespace NewtService.Core;

public class NewtProcessManager : IDisposable
{
    private Process? _process;
    private readonly object _lock = new();
    private bool _disposed;

    public event Action<string>? OnOutput;
    public event Action<string>? OnError;
    public event Action<int>? OnExit;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_lock)
            {
                return _process?.Id;
            }
        }
    }

    public bool Start(string[] args)
    {
        lock (_lock)
        {
            if (IsRunning)
                return true;

            if (!File.Exists(ServiceConstants.NewtExecutablePath))
            {
                OnError?.Invoke($"Newt executable not found at {ServiceConstants.NewtExecutablePath}");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ServiceConstants.NewtExecutablePath,
                    Arguments = string.Join(" ", args.Select(EscapeArgument)),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = ServiceConstants.AppDataPath
                };

                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                
                _process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) OnOutput?.Invoke(e.Data);
                };
                
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) OnError?.Invoke(e.Data);
                };
                
                _process.Exited += (_, _) =>
                {
                    lock (_lock)
                    {
                        var exitCode = _process?.ExitCode ?? -1;
                        OnExit?.Invoke(exitCode);
                    }
                };

                if (!_process.Start())
                {
                    OnError?.Invoke("Failed to start newt process");
                    return false;
                }

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start newt: {ex.Message}");
                return false;
            }
        }
    }

    public void Stop(int timeoutMs = 5000)
    {
        lock (_lock)
        {
            if (_process == null || _process.HasExited)
                return;

            try
            {
                if (!_process.CloseMainWindow())
                {
                    _process.Kill();
                }
                
                if (!_process.WaitForExit(timeoutMs))
                {
                    _process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error stopping process: {ex.Message}");
            }
            finally
            {
                _process?.Dispose();
                _process = null;
            }
        }
    }

    public void Restart(string[] args)
    {
        Stop();
        Start(args);
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('"')) return arg;
        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}

