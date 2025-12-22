using System.Diagnostics;
using Avalonia;
using NewtService.Core;

namespace NewtService.Tray;

class Program
{
    public static bool ShowConfigOnStartup { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--apply-update")
        {
            ApplyUpdate(args[1], args[2]);
            return;
        }

        CleanupOldFiles();
        CleanupStagingFolder();

        using var mutex = new Mutex(true, "NewtServiceTray", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ShowConfigOnStartup = args.Contains("--show-config");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void ApplyUpdate(string appDir, string pidStr)
    {
        try
        {
            if (int.TryParse(pidStr, out int pid))
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    proc.WaitForExit(30000);
                }
                catch { }
            }

            Thread.Sleep(500);

            var stagingDir = AppContext.BaseDirectory;
            var traySource = Path.Combine(stagingDir, "NewtService.Tray.exe");
            var workerSource = Path.Combine(stagingDir, "NewtService.Worker.exe");
            var trayDest = Path.Combine(appDir, "NewtService.Tray.exe");
            var workerDest = Path.Combine(appDir, "NewtService.Worker.exe");

            if (File.Exists(trayDest))
            {
                var oldPath = trayDest + ".old";
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(trayDest, oldPath);
            }

            if (File.Exists(workerDest))
            {
                var oldPath = workerDest + ".old";
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(workerDest, oldPath);
            }

            File.Copy(traySource, trayDest, true);
            if (File.Exists(workerSource))
                File.Copy(workerSource, workerDest, true);

            Process.Start(new ProcessStartInfo
            {
                FileName = trayDest,
                UseShellExecute = true,
                WorkingDirectory = appDir
            });
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "NewtServiceUpdate.log"),
                $"{DateTime.Now}: Update failed: {ex}");
        }
    }

    private static void CleanupOldFiles()
    {
        try
        {
            var appDir = AppContext.BaseDirectory;
            var trayOld = Path.Combine(appDir, "NewtService.Tray.exe.old");
            var workerOld = Path.Combine(appDir, "NewtService.Worker.exe.old");
            
            if (File.Exists(trayOld)) File.Delete(trayOld);
            if (File.Exists(workerOld)) File.Delete(workerOld);
        }
        catch { }
    }

    private static void CleanupStagingFolder()
    {
        try
        {
            if (Directory.Exists(AppUpdater.StagingPath))
                Directory.Delete(AppUpdater.StagingPath, true);
        }
        catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
