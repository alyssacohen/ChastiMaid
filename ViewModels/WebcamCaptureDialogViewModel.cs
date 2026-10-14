using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Services;
using FluentIcons.Common;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public enum WebcamCaptureMode
{
    Photo,
    Video,
}

/// <summary>
///     Reusable webcam capture dialog: preview → capture (photo) or record (video) → result.
///     Await Result to get the saved file path, or null if the user skipped / no webcam was available.
/// </summary>
public partial class WebcamCaptureDialogViewModel : ViewModelBase
{
    private readonly int _videoDurationSeconds;
    private readonly string _outputPath;
    private readonly TaskCompletionSource<string?> _tcs = new();

    [Reactive(SetModifier = AccessModifier.Private)]
    private int _countdown;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isPreviewing;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isRecording;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _noWebcam;

    private ILocalizer L { get; }

    public WebcamCaptureDialogViewModel(WebcamPreviewService webcam, ILocalizer l, WebcamCaptureMode mode, string outputPath,
        string previewMessage, string noWebcamMessage, string? skipLabel = null, int videoDurationSeconds = 5,
        int cameraIndex = 0, bool pixelateCenterOverlay = false)
    {
        L = l;
        skipLabel ??= L.T("Skip");
        Webcam = webcam;
        Mode = mode;
        PreviewMessage = previewMessage;
        NoWebcamMessage = noWebcamMessage;
        SkipLabel = skipLabel;
        _outputPath = outputPath;
        _videoDurationSeconds = videoDurationSeconds;

        CaptureCommand = ReactiveCommand.CreateFromTask(Capture);
        SkipCommand = ReactiveCommand.Create(() => Finish(null));

        Webcam.PixelateCenterOverlay = pixelateCenterOverlay;

        if (Webcam.Start(cameraIndex))
        {
            IsPreviewing = true;
            _ = Webcam.RefreshAvailableCamerasAsync(cameraIndex);
        }
        else
        {
            NoWebcam = true;
        }
    }

    public WebcamPreviewService Webcam { get; }
    public WebcamCaptureMode Mode { get; }
    public string PreviewMessage { get; }
    public string NoWebcamMessage { get; }
    public string SkipLabel { get; }
    public string CaptureLabel => Mode == WebcamCaptureMode.Photo
        ? L.T("Capture")
        : L.T("Record (5s)");
    public Symbol CaptureIcon => Mode == WebcamCaptureMode.Photo ? Symbol.Camera : Symbol.Record;

    public ICommand CaptureCommand { get; }
    public ICommand SkipCommand { get; }

    public Task<string?> Result => _tcs.Task;

    private async Task Capture()
    {
        try
        {
            if (Mode == WebcamCaptureMode.Photo)
            {
                if (!await Webcam.CaptureFrameAsync(_outputPath))
                {
                    SetNoWebcam();
                    return;
                }
            }
            else
            {
                IsPreviewing = false;
                IsRecording = true;
                Countdown = _videoDurationSeconds;
                await Webcam.RecordVideoAsync(_outputPath, _videoDurationSeconds, new Progress<int>(r => Countdown = r));
            }
        }
        catch
        {
            SetNoWebcam();
            return;
        }

        Finish(_outputPath);
    }

    private void SetNoWebcam()
    {
        IsPreviewing = false;
        IsRecording = false;
        NoWebcam = true;
    }

    private void Finish(string? path)
    {
        Webcam.Stop();
        Webcam.PixelateCenterOverlay = false;
        _tcs.TrySetResult(path);
    }
}
