using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class CornertimePunishmentViewModel : PunishmentExecutionViewModelBase
{
    private readonly GameSettings _settings;

    private int _requiredSeconds;
    private readonly int _maxSeconds;
    private IDisposable? _movementSubscription;
    private DispatcherTimer? _tickTimer;
    private DispatcherTimer? _positioningTimer;
    private DispatcherTimer? _narrationTimer;
    private CancellationTokenSource? _lifecycleCts;
    private int _lastNarratedPenaltyCount;

    [Reactive] private bool _isFraming = true;
    [Reactive] private bool _isActive;
    [Reactive] private bool _isResult;
    [Reactive] private int _elapsedSeconds;
    [Reactive] private string _resultMessage = "";
    [Reactive] private string _narrationText = "";

    public CornertimePunishmentViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, ActivePunishment punishment)
        : base(mainVm, saveRepo, punishment)
    {
        _settings = saveRepo.GetGameSettings();
        _requiredSeconds = punishment.CornertimeRequiredSeconds ?? 0;
        _maxSeconds = punishment.CornertimeMaxSeconds ?? _requiredSeconds;

        MainVm.Webcam.Start(_settings.SelectedCameraIndex);

        ConfirmFramingCommand = ReactiveCommand.Create(BeginPositioning);
        AbandonCommand = ReactiveCommand.Create(() => Finish(success: false, abandoned: true));
    }

    public WebcamPreviewService Webcam => MainVm.Webcam;
    public string RequiredDisplay => FormatSeconds(_requiredSeconds);
    public string ElapsedDisplay => FormatSeconds(ElapsedSeconds);
    public string MaxDisplay => FormatSeconds(_maxSeconds);

    public ICommand ConfirmFramingCommand { get; }
    public ICommand AbandonCommand { get; }

    private static string FormatSeconds(int seconds)
    {
        return $"{seconds / 60:D2}:{seconds % 60:D2}";
    }

    private void BeginPositioning()
    {
        IsFraming = false;
        IsActive = true;
        Webcam.ResetMovementBaseline();

        _lifecycleCts = new CancellationTokenSource();

        NarrationText = Resources.Get(CornertimeInstructionBank.GetRandomPosition());
        Speak(NarrationText);

        _positioningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _positioningTimer.Tick += (_, _) =>
        {
            _positioningTimer!.Stop();
            _positioningTimer = null;
            BeginCountdown();
        };
        _positioningTimer.Start();
    }

    private void BeginCountdown()
    {
        NarrationText = Resources.Get(CornertimeInstructionBank.GetRandomBegin());
        Speak(NarrationText);

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

        var slotName = SaveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);

        _movementSubscription = Webcam.WhenAnyValue(x => x.MovementPenaltyCount)
            .Skip(1)
            .Subscribe(_ =>
            {
                var added = Random.Shared.Next(
                    _settings.CornertimeMovementPenaltyMinSeconds,
                    _settings.CornertimeMovementPenaltyMaxSeconds + 1);
                _requiredSeconds += added;
                this.RaisePropertyChanged(nameof(RequiredDisplay));

                NarrationText = string.Format(Resources.Get(CornertimeInstructionBank.GetRandomMovementDetected()), added);
                Speak(NarrationText);
                _lastNarratedPenaltyCount = Webcam.MovementPenaltyCount;
            });

        Webcam.StartMovementMonitoring(
            _settings.CornertimeMovementSensitivityPercent,
            _settings.CornertimeMovementCooldownMinSeconds,
            _settings.CornertimeMovementCooldownMaxSeconds,
            dir);

        _lastNarratedPenaltyCount = 0;
        ScheduleNextNarrationTick();
    }

    /// <summary>
    ///     Ambient "you're still fine, don't get comfortable" narration, on its own randomized
    ///     10-30s schedule independent of the real movement-detection cooldown in
    ///     <see cref="WebcamPreviewService" />. Only speaks when no penalty was registered since the
    ///     last tick — a caught movement already gets its own line from the subscription above, so
    ///     this avoids narrating the same event twice.
    /// </summary>
    private void ScheduleNextNarrationTick()
    {
        var delay = TimeSpan.FromSeconds(Random.Shared.Next(
            _settings.CornertimeMovementCooldownMinSeconds,
            _settings.CornertimeMovementCooldownMaxSeconds + 1));

        _narrationTimer = new DispatcherTimer { Interval = delay };
        _narrationTimer.Tick += (_, _) =>
        {
            _narrationTimer!.Stop();

            if (Webcam.MovementPenaltyCount == _lastNarratedPenaltyCount)
            {
                NarrationText = Resources.Get(CornertimeInstructionBank.GetRandomStillnessWarning());
                Speak(NarrationText);
            }

            ScheduleNextNarrationTick();
        };
        _narrationTimer.Start();
    }

    private void Speak(string text)
    {
        if (_settings.CornertimeVoiceEnabled && _lifecycleCts is { } cts)
        {
            _ = SpeechSynthesisService.SpeakAsync(text, cts.Token);
        }
    }

    private void Finish(bool success, bool abandoned = false)
    {
        _positioningTimer?.Stop();
        _positioningTimer = null;
        _narrationTimer?.Stop();
        _narrationTimer = null;
        _tickTimer?.Stop();
        _tickTimer = null;
        _movementSubscription?.Dispose();
        _movementSubscription = null;
        _lifecycleCts?.Cancel();
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;

        // Read before Stop() — Stop() → ResetMovementBaseline() zeroes the count.
        var penaltyCount = Webcam.MovementPenaltyCount;
        Webcam.StopMovementMonitoring();
        Webcam.Stop();

        var escalationSeverity = success ? 0 : penaltyCount + (abandoned ? 3 : 0);
        var failureKey = escalationSeverity > 0 ? "CornertimeFailureWithConsequenceMessage" : "CornertimeFailureMessage";

        if (escalationSeverity > 0)
        {
            SaveRepo.AssignPunishment((escalationSeverity, escalationSeverity), "PunishmentReasonCornertimeFailed");
        }

        ResultMessage = Conclude(success, "CornertimeSuccessMessage", failureKey);
        IsActive = false;
        IsResult = true;
        NotificationSoundService.PlayChime();
    }
}
