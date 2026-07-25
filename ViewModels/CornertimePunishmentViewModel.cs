using System.Reactive.Linq;
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

    private int _requiredSeconds;
    private readonly int _maxSeconds;
    private IDisposable? _movementSubscription;
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

        var slotName = SaveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);

        _movementSubscription = Webcam.WhenAnyValue(x => x.MovementPenaltyCount)
            .Skip(1)
            .Subscribe(_ =>
            {
                _requiredSeconds += Random.Shared.Next(
                    _settings.CornertimeMovementPenaltyMinSeconds,
                    _settings.CornertimeMovementPenaltyMaxSeconds + 1);
                this.RaisePropertyChanged(nameof(RequiredDisplay));
            });

        Webcam.StartMovementMonitoring(
            _settings.CornertimeMovementSensitivityPercent,
            _settings.CornertimeMovementCooldownMinSeconds,
            _settings.CornertimeMovementCooldownMaxSeconds,
            dir);
    }

    private void Finish(bool success, bool abandoned = false)
    {
        _tickTimer?.Stop();
        _tickTimer = null;
        _movementSubscription?.Dispose();
        _movementSubscription = null;

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
    }
}
