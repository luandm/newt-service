using System.ServiceProcess;

namespace NewtService.Core;

public static class ServiceControlHelper
{
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

