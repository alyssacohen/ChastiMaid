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
    private readonly GameSettings _settings;
    private readonly int _penaltyLinesPerMistake;
    private CancellationTokenSource? _captureCts;
    private DispatcherTimer? _statsTimer;
    private DispatcherTimer? _mistakeLockTimer;
    private DateTime? _startedAt;
    private int _totalKeystrokes;
    private int _correctKeystrokes;
    private bool _inputLocked;

    [Reactive] private string _sentenceText = "";
    [Reactive] private int _totalLines;
    [Reactive] private int _completedLines;
    [Reactive] private int _mistakeCount;
    [Reactive] private string _typedText = "";
    [Reactive] private bool _isResult;
    [Reactive] private string _resultMessage = "";

    public WritingLinesPunishmentViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, ActivePunishment punishment)
        : base(mainVm, saveRepo, punishment)
    {
        _settings = saveRepo.GetGameSettings();
        _penaltyLinesPerMistake = _settings.WritingLinesPenaltyLinesPerMistake;
        _totalLines = punishment.WritingLinesTotalLines ?? 1;
        SentenceText = Resources.Get(punishment.WritingLinesSentenceKey ?? WritingLinesSentenceBank.GetRandom());

        this.WhenAnyValue(x => x.MistakeCount).Subscribe(_ => this.RaisePropertyChanged(nameof(HasMistakes)));

        AbandonCommand = ReactiveCommand.Create(() => Finish(success: false));

        StartSilentCaptureIfAvailable();
    }

    public bool HasMistakes => MistakeCount > 0;
    public ObservableCollection<WritingLineHistoryEntry> History { get; } = [];

    public string ProgressPercentText => TotalLines > 0 ? $"{(int)Math.Round(100.0 * CompletedLines / TotalLines)}%" : "0%";
    public string ProgressHintText => string.Format(Resources.Get("WritingLinesProgressHintFormat"), CompletedLines, TotalLines);
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

    public ICommand AbandonCommand { get; }

    private void StartSilentCaptureIfAvailable()
    {
        if (!WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex))
        {
            return;
        }

        _captureCts = new CancellationTokenSource();
        var slotName = SaveRepo.GetGameState()?.SlotName ?? "unknown";
        SilentCaptureLoop.Start(_settings, slotName, "writingLines", _captureCts.Token);
    }

    /// <summary>Called from the View's TextInput handler for every character the player types. There is no
    /// backspace/undo (a real typewriter has none) — a wrong character clears the line immediately instead.</summary>
    public void HandleCharacter(string text)
    {
        if (IsResult || string.IsNullOrEmpty(text) || TypedText == SentenceText)
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
                var (correctPart, errorChar) = WritingLinesValidator.SplitAtMismatch(SentenceText, candidate);
                History.Insert(0, new WritingLineHistoryEntry(correctPart, errorChar));
                MistakeCount++;
                TotalLines += _penaltyLinesPerMistake;
                TypedText = "";
                RaiseStatsChanged();

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

    /// <summary>Called from the View's KeyDown handler on Enter. Confirms the current line — a no-op unless
    /// it has been typed out in full, since a wrong keystroke already clears the buffer on the spot.</summary>
    public void HandleEnter()
    {
        if (IsResult || TypedText != SentenceText)
        {
            return;
        }

        History.Insert(0, new WritingLineHistoryEntry(SentenceText, ""));
        CompletedLines++;
        TypedText = "";

        if (CompletedLines >= TotalLines)
        {
            Finish(success: true);
            return;
        }

        SentenceText = Resources.Get(WritingLinesSentenceBank.GetRandom());
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

        _statsTimer?.Stop();
        _statsTimer = null;

        _mistakeLockTimer?.Stop();
        _mistakeLockTimer = null;

        ResultMessage = Conclude(success, "WritingLinesSuccessMessage", "WritingLinesFailureMessage");
        IsResult = true;
    }
}
