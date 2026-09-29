using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>
///     Randomly snaps a photo or short video at random intervals (bounded by <see cref="GameSettings.BusyCaptureMinIntervalSeconds" />
///     / <see cref="GameSettings.BusyCaptureMaxIntervalSeconds" />) until cancelled. Shared by any "busy mode"
///     screen (chores, Writing Lines) that needs unattended presence evidence. Webcam failures are swallowed —
///     missing hardware never interrupts the activity in progress.
/// </summary>
public static class SilentCaptureLoop
{
    public static void Start(GameSettings settings, int cameraIndex, string slotName, string filePrefix,
        CancellationToken ct)
    {
        var dir = AppPaths.SaveDir(slotName);
        var random = new Random();

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var delaySec = random.Next(settings.BusyCaptureMinIntervalSeconds,
                    settings.BusyCaptureMaxIntervalSeconds + 1);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    if (random.Next(2) == 0)
                    {
                        var path = Path.Combine(dir, $"{filePrefix}_{timestamp}.png");
                        await WebcamPreviewService.CaptureFrameStandaloneAsync(path, ct, cameraIndex);
                    }
                    else
                    {
                        var duration = random.Next(3, 11);
                        var path = Path.Combine(dir, $"{filePrefix}_{timestamp}.avi");
                        await WebcamPreviewService.RecordVideoStandaloneAsync(path, duration, ct: ct,
                            cameraIndex: cameraIndex);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    /* Webcam unavailable — ignore */
                }
            }
        }, ct);
    }
}
