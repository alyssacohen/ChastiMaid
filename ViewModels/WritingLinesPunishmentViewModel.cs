using System.Reactive.Linq;
using System.Windows.Input;
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

        this.WhenAnyValue(x => x.TypedText).Skip(1).Subscribe(ValidateTypedText);
        this.WhenAnyValue(x => x.MistakeCount).Subscribe(_ => this.RaisePropertyChanged(nameof(HasMistakes)));

        AbandonCommand = ReactiveCommand.Create(() => Finish(success: false));

        StartSilentCaptureIfAvailable();
    }

    public string SentenceText { get; }
    public bool HasMistakes => MistakeCount > 0;

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

    private void ValidateTypedText(string text)
    {
        switch (WritingLinesValidator.Validate(SentenceText, text))
        {
            case WritingLinesValidationResult.LineComplete:
                TypedText = "";
                CompletedLines++;
                if (CompletedLines >= TotalLines)
                {
                    Finish(success: true);
                }

                break;

            case WritingLinesValidationResult.Mistake:
                MistakeCount++;
                TotalLines += _penaltyLinesPerMistake;
                TypedText = "";
                break;
        }
    }

    private void Finish(bool success)
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = null;

        ResultMessage = Conclude(success, "WritingLinesSuccessMessage", "WritingLinesFailureMessage");
        IsResult = true;
    }
}
