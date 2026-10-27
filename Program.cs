using Avalonia;
using ReactiveUI.Avalonia;

namespace ChastiMaid;

internal sealed class Program
{
    public static bool ForceLandscape { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ForceLandscape = args.Contains("--landscape");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
    }
}
