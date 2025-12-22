using Avalonia.Threading;
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
                Task.Run(() => ServiceControlHelper.SetDelayedStart(value));
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
                Task.Run(() => SetStartOnBoot(value));
            }
        }
    }

    public SettingsViewModel()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var (isInstalled, delayedStart, startOnBoot) = await Task.Run(() =>
        {
            var installed = ServiceControlHelper.IsServiceInstalled();
            var delayed = installed && ServiceControlHelper.GetDelayedStart();
            var boot = GetStartOnBoot();
            return (installed, delayed, boot);
        }).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsInstalled = isInstalled;
            _delayedStart = delayedStart;
            OnPropertyChanged(nameof(DelayedStart));
            _startOnBoot = startOnBoot;
            OnPropertyChanged(nameof(StartOnBoot));
        });
    }

    private static bool GetStartOnBoot()
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

    private static void SetStartOnBoot(bool enable)
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
