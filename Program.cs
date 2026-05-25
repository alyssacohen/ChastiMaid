using Avalonia;
using ReactiveUI.Avalonia;

namespace ChastiMaid;

sealed class Program
{
    public static bool TestMode { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        TestMode = args.Contains("--test");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
