using Avalonia;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using ReactiveUI;

namespace ChastiMaid.Services;

/// <summary>
/// Gestisce la webcam: preview live su WriteableBitmap + registrazione video / cattura foto
/// dalla stessa VideoCapture aperta. Implementa INotifyPropertyChanged via ReactiveObject.
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

    /// <summary>Apre la webcam e avvia il loop di preview. Ritorna false se non disponibile.</summary>
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
        // Converti BGR → BGRA e copia i pixel in un byte[] sul thread corrente (background)
        using var bgra = new Mat();
        Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);

        var w = bgra.Width;
        var h = bgra.Height;
        var stride = w * 4;
        var size = stride * h;
        var pixels = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(bgra.Data, pixels, 0, size);

        // Posta solo il byte[] al UI thread — nessun riferimento a Mat
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_previewBitmap is null || _previewBitmap.PixelSize.Width != w || _previewBitmap.PixelSize.Height != h)
                PreviewBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);

            using var fb = _previewBitmap!.Lock();
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, size);
            FrameCount++;
        });
    }

    /// <summary>Ferma il preview loop e attende che si chiuda. La VideoCapture resta aperta.</summary>
    private void StopPreviewLoop()
    {
        _previewCts?.Cancel();
        try { _previewTask?.Wait(1000); } catch { /* ignore */ }
        _previewCts?.Dispose();
        _previewCts = null;
        _previewTask = null;
    }

    /// <summary>Riavvia il preview loop (la VideoCapture deve essere già aperta).</summary>
    private void StartPreviewLoop()
    {
        if (_capture is null || !_capture.IsOpened()) return;
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _previewTask = Task.Run(() => PreviewLoop(ct), ct);
    }

    /// <summary>Registra video per la durata specificata dalla webcam già aperta.</summary>
    public async Task RecordVideoAsync(string outputPath, int durationSeconds, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (_capture is null || !_capture.IsOpened())
            throw new InvalidOperationException("Webcam not started.");

        // Ferma il preview loop per avere accesso esclusivo alla VideoCapture
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

                    // Aggiorna il preview anche durante la registrazione
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

    /// <summary>Cattura un singolo frame e lo salva come immagine.</summary>
    public async Task<bool> CaptureFrameAsync(string outputPath, CancellationToken ct = default)
    {
        if (_capture is null || !_capture.IsOpened()) return false;

        // Ferma il preview loop per avere accesso esclusivo
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

    /// <summary>Ferma il preview e rilascia la webcam.</summary>
    public void Stop()
    {
        StopPreviewLoop();

        _capture?.Dispose();
        _capture = null;

        IsRunning = false;
    }

    public void Dispose() => Stop();
}
