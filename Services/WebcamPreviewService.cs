using System.Runtime.InteropServices;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenCvSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Windows.Devices.Enumeration;
using Size = OpenCvSharp.Size;

namespace ChastiMaid.Services;

/// <summary>
///     Manages the webcam: live preview on a WriteableBitmap plus video recording and photo capture
///     from the same open VideoCapture. Photos and videos keep the camera's full frame, resolution and
///     orientation; only the preview is shrunk. Implements INotifyPropertyChanged via ReactiveObject.
/// </summary>
public partial class WebcamPreviewService : ReactiveObject, IDisposable
{
    // Same share of the visible picture as the old 240×80 box over the 320-px-wide preview crop.
    private const double PixelateWidthRatio = 0.75;
    private const double PixelateHeightRatio = 1.0 / 6;
    private const int PixelateBlocksAcross = 12;

    /// <summary>Longest side of the preview frames: about the largest box the preview gets on the Surface Go.</summary>
    private const int PreviewMaxSide = 960;

    /// <summary>Longest side movement detection works at: the old default 640×480 the sensitivity was tuned on.</summary>
    private const int MovementAnalysisMaxSide = 640;

    private VideoCapture? _capture;
    private int _currentCameraIndex = -1;

    private const int BackgroundSubtractorHistory = 300;
    private const double BackgroundSubtractorVarThreshold = 16;
    private const int MovementWarmupFrames = 30; // ~1s at 30fps, lets the background model settle before evaluating

    private BackgroundSubtractorMOG2? _bgSubtractor;
    private int _bgSubtractorWarmupFramesRemaining;

    private bool _monitorMovement;
    private int _movementSensitivityPercent;
    private int _movementCooldownMinSeconds;
    private int _movementCooldownMaxSeconds;
    private TimeSpan _movementPenaltyCooldown;
    private string? _movementEvidenceDir;
    private DateTime? _lastPenaltyAt;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int _movementPenaltyCount;

    /// <summary>
    ///     When true, a centered strip of the preview frame is heavily pixelated before display,
    ///     so the user can frame a shot (e.g. a safe combination) without being able to read it.
    ///     Only affects the shrunk preview copy — captured photos and videos are never pixelated.
    /// </summary>
    public bool PixelateCenterOverlay { get; set; }

    [Reactive(SetModifier = AccessModifier.Private)]
    private long _frameCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isRunning;

    [Reactive(SetModifier = AccessModifier.Private)]
    private WriteableBitmap? _previewBitmap;

    private IReadOnlyList<CameraOption> _availableCameras = [];

    /// <summary>Cameras detected via <see cref="RefreshAvailableCamerasAsync" />, for a live camera-selector dropdown.</summary>
    public IReadOnlyList<CameraOption> AvailableCameras
    {
        get => _availableCameras;
        private set
        {
            this.RaiseAndSetIfChanged(ref _availableCameras, value);
            this.RaisePropertyChanged(nameof(HasMultipleCameras));
        }
    }

    private CameraOption? _selectedCamera;

    /// <summary>
    ///     The camera every capture uses: the last one the user picked, from any selector. Seeded from the
    ///     profile at startup; <see cref="PreferredCameraChanged" /> lets the owner persist new picks.
    /// </summary>
    public int PreferredCameraIndex { get; set; }

    /// <summary>Raised when the user picks a different camera in a live selector.</summary>
    public event Action<int>? PreferredCameraChanged;

    /// <summary>
    ///     The camera chosen in the live selector. A user pick becomes the <see cref="PreferredCameraIndex" />
    ///     and, while the preview runs on another camera, restarts it on the new one.
    /// </summary>
    public CameraOption? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCamera, value);
            if (value is null)
            {
                return;
            }

            if (value.Index != PreferredCameraIndex)
            {
                PreferredCameraIndex = value.Index;
                PreferredCameraChanged?.Invoke(value.Index);
            }

            if (value.Index != _currentCameraIndex && IsRunning)
            {
                Stop();
                Start(value.Index);
            }
        }
    }

    public bool HasMultipleCameras => AvailableCameras.Count > 1;

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

        var cap = OpenCamera(cameraIndex);
        if (!cap.IsOpened())
        {
            cap.Dispose();
            return false;
        }

        // ponytail: drop any stale bitmap from a prior session so the control shows
        // "no signal" instead of a leftover (possibly unpixelated) frame until the first fresh one arrives
        PreviewBitmap = null;

        _currentCameraIndex = cameraIndex;
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

            if (_monitorMovement)
            {
                CheckMovement(frame);
            }

            CopyFrameToBitmap(frame);
            Thread.Sleep(33); // ~30 fps
        }
    }

    /// <summary>
    ///     Feeds <paramref name="frame" /> into a running MOG2 background model on every tick of
    ///     <see cref="PreviewLoop" />, so movement is caught continuously instead of only between periodic
    ///     snapshots. A plain two-frame difference is unusable here: adjacent frames are only ~33ms apart, so
    ///     genuine movement shifts far too few pixels between them to ever cross a sensitivity threshold tuned
    ///     for far-apart snapshots. Background subtraction instead compares each frame against a continuously
    ///     learned background, so the foreground ratio stays meaningful regardless of how often frames arrive.
    ///     When the foreground-pixel ratio exceeds <see cref="_movementSensitivityPercent" /> and at least
    ///     <see cref="_movementPenaltyCooldown" /> has passed since the last penalty, saves the frame as evidence
    ///     and bumps <see cref="MovementPenaltyCount" />.
    /// </summary>
    private void CheckMovement(Mat frame)
    {
        if (_bgSubtractor is null)
        {
            return;
        }

        using var small = Shrink(frame, MovementAnalysisMaxSide);
        using var fgMask = new Mat();
        _bgSubtractor.Apply(small, fgMask);

        if (_bgSubtractorWarmupFramesRemaining > 0)
        {
            // Let the background model settle on the initial "still" pose first, otherwise the
            // very first frames are entirely foreground and immediately (falsely) trigger a penalty.
            _bgSubtractorWarmupFramesRemaining--;
            return;
        }

        var changedRatio = Cv2.CountNonZero(fgMask) / (double)(fgMask.Width * fgMask.Height);
        var moved = changedRatio * 100 > _movementSensitivityPercent;

        if (moved && (_lastPenaltyAt is null || DateTime.Now - _lastPenaltyAt >= _movementPenaltyCooldown))
        {
            _lastPenaltyAt = DateTime.Now;
            _movementPenaltyCooldown = TimeSpan.FromSeconds(
                Random.Shared.Next(_movementCooldownMinSeconds, _movementCooldownMaxSeconds + 1));

            if (_movementEvidenceDir is not null)
            {
                Directory.CreateDirectory(_movementEvidenceDir);
                var path = Path.Combine(_movementEvidenceDir, $"cornertime_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                Cv2.ImWrite(path, frame);
            }

            Dispatcher.UIThread.Post(() => MovementPenaltyCount++);
        }
    }

    /// <summary>
    ///     Starts continuous movement monitoring inside the running preview loop, backed by a fresh MOG2
    ///     background-subtraction model. Call <see cref="ResetMovementBaseline" /> beforehand so a stale
    ///     model from an unrelated session isn't reused.
    /// </summary>
    public void StartMovementMonitoring(int sensitivityPercent, int cooldownMinSeconds, int cooldownMaxSeconds,
        string evidenceDir)
    {
        _movementSensitivityPercent = sensitivityPercent;
        _movementCooldownMinSeconds = cooldownMinSeconds;
        _movementCooldownMaxSeconds = cooldownMaxSeconds;
        _movementPenaltyCooldown = TimeSpan.FromSeconds(Random.Shared.Next(cooldownMinSeconds, cooldownMaxSeconds + 1));
        _movementEvidenceDir = evidenceDir;
        _lastPenaltyAt = null;

        _bgSubtractor?.Dispose();
        _bgSubtractor = BackgroundSubtractorMOG2.Create(
            BackgroundSubtractorHistory, BackgroundSubtractorVarThreshold, detectShadows: false);
        _bgSubtractorWarmupFramesRemaining = MovementWarmupFrames;

        _monitorMovement = true;
    }

    /// <summary>Stops continuous movement monitoring.</summary>
    public void StopMovementMonitoring()
    {
        _monitorMovement = false;
        _lastPenaltyAt = null;
    }

    private void CopyFrameToBitmap(Mat frame)
    {
        using var preview = Shrink(frame, PreviewMaxSide);
        if (PixelateCenterOverlay)
        {
            PixelateCenter(preview);
        }

        // Convert BGR → BGRA and copy pixels into a byte[] on the current (background) thread
        using var bgra = new Mat();
        Cv2.CvtColor(preview, bgra, ColorConversionCodes.BGR2BGRA);

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
        var w = (int)(frame.Width * PixelateWidthRatio);
        var h = (int)(frame.Height * PixelateHeightRatio);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var x = (frame.Width - w) / 2;
        var y = (frame.Height - h) / 2;

        using var roi = new Mat(frame, new OpenCvSharp.Rect(x, y, w, h));
        using var small = new Mat();
        var blockSize = Math.Max(1, w / PixelateBlocksAcross);
        var smallSize = new Size(Math.Max(1, w / blockSize), Math.Max(1, h / blockSize));

        Cv2.Resize(roi, small, smallSize, interpolation: InterpolationFlags.Linear);
        Cv2.Resize(small, roi, roi.Size(), interpolation: InterpolationFlags.Nearest);

        // Thin white guide border, baked in at the same coordinates as the pixelated region —
        // avoids mapping between the control's display size and the raw camera frame's resolution
        Cv2.Rectangle(frame, new OpenCvSharp.Rect(x, y, w, h), Scalar.White, thickness: 1);
    }

    /// <summary>Returns a copy of the frame shrunk so its longer side is at most <paramref name="maxSide" /> (never enlarged).</summary>
    private static Mat Shrink(Mat frame, int maxSide)
    {
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(frame.Width, frame.Height));
        var result = new Mat();
        Cv2.Resize(frame, result, new Size(), scale, scale, InterpolationFlags.Area);
        return result;
    }

    /// <summary>
    ///     Opens a camera at its highest resolution: asking for more than any mode offers makes the driver
    ///     fall back to its largest one.
    /// </summary>
    private static VideoCapture OpenCamera(int cameraIndex)
    {
        var cap = new VideoCapture(cameraIndex);
        if (cap.IsOpened())
        {
            cap.Set(VideoCaptureProperties.FrameWidth, 10_000);
            cap.Set(VideoCaptureProperties.FrameHeight, 10_000);
        }

        return cap;
    }

    /// <summary>Records <paramref name="durationSeconds" /> of MJPG video at the frames' own size.</summary>
    private static void Record(VideoCapture cap, string outputPath, int durationSeconds, IProgress<int>? progress,
        Action<Mat>? onFrame, CancellationToken ct)
    {
        var fps = cap.Fps > 0 ? cap.Fps : 30.0;
        var totalFrames = (int)(fps * durationSeconds);
        var delay = (int)(1000.0 / fps);

        using var frame = new Mat();
        VideoWriter? writer = null;
        try
        {
            for (var i = 0; i < totalFrames; i++)
            {
                ct.ThrowIfCancellationRequested();
                cap.Read(frame);
                if (frame.Empty())
                {
                    continue;
                }

                // Sized from a real frame: a writer whose size differs from the frames silently drops them all.
                writer ??= new VideoWriter(outputPath, FourCC.MJPG, fps, frame.Size());
                writer.Write(frame);
                onFrame?.Invoke(frame);

                var elapsed = (int)(i / fps);
                progress?.Report(durationSeconds - elapsed);

                Thread.Sleep(delay);
            }
        }
        finally
        {
            writer?.Dispose();
        }

        if (writer is null)
        {
            throw new InvalidOperationException("The webcam delivered no frame.");
        }
    }

    /// <summary>Reads one frame and saves it as an image.</summary>
    private static bool SaveFrame(VideoCapture cap, string outputPath)
    {
        using var frame = new Mat();
        cap.Read(frame);
        return !frame.Empty() && Cv2.ImWrite(outputPath, frame);
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

        var cap = _capture;
        try
        {
            // The preview keeps updating during recording
            await Task.Run(() => Record(cap, outputPath, durationSeconds, progress, CopyFrameToBitmap, ct), ct);
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

        var cap = _capture;
        try
        {
            return await Task.Run(() => SaveFrame(cap, outputPath), ct);
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
        StopMovementMonitoring();

        _capture?.Dispose();
        _capture = null;

        IsRunning = false;
        ResetMovementBaseline();
    }

    /// <summary>Discards the movement-detection background model. Call before starting a new Cornertime session.</summary>
    public void ResetMovementBaseline()
    {
        _bgSubtractor?.Dispose();
        _bgSubtractor = null;
        _bgSubtractorWarmupFramesRemaining = 0;
        MovementPenaltyCount = 0;
    }

    /// <summary>Checks whether a webcam is available.</summary>
    public static bool DetectWebcam(int cameraIndex = 0)
    {
        using var cap = new VideoCapture(cameraIndex);
        return cap.IsOpened();
    }

    private static IReadOnlyList<string>? _cachedFriendlyNames;

    /// <summary>
    ///     Fetches the OS's video capture devices via the WinRT enumeration API (the same
    ///     <see cref="Windows.Devices.Enumeration.DeviceInformation" /> API used for microphone
    ///     detection) and caches their friendly names for the process lifetime. This is fast and
    ///     doesn't touch OpenCV, so it never triggers the
    ///     dshow/obsensor backend-enumeration warnings that probing with <see cref="VideoCapture" /> does.
    /// </summary>
    private static async Task<IReadOnlyList<string>> GetFriendlyNamesAsync()
    {
        if (_cachedFriendlyNames is not null)
        {
            return _cachedFriendlyNames;
        }

        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            return _cachedFriendlyNames = devices.Select(d => d.Name).ToList();
        }
        catch (Exception)
        {
            return _cachedFriendlyNames = [];
        }
    }

    private static List<int>? _cachedCameraIndices;

    /// <summary>
    ///     Returns the camera indices that actually open successfully. The number of indices to probe is
    ///     bounded by the WinRT device count (via <see cref="GetFriendlyNamesAsync()" />) instead of a
    ///     fixed guess, so a machine with e.g. one camera only opens <see cref="VideoCapture" /> once
    ///     instead of blindly trying indices with nothing attached (each such probe triggers OpenCV's
    ///     dshow/obsensor warnings and is noticeably slow). Falls back to probing <paramref name="maxProbe" />
    ///     indices if WinRT enumeration fails or reports zero devices. Cached for the process lifetime.
    /// </summary>
    public static List<int> EnumerateCameras(int maxProbe = 5)
    {
        if (_cachedCameraIndices is not null)
        {
            return _cachedCameraIndices;
        }

        var deviceCount = Task.Run(async () => (await GetFriendlyNamesAsync()).Count).GetAwaiter().GetResult();
        var probeCount = deviceCount > 0 ? deviceCount : maxProbe;

        var found = new List<int>();
        for (var i = 0; i < probeCount; i++)
        {
            using var cap = new VideoCapture(i);
            if (cap.IsOpened())
            {
                found.Add(i);
            }
        }

        return _cachedCameraIndices = found;
    }

    /// <summary>
    ///     Best-effort human-friendly names for the given camera indices. Names are matched positionally
    ///     against <paramref name="indices" /> — both APIs enumerate the OS's video capture devices, but
    ///     there is no formal guarantee their ordering lines up with OpenCV's dshow index order, so this is
    ///     a helpful hint rather than an authoritative mapping. Falls back to a generic "Camera {index}"
    ///     label per index if names can't be resolved.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GetFriendlyNamesAsync(IReadOnlyList<int> indices)
    {
        var names = await GetFriendlyNamesAsync();
        return indices.Select((idx, i) => i < names.Count ? names[i] : $"Camera {idx}").ToList();
    }

    /// <summary>
    ///     Probes cameras off the UI thread and populates <see cref="AvailableCameras" />/<see cref="SelectedCamera" />
    ///     for a live camera-selector dropdown next to a preview. <paramref name="currentIndex" /> is the
    ///     camera the preview is currently (or about to be) opened on.
    /// </summary>
    public async Task RefreshAvailableCamerasAsync(int currentIndex)
    {
        var indices = await Task.Run(() => EnumerateCameras());
        if (indices.Count == 0)
        {
            indices = [currentIndex];
        }

        var names = await GetFriendlyNamesAsync(indices);
        var options = indices.Zip(names, (idx, name) => new CameraOption(idx, name)).ToList();

        Dispatcher.UIThread.Post(() =>
        {
            AvailableCameras = options;
            // Field, not the setter: showing the fallback when the preferred camera is missing is not a pick.
            this.RaiseAndSetIfChanged(ref _selectedCamera,
                options.FirstOrDefault(c => c.Index == currentIndex) ?? options[0], nameof(SelectedCamera));
        });
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
            using var cap = OpenCamera(cameraIndex);
            if (!cap.IsOpened())
            {
                throw new InvalidOperationException("Webcam not available.");
            }

            Record(cap, outputPath, durationSeconds, progress, null, ct);
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
            using var cap = OpenCamera(cameraIndex);
            return cap.IsOpened() && SaveFrame(cap, outputPath);
        }, ct);
    }
}

/// <summary>
///     A camera choice for a live preview's camera-selector dropdown. Equality is based on
///     <see cref="Index" /> only, so resolving a friendlier <see cref="DisplayName" /> asynchronously
///     doesn't spuriously trigger unrelated change handlers.
/// </summary>
public record CameraOption(int Index, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }

    public virtual bool Equals(CameraOption? other)
    {
        return other is not null && Index == other.Index;
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode();
    }
}
