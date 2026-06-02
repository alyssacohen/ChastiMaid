using OpenCvSharp;

namespace ChastiMaid.Services;

public class MediaCaptureService
{
    /// <summary>Checks whether a webcam is available.</summary>
    public static bool DetectWebcam()
    {
        using var cap = new VideoCapture(0);
        return cap.IsOpened();
    }

    /// <summary>
    /// Records a video from the webcam for the specified duration.
    /// Saves in AVI (MJPG) format at the given path.
    /// </summary>
    public static async Task RecordVideoAsync(
        string outputPath,
        int durationSeconds,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await Task.Run(() =>
        {
            using var cap = new VideoCapture(0);
            if (!cap.IsOpened()) throw new InvalidOperationException("Webcam not available.");

            var fps = cap.Fps > 0 ? cap.Fps : 30.0;
            var width = cap.FrameWidth;
            var height = cap.FrameHeight;
            var fourcc = FourCC.MJPG;

            using var writer = new VideoWriter(outputPath, fourcc, fps, new Size(width, height));
            using var frame = new Mat();

            var totalFrames = (int)(fps * durationSeconds);
            var delay = (int)(1000.0 / fps);

            for (var i = 0; i < totalFrames; i++)
            {
                ct.ThrowIfCancellationRequested();
                cap.Read(frame);
                if (frame.Empty()) continue;
                writer.Write(frame);

                var elapsed = (int)(i / fps);
                progress?.Report(durationSeconds - elapsed);

                Thread.Sleep(delay);
            }
        }, ct);
    }

    /// <summary>Captures a single frame from the webcam and saves it as an image.</summary>
    public static async Task<bool> CaptureFrameAsync(string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        return await Task.Run(() =>
        {
            using var cap = new VideoCapture(0);
            if (!cap.IsOpened()) return false;
            using var frame = new Mat();
            cap.Read(frame);
            if (frame.Empty()) return false;
            Cv2.ImWrite(outputPath, frame);
            return true;
        }, ct);
    }
}
