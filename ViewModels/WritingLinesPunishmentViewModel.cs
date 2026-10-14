using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class WritingLinesPunishmentViewModel : PunishmentExecutionViewModelBase
{
    private static readonly string[] Interruptions =
    [
        Localizer.Mark("Do keep those fingers moving."),
        Localizer.Mark("Errors are so unbecoming."),
        Localizer.Mark("The ink dries whether you write or not."),
        Localizer.Mark("I have all evening. Do you?"),
        Localizer.Mark("A steady hand betrays a steady mind."),
        Localizer.Mark("Dawdling adds nothing but time."),
    ];

    private const int InterruptionDisplayMilliseconds = 2000;

    private readonly GameSettings _settings;
    private readonly int _penaltyLinesPerMistake;
    private readonly List<string> _lineSequence = [];
    private CancellationTokenSource? _captureCts;
    private CancellationTokenSource? _interruptionCts;
    private DispatcherTimer? _statsTimer;
    private DispatcherTimer? _mistakeLockTimer;
    private DispatcherTimer? _noIdleTimer;
    private DispatcherTimer? _interruptionHideTimer;
    private DateTime? _startedAt;
    private int _totalKeystrokes;
    private int _correctKeystrokes;
    private bool _inputLocked;

    [Reactive] private int _totalLines;
    [Reactive] private int _completedLines;
    [Reactive] private int _mistakeCount;
    [Reactive] private string _typedText = "";
    [Reactive] private bool _isResult;
    [Reactive] private string _resultMessage = "";
    [Reactive] private bool _isInterruptionVisible;
    [Reactive] private string _interruptionMessage = "";

    public WritingLinesPunishmentViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, ActivePunishment punishment)
        : base(mainVm, saveRepo, punishment)
    {
        _settings = saveRepo.GetGameSettings();
        _penaltyLinesPerMistake = _settings.WritingLinesPenaltyLinesPerMistake;
        _totalLines = punishment.WritingLinesTotalLines ?? 1;
        EnsureLineSequenceLength(_totalLines);

        Punishment.WritingLinesAttemptActive = true;
        SaveRepo.SaveActivePunishment(Punishment);

        this.WhenAnyValue(x => x.MistakeCount).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(HasMistakes));
            this.RaisePropertyChanged(nameof(MistakeCountText));
        });

        AbandonCommand = ReactiveCommand.Create(() => Finish(success: false));

        StartSilentCaptureIfAvailable();
        StartInterruptionLoopIfActive();
    }

    public string SentenceText => _lineSequence[CompletedLines];
    public bool HasMistakes => MistakeCount > 0;

    public string MistakeCountText =>
        L.N("{0} mistake — lines added", "{0} mistakes — lines added", MistakeCount, MistakeCount);
    public ObservableCollection<WritingLineHistoryEntry> History { get; } = [];

    public string ProgressPercentText => TotalLines > 0 ? $"{(int)Math.Round(100.0 * CompletedLines / TotalLines)}%" : "0%";
    public string ProgressHintText => L.N("{0}/{1} line", "{0}/{1} lines", TotalLines, CompletedLines, TotalLines);
    public string CpmText => ElapsedSeconds > 0 ? $"{(int)Math.Round(_correctKeystrokes / (ElapsedSeconds / 60.0))}" : "0";
    public string AccuracyText => _totalKeystrokes > 0 ? $"{(int)Math.Round(100.0 * _correctKeystrokes / _totalKeystrokes)}%" : "100%";

    public string TimeText
    {
        get
        {
            var seconds = (int)ElapsedSeconds;
            return seconds < 60 ? $"{seconds}s" : $"{seconds / 60}:{seconds % 60:D2}";
        }
    }

    private double ElapsedSeconds => _startedAt is null ? 0 : (DateTime.UtcNow - _startedAt.Value).TotalSeconds;

    private void EnsureLineSequenceLength(int length)
    {
        var keys = Punishment.WritingLinesSentences is { Count: > 0 }
            ? Punishment.WritingLinesSentences
            : EmployerPhraseBank.GetRandomSet(1);

        while (_lineSequence.Count < length)
        {
            var shuffled = keys.OrderBy(_ => Random.Shared.Next());
            _lineSequence.AddRange(shuffled.Select(L.T));
        }
    }

    public ICommand AbandonCommand { get; }

    private void StartSilentCaptureIfAvailable()
    {
        if (!WebcamPreviewService.DetectWebcam(MainVm.Webcam.PreferredCameraIndex))
        {
            return;
        }

        _captureCts = new CancellationTokenSource();
        var slotName = SaveRepo.GetGameState()?.SlotName ?? "unknown";
        SilentCaptureLoop.Start(_settings, MainVm.Webcam.PreferredCameraIndex, slotName, "writingLines", _captureCts.Token);
    }

    /// <summary>Called from the View's TextInput handler for every character the player types. There is no
    /// backspace/undo (a real typewriter has none) — a wrong character clears the line immediately instead.</summary>
    public void HandleCharacter(string text)
    {
        if (IsInterruptionVisible || IsResult || string.IsNullOrEmpty(text) || TypedText == SentenceText)
        {
            return;
        }

        // After a mistake, keystrokes are ignored until the player pauses for 500ms — otherwise a player
        // who doesn't notice the reset keeps typing into an already-cleared line, multiplying mistakes.
        if (_inputLocked)
        {
            RestartMistakeLockTimer();
            return;
        }

        // The stopwatch and live-stats timer start on the first keystroke, not on screen open — Time/CPM
        // must reflect the writing attempt itself, not idle time spent looking at the phrase.
        _startedAt ??= DateTime.UtcNow;
        if (_statsTimer is null)
        {
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += (_, _) => RaiseStatsChanged();
            _statsTimer.Start();
        }

        if (Punishment.WritingLinesNoIdleActive)
        {
            RestartNoIdleTimer();
        }

        _totalKeystrokes++;
        var candidate = TypedText + text;

        switch (WritingLinesValidator.Validate(SentenceText, candidate))
        {
            case WritingLinesValidationResult.Continue:
            case WritingLinesValidationResult.LineComplete:
                _correctKeystrokes++;
                TypedText = candidate;
                RaiseStatsChanged();
                break;

            case WritingLinesValidationResult.Mistake:
                SoundEffectService.PlayBuzz();
                var (correctPart, errorChar) = WritingLinesValidator.SplitAtMismatch(SentenceText, candidate);
                History.Insert(0, new WritingLineHistoryEntry(correctPart, errorChar));
                MistakeCount++;
                TotalLines = Math.Min(TotalLines + _penaltyLinesPerMistake, _settings.WritingLinesMaxLines);
                EnsureLineSequenceLength(TotalLines);
                TypedText = "";
                RaiseStatsChanged();

                Punishment.WritingLinesTotalLines = TotalLines;
                SaveRepo.SaveActivePunishment(Punishment);

                _inputLocked = true;
                RestartMistakeLockTimer();
                break;
        }
    }

    private void RestartMistakeLockTimer()
    {
        _mistakeLockTimer?.Stop();
        _mistakeLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _mistakeLockTimer.Tick += (_, _) =>
        {
            _inputLocked = false;
            _mistakeLockTimer?.Stop();
            _mistakeLockTimer = null;
        };
        _mistakeLockTimer.Start();
    }

    /// <summary>Restarted on every accepted keystroke. Fires repeatedly for as long as the player stays
    /// idle, adding penalty lines each time it does — independent of the post-mistake input lock.</summary>
    private void RestartNoIdleTimer()
    {
        _noIdleTimer?.Stop();
        _noIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.WritingLinesNoIdleTimeoutSeconds) };
        _noIdleTimer.Tick += (_, _) =>
        {
            TotalLines = Math.Min(TotalLines + _settings.WritingLinesNoIdleLinesToAdd, _settings.WritingLinesMaxLines);
            EnsureLineSequenceLength(TotalLines);
            RaiseStatsChanged();

            Punishment.WritingLinesTotalLines = TotalLines;
            SaveRepo.SaveActivePunishment(Punishment);
        };
        _noIdleTimer.Start();
    }

    private void StartInterruptionLoopIfActive()
    {
        if (!Punishment.WritingLinesInterruptionsActive)
        {
            return;
        }

        _interruptionCts = new CancellationTokenSource();
        _ = RunInterruptionLoop(_interruptionCts.Token);
    }

    private async Task RunInterruptionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var delaySeconds = Random.Shared.Next(
                _settings.WritingLinesInterruptionMinIntervalSeconds,
                _settings.WritingLinesInterruptionMaxIntervalSeconds + 1);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            await Dispatcher.UIThread.InvokeAsync(ShowInterruption);
        }
    }

    private void ShowInterruption()
    {
        if (IsResult)
        {
            return;
        }

        InterruptionMessage = L.T(Interruptions[Random.Shared.Next(Interruptions.Length)]);
        IsInterruptionVisible = true;
        SoundEffectService.PlayClock();

        _interruptionHideTimer?.Stop();
        _interruptionHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(InterruptionDisplayMilliseconds) };
        _interruptionHideTimer.Tick += (_, _) =>
        {
            IsInterruptionVisible = false;
            _interruptionHideTimer?.Stop();
            _interruptionHideTimer = null;
        };
        _interruptionHideTimer.Start();
    }

    /// <summary>Called from the View's KeyDown handler on Enter. Confirms the current line — a no-op unless
    /// it has been typed out in full, since a wrong keystroke already clears the buffer on the spot.</summary>
    public void HandleEnter()
    {
        if (IsInterruptionVisible || IsResult || TypedText != SentenceText)
        {
            return;
        }

        if (Punishment.WritingLinesNoIdleActive)
        {
            RestartNoIdleTimer();
        }

        History.Insert(0, new WritingLineHistoryEntry(SentenceText, ""));
        CompletedLines++;
        TypedText = "";

        if (CompletedLines >= TotalLines)
        {
            Finish(success: true);
            return;
        }

        this.RaisePropertyChanged(nameof(SentenceText));
        RaiseStatsChanged();
    }

    private void RaiseStatsChanged()
    {
        this.RaisePropertyChanged(nameof(ProgressPercentText));
        this.RaisePropertyChanged(nameof(ProgressHintText));
        this.RaisePropertyChanged(nameof(CpmText));
        this.RaisePropertyChanged(nameof(AccuracyText));
        this.RaisePropertyChanged(nameof(TimeText));
    }

    private void Finish(bool success)
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = null;

        _interruptionCts?.Cancel();
        _interruptionCts?.Dispose();
        _interruptionCts = null;

        _statsTimer?.Stop();
        _statsTimer = null;

        _mistakeLockTimer?.Stop();
        _mistakeLockTimer = null;

        _noIdleTimer?.Stop();
        _noIdleTimer = null;

        _interruptionHideTimer?.Stop();
        _interruptionHideTimer = null;

        Punishment.WritingLinesAttemptActive = false;
        SaveRepo.SaveActivePunishment(Punishment);

        ResultMessage = Conclude(success,
            L.T("Every line, faithfully copied. I have taken note of your penmanship."),
            L.T("You set down the pen too soon. A fine has been added, and the lines remain unwritten."));
        IsResult = true;
    }
}
