using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;

namespace NewtService.Core;

public static class ServiceControlHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static (bool success, string message) InstallService(string workerExePath)
    {
        if (!File.Exists(workerExePath))
            return (false, "Service executable not found");

        var createResult = RunScCommand($"create {ServiceConstants.ServiceName} binPath= \"{workerExePath}\" start= auto DisplayName= \"{ServiceConstants.ServiceDisplayName}\"");
        if (!createResult.success)
            return (false, createResult.message);

        RunScCommand($"description {ServiceConstants.ServiceName} \"{ServiceConstants.ServiceDescription}\"");
        
        return IsServiceInstalled() 
            ? (true, "Service installed successfully") 
            : (false, "Service installation failed");
    }

    public static (bool success, string message) UninstallService()
    {
        if (!IsServiceInstalled())
            return (true, "Service not installed");

        if (IsServiceRunning())
        {
            var stopResult = StopServiceAsync().Result;
            if (!stopResult)
                return (false, "Failed to stop service");
        }

        var deleteResult = RunScCommand($"delete {ServiceConstants.ServiceName}");
        if (!deleteResult.success)
            return (false, deleteResult.message);

        return (true, "Service uninstalled successfully");
    }

    private static (bool success, string message) RunScCommand(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = IsRunningAsAdmin() ? "" : "runas",
                CreateNoWindow = true
            };
            
            var process = Process.Start(psi);
            if (process == null)
                return (false, "Failed to start sc.exe");
            
            process.WaitForExit(15000);
            
            return process.ExitCode == 0 
                ? (true, "Success") 
                : (false, $"Operation failed (error {process.ExitCode})");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Cancelled - admin required");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    public static ServiceControllerStatus? GetServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(ServiceConstants.ServiceName);
            return sc.Status;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsServiceInstalled()
    {
        return GetServiceStatus() != null;
    }

    public static bool IsServiceRunning()
    {
        return GetServiceStatus() == ServiceControllerStatus.Running;
    }

    public static async Task<bool> StartServiceAsync(int timeoutSeconds = 30)
    {
        try
        {
            using var sc = new ServiceController(ServiceConstants.ServiceName);
            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            sc.Start();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeoutSeconds)));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> StopServiceAsync(int timeoutSeconds = 30)
    {
        try
        {
            using var sc = new ServiceController(ServiceConstants.ServiceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
                return true;

            sc.Stop();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeoutSeconds)));
            return sc.Status == ServiceControllerStatus.Stopped;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> RestartServiceAsync(int timeoutSeconds = 30)
    {
        if (!await StopServiceAsync(timeoutSeconds))
            return false;
        return await StartServiceAsync(timeoutSeconds);
    }

    public static string GetStatusText()
    {
        var status = GetServiceStatus();
        return status switch
        {
            ServiceControllerStatus.Running => "Running",
            ServiceControllerStatus.Stopped => "Stopped",
            ServiceControllerStatus.Paused => "Paused",
            ServiceControllerStatus.StartPending => "Starting...",
            ServiceControllerStatus.StopPending => "Stopping...",
            ServiceControllerStatus.ContinuePending => "Resuming...",
            ServiceControllerStatus.PausePending => "Pausing...",
            null => "Not Installed",
            _ => "Unknown"
        };
    }
}

