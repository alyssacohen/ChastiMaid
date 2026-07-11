using Avalonia;
using ReactiveUI.Avalonia;

namespace ChastiMaid;

internal sealed class Program
{
    public static bool ForcePortrait { get; private set; }
    public static bool ForceLandscape { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ForcePortrait = args.Contains("--portrait");
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
