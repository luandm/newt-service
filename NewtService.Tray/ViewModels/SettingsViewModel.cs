using NewtService.Core;

namespace NewtService.Tray.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private bool _delayedStart;
    private bool _isInstalled;

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

    public SettingsViewModel()
    {
        IsInstalled = ServiceControlHelper.IsServiceInstalled();
        if (IsInstalled)
        {
            _delayedStart = ServiceControlHelper.GetDelayedStart();
        }
    }
}
