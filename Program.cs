using System.Runtime.InteropServices;
using Avalonia;
using ReactiveUI.Avalonia;

namespace ChastiMaid;

internal sealed class Program
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    public static bool ForceLandscape { get; private set; }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    [STAThread]
    public static void Main(string[] args)
    {
        ForceLandscape = args.Contains("--landscape");

        // No standby and no monitor timeout while the app runs. The request lives as long as this (UI) thread,
        // so Windows drops it by itself when the app closes.
        SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);

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
