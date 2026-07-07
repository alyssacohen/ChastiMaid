using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenCvSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Size = OpenCvSharp.Size;

namespace ChastiMaid.Services;

/// <summary>
///     Manages the webcam: live preview on a WriteableBitmap plus video recording and photo capture
///     from the same open VideoCapture. Implements INotifyPropertyChanged via ReactiveObject.
/// </summary>
public partial class WebcamPreviewService : ReactiveObject, IDisposable
{
    private const int PixelateRegionWidth = 240;
    private const int PixelateRegionHeight = 80;
    private const int PixelateBlockSize = 20;

    private VideoCapture? _capture;

    /// <summary>
    ///     When true, a centered 240x80 region of the preview frame is heavily pixelated before display,
    ///     so the user can frame a shot (e.g. a safe combination) without being able to read it.
    ///     Only affects the live preview bitmap — frames captured via CaptureFrameAsync are read fresh
    ///     from the camera and are never pixelated.
    /// </summary>
    public bool PixelateCenterOverlay { get; set; }

    [Reactive(SetModifier = AccessModifier.Private)]
    private long _frameCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isRunning;

    [Reactive(SetModifier = AccessModifier.Private)]
    private WriteableBitmap? _previewBitmap;

    private CancellationTokenSource? _previewCts;
    private Task? _previewTask;

    public void Dispose()
    {
        Stop();
    }

    /// <summary>Opens the webcam and starts the preview loop. Returns false if unavailable.</summary>
    public bool Start(int cameraIndex = 0)
    {
        if (_capture is not null)
        {
            return true;
        }

        var cap = new VideoCapture(cameraIndex);
        if (!cap.IsOpened())
        {
            cap.Dispose();
            return false;
        }

        // ponytail: drop any stale bitmap from a prior session so the control shows
        // "no signal" instead of a leftover (possibly unpixelated) frame until the first fresh one arrives
        PreviewBitmap = null;

        _capture = cap;
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        _previewTask = Task.Run(() => PreviewLoop(ct), ct);
        IsRunning = true;
        return true;
    }

    private void PreviewLoop(CancellationToken ct)
    {
        using var frame = new Mat();
        while (!ct.IsCancellationRequested)
        {
            if (_capture is null || !_capture.IsOpened())
            {
                break;
            }

            _capture.Read(frame);
            if (frame.Empty())
            {
                Thread.Sleep(30);
                continue;
            }

            CopyFrameToBitmap(frame);
            Thread.Sleep(33); // ~30 fps
        }
    }

    private void CopyFrameToBitmap(Mat frame)
    {
        if (PixelateCenterOverlay)
        {
            PixelateCenter(frame);
        }

        // Convert BGR → BGRA and copy pixels into a byte[] on the current (background) thread
        using var bgra = new Mat();
        Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);

        var w = bgra.Width;
        var h = bgra.Height;
        var stride = w * 4;
        var size = stride * h;
        var pixels = new byte[size];
        Marshal.Copy(bgra.Data, pixels, 0, size);

        // Post only the byte[] to the UI thread — no Mat reference crosses the thread boundary
        Dispatcher.UIThread.Post(() =>
        {
            if (_previewBitmap is null || _previewBitmap.PixelSize.Width != w ||
                _previewBitmap.PixelSize.Height != h)
            {
                PreviewBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888);
            }

            using var fb = _previewBitmap!.Lock();
            Marshal.Copy(pixels, 0, fb.Address, size);
            FrameCount++;
        });
    }

    /// <summary>Heavily pixelates a centered region of the frame in place, in-frame only (preview never persisted).</summary>
    private static void PixelateCenter(Mat frame)
    {
        var w = Math.Min(PixelateRegionWidth, frame.Width);
        var h = Math.Min(PixelateRegionHeight, frame.Height);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var x = (frame.Width - w) / 2;
        var y = (frame.Height - h) / 2;

        using var roi = new Mat(frame, new OpenCvSharp.Rect(x, y, w, h));
        using var small = new Mat();
        var smallSize = new Size(Math.Max(1, w / PixelateBlockSize), Math.Max(1, h / PixelateBlockSize));

        Cv2.Resize(roi, small, smallSize, interpolation: InterpolationFlags.Linear);
        Cv2.Resize(small, roi, roi.Size(), interpolation: InterpolationFlags.Nearest);

        // Thin white guide border, baked in at the same coordinates as the pixelated region —
        // avoids mapping between the control's display size and the raw camera frame's resolution
        Cv2.Rectangle(frame, new OpenCvSharp.Rect(x, y, w, h), Scalar.White, thickness: 1);
    }

    /// <summary>Stops the preview loop and waits for it to finish. The VideoCapture stays open.</summary>
    private void StopPreviewLoop()
    {
        _previewCts?.Cancel();
        try
        {
            _previewTask?.Wait(1000);
        }
        catch
        {
            /* ignore */
        }

        _previewCts?.Dispose();
        _previewCts = null;
        _previewTask = null;
    }

    /// <summary>Restarts the preview loop (VideoCapture must already be open).</summary>
    private void StartPreviewLoop()
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return;
        }

        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewTask = Task.Run(() => PreviewLoop(ct), ct);
    }

    /// <summary>Records a video for the specified duration using the already-open VideoCapture.</summary>
    public async Task RecordVideoAsync(string outputPath, int durationSeconds, IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (_capture is null || !_capture.IsOpened())
        {
            throw new InvalidOperationException("Webcam not started.");
        }

        // Stop the preview loop for exclusive access to the VideoCapture
        StopPreviewLoop();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            await Task.Run(() =>
            {
                var fps = _capture.Fps > 0 ? _capture.Fps : 30.0;
                var width = _capture.FrameWidth;
                var height = _capture.FrameHeight;

                using var writer = new VideoWriter(outputPath, FourCC.MJPG, fps, new Size(width, height));
                using var frame = new Mat();

                var totalFrames = (int)(fps * durationSeconds);
                var delay = (int)(1000.0 / fps);

                for (var i = 0; i < totalFrames; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    _capture.Read(frame);
                    if (frame.Empty())
                    {
                        continue;
                    }

                    writer.Write(frame);

                    // Keep the preview updated during recording
                    CopyFrameToBitmap(frame);

                    var elapsed = (int)(i / fps);
                    progress?.Report(durationSeconds - elapsed);

                    Thread.Sleep(delay);
                }
            }, ct);
        }
        finally
        {
            StartPreviewLoop();
        }
    }

    /// <summary>Captures a single frame and saves it as an image.</summary>
    public async Task<bool> CaptureFrameAsync(string outputPath, CancellationToken ct = default)
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return false;
        }

        // Stop the preview loop for exclusive access
        StopPreviewLoop();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            return await Task.Run(() =>
            {
                using var frame = new Mat();
                _capture.Read(frame);
                if (frame.Empty())
                {
                    return false;
                }

                Cv2.ImWrite(outputPath, frame);
                return true;
            }, ct);
        }
        finally
        {
            StartPreviewLoop();
        }
    }

    /// <summary>Stops the preview and releases the webcam.</summary>
    public void Stop()
    {
        StopPreviewLoop();

        _capture?.Dispose();
        _capture = null;

        IsRunning = false;
    }

    /// <summary>Checks whether a webcam is available.</summary>
    public static bool DetectWebcam(int cameraIndex = 0)
    {
        using var cap = new VideoCapture(cameraIndex);
        return cap.IsOpened();
    }

    /// <summary>Probes camera indices 0..maxProbe-1 and returns the ones that open successfully.</summary>
    public static List<int> EnumerateCameras(int maxProbe = 5)
    {
        var found = new List<int>();
        for (var i = 0; i < maxProbe; i++)
        {
            using var cap = new VideoCapture(i);
            if (cap.IsOpened())
            {
                found.Add(i);
            }
        }

        return found;
    }

    /// <summary>
    ///     Opens its own VideoCapture (independent of any running preview) and records a video from the webcam
    ///     for the specified duration. Saves in AVI (MJPG) format at the given path.
    /// </summary>
    public static async Task RecordVideoStandaloneAsync(
        string outputPath,
        int durationSeconds,
        IProgress<int>? progress = null,
        CancellationToken ct = default,
        int cameraIndex = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await Task.Run(() =>
        {
            using var cap = new VideoCapture(cameraIndex);
            if (!cap.IsOpened())
            {
                throw new InvalidOperationException("Webcam not available.");
            }

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
                if (frame.Empty())
                {
                    continue;
                }

                writer.Write(frame);

                var elapsed = (int)(i / fps);
                progress?.Report(durationSeconds - elapsed);

                Thread.Sleep(delay);
            }
        }, ct);
    }

    /// <summary>
    ///     Opens its own VideoCapture (independent of any running preview) and captures a single frame,
    ///     saving it as an image.
    /// </summary>
    public static async Task<bool> CaptureFrameStandaloneAsync(string outputPath, CancellationToken ct = default,
        int cameraIndex = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        return await Task.Run(() =>
        {
            using var cap = new VideoCapture(cameraIndex);
            if (!cap.IsOpened())
            {
                return false;
            }

            using var frame = new Mat();
            cap.Read(frame);
            if (frame.Empty())
            {
                return false;
            }

            Cv2.ImWrite(outputPath, frame);
            return true;
        }, ct);
    }
}
