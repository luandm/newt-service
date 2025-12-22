using Avalonia;

namespace NewtService.Tray;

class Program
{
    public static bool ShowConfigOnStartup { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "NewtServiceTray", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ShowConfigOnStartup = args.Contains("--show-config");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
