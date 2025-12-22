using Microsoft.Win32;
using NewtService.Core;

namespace NewtService.Tray.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NewtService";
    
    private bool _delayedStart;
    private bool _isInstalled;
    private bool _startOnBoot;

    public bool DelayedStart
    {
        get => _delayedStart;
        set
        {
            if (SetProperty(ref _delayedStart, value))
            {
                ServiceControlHelper.SetDelayedStart(value);
            }
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public bool StartOnBoot
    {
        get => _startOnBoot;
        set
        {
            if (SetProperty(ref _startOnBoot, value))
            {
                SetStartOnBoot(value);
            }
        }
    }

    public SettingsViewModel()
    {
        IsInstalled = ServiceControlHelper.IsServiceInstalled();
        if (IsInstalled)
        {
            _delayedStart = ServiceControlHelper.GetDelayedStart();
        }
        _startOnBoot = GetStartOnBoot();
    }

    private bool GetStartOnBoot()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    private void SetStartOnBoot(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }
}
