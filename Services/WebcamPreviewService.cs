using Avalonia;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using ReactiveUI;

namespace ChastiMaid.Services;

/// <summary>
/// Manages the webcam: live preview on a WriteableBitmap plus video recording and photo capture
/// from the same open VideoCapture. Implements INotifyPropertyChanged via ReactiveObject.
/// </summary>
public class WebcamPreviewService : ReactiveObject, IDisposable
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _previewCts;
    private Task? _previewTask;

    private WriteableBitmap? _previewBitmap;
    public WriteableBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set => this.RaiseAndSetIfChanged(ref _previewBitmap, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    private long _frameCount;
    public long FrameCount
    {
        get => _frameCount;
        private set => this.RaiseAndSetIfChanged(ref _frameCount, value);
    }

    /// <summary>Opens the webcam and starts the preview loop. Returns false if unavailable.</summary>
    public bool Start()
    {
        if (_capture is not null) return true;

        var cap = new VideoCapture(0);
        if (!cap.IsOpened()) { cap.Dispose(); return false; }

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
            if (_capture is null || !_capture.IsOpened()) break;
            _capture.Read(frame);
            if (frame.Empty()) { Thread.Sleep(30); continue; }

            CopyFrameToBitmap(frame);
            Thread.Sleep(33); // ~30 fps
        }
    }

    private void CopyFrameToBitmap(Mat frame)
    {
        // Convert BGR → BGRA and copy pixels into a byte[] on the current (background) thread
        using var bgra = new Mat();
        Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);

        var w = bgra.Width;
        var h = bgra.Height;
        var stride = w * 4;
        var size = stride * h;
        var pixels = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(bgra.Data, pixels, 0, size);

        // Post only the byte[] to the UI thread — no Mat reference crosses the thread boundary
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_previewBitmap is null || _previewBitmap.PixelSize.Width != w || _previewBitmap.PixelSize.Height != h)
                PreviewBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);

            using var fb = _previewBitmap!.Lock();
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, size);
            FrameCount++;
        });
    }

    /// <summary>Stops the preview loop and waits for it to finish. The VideoCapture stays open.</summary>
    private void StopPreviewLoop()
    {
        _previewCts?.Cancel();
        try { _previewTask?.Wait(1000); } catch { /* ignore */ }
        _previewCts?.Dispose();
        _previewCts = null;
        _previewTask = null;
    }

    /// <summary>Restarts the preview loop (VideoCapture must already be open).</summary>
    private void StartPreviewLoop()
    {
        if (_capture is null || !_capture.IsOpened()) return;
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewTask = Task.Run(() => PreviewLoop(ct), ct);
    }

    /// <summary>Records a video for the specified duration using the already-open VideoCapture.</summary>
    public async Task RecordVideoAsync(string outputPath, int durationSeconds, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (_capture is null || !_capture.IsOpened())
            throw new InvalidOperationException("Webcam not started.");

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

                using var writer = new VideoWriter(outputPath, FourCC.MJPG, fps, new OpenCvSharp.Size(width, height));
                using var frame = new Mat();

                var totalFrames = (int)(fps * durationSeconds);
                var delay = (int)(1000.0 / fps);

                for (var i = 0; i < totalFrames; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    _capture.Read(frame);
                    if (frame.Empty()) continue;
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
        if (_capture is null || !_capture.IsOpened()) return false;

        // Stop the preview loop for exclusive access
        StopPreviewLoop();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            return await Task.Run(() =>
            {
                using var frame = new Mat();
                _capture.Read(frame);
                if (frame.Empty()) return false;
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

    public void Dispose() => Stop();
}
