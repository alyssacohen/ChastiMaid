using System.Windows.Input;
using Avalonia.Threading;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class CornertimePunishmentViewModel : PunishmentExecutionViewModelBase
{
    private readonly GameSettings _settings;

    private readonly int _requiredSeconds;
    private int _maxSeconds;
    private CancellationTokenSource? _monitorCts;
    private DispatcherTimer? _tickTimer;

    [Reactive] private bool _isFraming = true;
    [Reactive] private bool _isActive;
    [Reactive] private bool _isResult;
    [Reactive] private int _elapsedSeconds;
    [Reactive] private string _resultMessage = "";

    public CornertimePunishmentViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, ActivePunishment punishment)
        : base(mainVm, saveRepo, punishment)
    {
        _settings = saveRepo.GetGameSettings();
        _requiredSeconds = punishment.CornertimeRequiredSeconds ?? 0;
        _maxSeconds = punishment.CornertimeMaxSeconds ?? _requiredSeconds;

        MainVm.Webcam.Start(_settings.SelectedCameraIndex);

        ConfirmFramingCommand = ReactiveCommand.Create(StartActive);
        AbandonCommand = ReactiveCommand.Create(() => Finish(success: false));
    }

    public WebcamPreviewService Webcam => MainVm.Webcam;
    public string RequiredDisplay => FormatSeconds(_requiredSeconds);
    public string ElapsedDisplay => FormatSeconds(ElapsedSeconds);

    public ICommand ConfirmFramingCommand { get; }
    public ICommand AbandonCommand { get; }

    private static string FormatSeconds(int seconds)
    {
        return $"{seconds / 60:D2}:{seconds % 60:D2}";
    }

    private void StartActive()
    {
        IsFraming = false;
        IsActive = true;
        Webcam.ResetMovementBaseline();

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) =>
        {
            ElapsedSeconds++;
            this.RaisePropertyChanged(nameof(ElapsedDisplay));

            if (ElapsedSeconds >= _requiredSeconds)
            {
                Finish(success: true);
            }
            else if (ElapsedSeconds >= _maxSeconds)
            {
                Finish(success: false);
            }
        };
        _tickTimer.Start();

        _monitorCts = new CancellationTokenSource();
        var ct = _monitorCts.Token;
        var slotName = SaveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.CornertimeCheckIntervalSeconds), ct);
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var path = Path.Combine(dir, $"cornertime_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                    var moved = await Webcam.CaptureAndDetectMovementAsync(
                        path, _settings.CornertimeMovementSensitivityPercent, ct);
                    if (moved)
                    {
                        _maxSeconds += _settings.CornertimeMovementPenaltySeconds;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    /* Webcam unavailable mid-session — ignore */
                }
            }
        }, ct);
    }

    private void Finish(bool success)
    {
        _tickTimer?.Stop();
        _tickTimer = null;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
        Webcam.Stop();

        ResultMessage = Conclude(success, "CornertimeSuccessMessage", "CornertimeFailureMessage");
        IsActive = false;
        IsResult = true;
    }
}
