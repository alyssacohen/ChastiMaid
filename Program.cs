using Avalonia;
using ReactiveUI.Avalonia;

namespace ChastiMaid;

sealed class Program
{
    public static bool TestMode       { get; private set; }
    public static bool ForcePortrait  { get; private set; }
    public static bool ForceLandscape { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        TestMode       = args.Contains("--test");
        ForcePortrait  = args.Contains("--portrait");
        ForceLandscape = args.Contains("--landscape");
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
