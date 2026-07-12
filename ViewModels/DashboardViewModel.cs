using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Views.Dialogs;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public class ChoreItemViewModel : ViewModelBase
{
    public ChoreItemViewModel(Chore chore, double multiplier, Action<ChoreItemViewModel> onStart)
    {
        ChoreId = chore.Id;
        Name = chore.Name;
        Description = chore.Description;
        Value = chore.Value;
        var effective = Math.Round(chore.Value * (decimal)multiplier, 2);
        ValueDisplay = $"${effective:F2}";
        StartChoreCommand = ReactiveCommand.Create(() => onStart(this));
    }

    public int ChoreId { get; }
    public string Name { get; }
    public string Description { get; }
    public decimal Value { get; }
    public string ValueDisplay { get; }
    public ICommand StartChoreCommand { get; }
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AutoSaveService _autoSave;
    private readonly DispatcherTimer? _countdownTimer;
    private readonly MainWindowViewModel _mainVm;
    private readonly double _outfitBonus;
    private readonly Random _random = new();
    private readonly SaveRepository _saveRepo;
    private readonly SessionLoopService _sessionLoop;
    private readonly GameSettings _settings;

    private ChoreItemViewModel? _activeChore;
    [Reactive] private string _busyChoreDescription = "";

    // --- Busy mode ---
    [Reactive] private string _busyChoreName = "";
    [Reactive] private string _busyChoreValueDisplay = "";
    [Reactive] private string _busyElapsed = "00:00";
    private CancellationTokenSource? _captureCts;
    [Reactive] private string _choreResultMessage = "";
    private DateTime _choreStartTime;
    private DispatcherTimer? _choreTimer;

    private ImpossibleQuestion? _currentQuestion;

    [Reactive] private decimal _currentDebt;
    [Reactive] private OutfitViewModel? _currentOutfitVm;

    [Reactive] private string _debtDisplay = "";
    [Reactive] private decimal _debtSettled;

    // --- Debt widget data ---
    [Reactive] private decimal _debtTotal;
    [Reactive] private bool _isBusy;
    [Reactive] private bool _isIdle = true;

    // --- Orientation ---
    [Reactive] private bool _isLandscape;

    // --- Outfit drawer ---
    [Reactive] private bool _isOutfitDrawerOpen;

    // --- Idle popup: Impossible Questions ---
    [Reactive] private bool _isPopupVisible;
    [Reactive] private bool _isShowingPopupOutcome;

    [Reactive] private string _payRate = "";

    // --- Pending punishments ---
    [Reactive] private int _pendingPunishmentsCount;
    [Reactive] private ObservableCollection<string> _popupAnswers = [];
    [Reactive] private int _popupCountdown;
    [Reactive] private string _popupOutcomeMessage = "";
    [Reactive] private string _popupQuestion = "";
    private DispatcherTimer? _popupOutcomeTimer;
    private DispatcherTimer? _popupTimer;
    private bool _pendingOutcomePositive;
    [Reactive] private string _timeToNextWage = "";

    // --- Idle popup: Pose for Camera ---
    [Reactive] private bool _isPoseCountdownActive;
    [Reactive] private bool _isPoseForCameraPopupActive;
    [Reactive] private int _poseCountdown;
    [Reactive] private Bitmap? _capturedPosePhoto;

    // Responsive sizing, recalculated on window resize (see DashboardView.axaml.cs) — shared by
    // every idle popup that shows a live webcam preview (Pose for Camera, Curtsy, ...)
    [Reactive] private double _popupDialogMaxHeight = 420;
    [Reactive] private double _popupPreviewWidth = 280;
    [Reactive] private double _popupPreviewHeight = 420;
    private DispatcherTimer? _poseCountdownTimer;
    private ClothingItem? _currentPoseItem;

    // --- Idle popup: Curtsy (video di riverenza) ---
    [Reactive] private bool _isCurtsyCountdownActive;
    [Reactive] private bool _isCurtsyRecordingActive;
    [Reactive] private bool _isCurtsyPopupActive;
    [Reactive] private int _curtsyCountdown;
    [Reactive] private int _curtsyRecordingSecondsLeft;
    private DispatcherTimer? _curtsyCountdownTimer;
    private CancellationTokenSource? _curtsyRecordingCts;

    // --- Idle popup: Motivational Quote ---
    [Reactive] private bool _isQuotePopupActive;
    [Reactive] private bool _isQuoteListeningActive;
    [Reactive] private bool _isQuoteTypingActive;
    [Reactive] private int _quoteListenCountdown;
    [Reactive] private int _quoteAttemptsRemaining;
    [Reactive] private string _quoteTypedAnswer = "";
    private const int QuoteMaxAttempts = 3;
    private string? _currentQuoteText;
    private DispatcherTimer? _quoteListenTimer;
    private CancellationTokenSource? _quoteListenCts;

    /// <summary>Standard constructor — enters idle mode.</summary>
    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
        : this(mainVm, saveRepo, null)
    {
    }

    /// <summary>If startChore is not null, enters busy mode directly.</summary>
    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Chore? startChore)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _autoSave = mainVm.AutoSaveService;
        _settings = saveRepo.GetGameSettings();

        _autoSave.Bind(saveRepo);
        _autoSave.StartTimer(_settings.AutoSaveIntervalMinutes);

        var state = saveRepo.GetGameState();
        if (state is not null)
        {
            state.LastSessionAt = DateTime.UtcNow;
            saveRepo.SaveGameState(state);
        }

        CurrentDebt = state?.CurrentDebt ?? 0;
        DebtDisplay = FormatDebt(CurrentDebt);
        InitialDebtDisplay = string.Format(Resources.Get("DashboardInitialDebtFormat"), state?.InitialDebt ?? 0);
        CurrentOutfit = state?.CurrentOutfit ?? Resources.Get("DashboardNoOutfitSet");

        _outfitBonus = state?.EffectiveMultiplier ?? 1.0;
        var effectiveWage = Math.Round(_settings.IdleWageAmount * (decimal)_outfitBonus, 2);
        PayRate = string.Format(Resources.Get("DashboardWageFormat"), effectiveWage, _settings.IdleWageIntervalMinutes);

        LoadChores();

        foreach (var punishment in saveRepo.GetActivePunishments())
        {
            Punishments.Add(new PunishmentItemViewModel(punishment));
        }

        Punishments.CollectionChanged += (_, _) =>
        {
            PendingPunishmentsCount = Punishments.Count;
            this.RaisePropertyChanged(nameof(HasPendingPunishments));
        };
        PendingPunishmentsCount = Punishments.Count;

        // Debt widget
        DebtTotal = state?.InitialDebt ?? 0;
        DebtSettled = (state?.InitialDebt ?? 0) - (state?.CurrentDebt ?? 0);

        // Outfit drawer
        CurrentOutfitVm = OutfitViewModel.FromString(state?.CurrentOutfit);

        _sessionLoop = new SessionLoopService(saveRepo, _settings);
        _sessionLoop.OutfitBonus = _outfitBonus;
        _sessionLoop.OnIdleWagePaid = () => Dispatcher.UIThread.Post(RefreshDebt);
        _sessionLoop.OnPopupTriggered = () => Dispatcher.UIThread.Post(ShowIdlePopup);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateWageCountdown();

        EndSessionCommand = ReactiveCommand.Create(EndSession);
        CompleteGameCommand =
            ReactiveCommand.Create(() => _mainVm.NavigateTo(new GameCompleteViewModel(_mainVm, _saveRepo)));
        AnswerCommand = ReactiveCommand.Create<string>(OnAnswer);
        DismissPopupOutcomeCommand = ReactiveCommand.Create(DismissPopupOutcome);
        SubmitQuoteTypedAnswerCommand = ReactiveCommand.Create(OnSubmitQuoteTypedAnswer);
        EndChoreCommand = ReactiveCommand.Create(OnEndChore);

        var canOpenPunishments = this.WhenAnyValue(x => x.PendingPunishmentsCount, c => c > 0);
        ToggleOutfitDrawerCommand = ReactiveCommand.Create(() => IsOutfitDrawerOpen = !IsOutfitDrawerOpen);
        OpenPunishmentsCommand = ReactiveCommand.CreateFromTask(OpenPunishmentsAsync, canOpenPunishments);
        StartTaskCommand = ReactiveCommand.Create<TaskItemViewModel>(OnStartTask);
        StartPunishmentCommand = ReactiveCommand.Create<PunishmentItemViewModel>(OnStartPunishment);
        RequestPermissionCommand = ReactiveCommand.Create(() =>
        {
            /* spec TBD */
        });
        OpenShopCommand = ReactiveCommand.Create(() =>
        {
            /* spec TBD */
        });
        EmergencyBreakCommand = ReactiveCommand.Create(OnEmergencyBreak);

        if (startChore is not null)
        {
            // Find the matching ChoreItemViewModel
            _activeChore = Chores.FirstOrDefault(c => c.ChoreId == startChore.Id);
            if (_activeChore is not null)
            {
                EnterBusyMode();
            }
            else
            {
                StartIdleMode();
            }
        }
        else
        {
            StartIdleMode();
        }
    }

    public string InitialDebtDisplay { get; }

    public bool CanComplete => CurrentDebt <= 0;
    public decimal DebtRemaining => DebtTotal - DebtSettled;
    public double DebtProgress => DebtTotal > 0 ? Math.Clamp((double)(DebtSettled / DebtTotal), 0.0, 1.0) : 0.0;
    public bool HasPendingPunishments => PendingPunishmentsCount > 0;

    public ObservableCollection<ChoreItemViewModel> Chores { get; } = [];
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];
    public ObservableCollection<PunishmentItemViewModel> Punishments { get; } = [];

    public string CurrentOutfit { get; }
    public WebcamPreviewService Webcam => _mainVm.Webcam;

    public bool IsShowingQuestionPanel =>
        !IsShowingPopupOutcome && !IsPoseCountdownActive && !IsCurtsyCountdownActive && !IsCurtsyRecordingActive &&
        !IsQuoteListeningActive && !IsQuoteTypingActive;

    public bool IsWebcamPreviewNeededInQuestionPanel => IsPoseForCameraPopupActive || IsCurtsyPopupActive;

    public ICommand EndSessionCommand { get; }
    public ICommand CompleteGameCommand { get; }
    public ICommand AnswerCommand { get; }
    public ICommand DismissPopupOutcomeCommand { get; }
    public ICommand SubmitQuoteTypedAnswerCommand { get; }
    public ICommand EndChoreCommand { get; }

    // --- New dashboard commands ---
    public ICommand ToggleOutfitDrawerCommand { get; }
    public ICommand OpenPunishmentsCommand { get; }
    public ICommand StartTaskCommand { get; }
    public ICommand StartPunishmentCommand { get; }
    public ICommand RequestPermissionCommand { get; }
    public ICommand OpenShopCommand { get; }
    public ICommand EmergencyBreakCommand { get; }

    private void StartIdleMode()
    {
        _sessionLoop.Start();
        _countdownTimer?.Start();
    }

    private static string FormatDebt(decimal debt)
    {
        return string.Format(Resources.Get("DashboardDebtFormat"), debt);
    }

    private void LoadChores()
    {
        Chores.Clear();
        Tasks.Clear();
        foreach (var chore in _saveRepo.GetChores())
        {
            Chores.Add(new ChoreItemViewModel(chore, _outfitBonus, OnStartChore));
            Tasks.Add(new TaskItemViewModel(chore, OnStartTask));
        }
    }

    private void RefreshPunishments()
    {
        Punishments.Clear();
        foreach (var punishment in _saveRepo.GetActivePunishments())
        {
            Punishments.Add(new PunishmentItemViewModel(punishment));
        }
    }

    private void RefreshDebt()
    {
        var state = _saveRepo.GetGameState();
        CurrentDebt = state?.CurrentDebt ?? 0;
        DebtDisplay = FormatDebt(CurrentDebt);
        DebtTotal = state?.InitialDebt ?? 0;
        DebtSettled = (state?.InitialDebt ?? 0) - (state?.CurrentDebt ?? 0);
        this.RaisePropertyChanged(nameof(CanComplete));
        this.RaisePropertyChanged(nameof(DebtRemaining));
        this.RaisePropertyChanged(nameof(DebtProgress));
    }

    private void UpdateWageCountdown()
    {
        if (!IsIdle)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - _sessionLoop.LastWageTime;
        var interval = TimeSpan.FromMinutes(_sessionLoop.WageIntervalMinutes);
        var remaining = interval - elapsed;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        TimeToNextWage = string.Format(Resources.Get("DashboardNextWageFormat"), remaining.Minutes, remaining.Seconds);
    }

    // --- Idle popup: Impossible Questions / Hydration / Pose for Camera / Curtsy / Motivational Quote ---
    private enum IdlePopupKind
    {
        ImpossibleQuestion,
        Hydration,
        PoseForCamera,
        Curtsy,
        MotivationalQuote
    }

    private static readonly IdlePopupKind[] EnabledIdlePopupKinds =
    [
        IdlePopupKind.ImpossibleQuestion, IdlePopupKind.Hydration, IdlePopupKind.PoseForCamera,
        IdlePopupKind.Curtsy, IdlePopupKind.MotivationalQuote
    ];

    private IdlePopupKind _currentPopupKind;
    private DateTime _lastHydrationPopupAt = DateTime.MinValue;

    private bool IsHydrationAvailable()
    {
        return DateTime.UtcNow - _lastHydrationPopupAt >= TimeSpan.FromMinutes(_settings.HydrationMinIntervalMinutes);
    }

    private bool IsPoseForCameraAvailable()
    {
        var itemIds = _saveRepo.GetGameState()?.CurrentOutfitItemIds;
        if (itemIds is null || itemIds.Count == 0)
        {
            return false;
        }

        return WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex);
    }

    private bool IsKindAvailable(IdlePopupKind kind)
    {
        return kind switch
        {
            IdlePopupKind.Hydration => IsHydrationAvailable(),
            IdlePopupKind.PoseForCamera => IsPoseForCameraAvailable(),
            IdlePopupKind.Curtsy => WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex),
            _ => true
        };
    }

    private void ShowIdlePopup()
    {
        if (IsPopupVisible || IsBusy)
        {
            return;
        }

        var candidates = EnabledIdlePopupKinds.Where(IsKindAvailable).ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        switch (candidates[_random.Next(candidates.Length)])
        {
            case IdlePopupKind.ImpossibleQuestion:
                ShowImpossibleQuestionPopup();
                break;
            case IdlePopupKind.Hydration:
                ShowHydrationPopup();
                break;
            case IdlePopupKind.PoseForCamera:
                ShowPoseForCameraPopup();
                break;
            case IdlePopupKind.Curtsy:
                ShowCurtsyPopup();
                break;
            case IdlePopupKind.MotivationalQuote:
                ShowMotivationalQuotePopup();
                break;
        }
    }

    private void ShowImpossibleQuestionPopup()
    {
        _currentPopupKind = IdlePopupKind.ImpossibleQuestion;
        _currentQuestion = ImpossibleQuestionBank.GetRandom();
        PopupQuestion = Resources.Get(_currentQuestion.QuestionKey);
        PopupAnswers =
        [
            Resources.Get("ImpossibleQuestionYes"),
            Resources.Get("ImpossibleQuestionNo")
        ];
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
        {
            Task.Run(() => Console.Beep(800, 300));
        }

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
                _popupTimer?.Stop();
                _popupTimer = null;
                ClosePopup(false);
            }
        };
        _popupTimer.Start();
    }

    private void ShowHydrationPopup()
    {
        _currentPopupKind = IdlePopupKind.Hydration;
        _lastHydrationPopupAt = DateTime.UtcNow;
        PopupQuestion = Resources.Get(HydrationReminderBank.GetRandom());
        PopupAnswers = [Resources.Get("HydrationConfirm")];
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
        {
            Task.Run(() => Console.Beep(800, 300));
        }

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
                _popupTimer?.Stop();
                _popupTimer = null;
                ClosePopup(false);
            }
        };
        _popupTimer.Start();
    }

    private void ShowPoseForCameraPopup()
    {
        var itemIds = _saveRepo.GetGameState()?.CurrentOutfitItemIds ?? [];
        var candidates = _saveRepo.GetWardrobe().Where(i => itemIds.Contains(i.Id)).ToList();
        if (candidates.Count == 0 || !_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            return;
        }

        _currentPopupKind = IdlePopupKind.PoseForCamera;
        IsPoseForCameraPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        _currentPoseItem = candidates[_random.Next(candidates.Count)];
        PopupQuestion = string.Format(Resources.Get(PoseForCameraInstructionBank.GetRandom()), _currentPoseItem.Name);
        PopupAnswers = [Resources.Get("PoseForCameraConfirm")];
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
        {
            Task.Run(() => Console.Beep(800, 300));
        }

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
                _popupTimer?.Stop();
                _popupTimer = null;
                _currentPoseItem = null;
                ClosePopup(false);
            }
        };
        _popupTimer.Start();
    }

    private void ShowCurtsyPopup()
    {
        if (!_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            return;
        }

        _currentPopupKind = IdlePopupKind.Curtsy;
        IsCurtsyPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        PopupQuestion = Resources.Get(CurtsyInstructionBank.GetRandom());
        PopupAnswers = [Resources.Get("CurtsyConfirm")];
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
        {
            Task.Run(() => Console.Beep(800, 300));
        }

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
                _popupTimer?.Stop();
                _popupTimer = null;
                ClosePopup(false);
            }
        };
        _popupTimer.Start();
    }

    private void ShowMotivationalQuotePopup()
    {
        _currentPopupKind = IdlePopupKind.MotivationalQuote;
        _currentQuoteText = Resources.Get(MotivationalQuoteBank.GetRandom());
        PopupQuestion = string.Format(Resources.Get("MotivationalQuoteInstruction"), _currentQuoteText);
        PopupAnswers = [Resources.Get("MotivationalQuoteConfirm")];
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
        {
            Task.Run(() => Console.Beep(800, 300));
        }

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
                _popupTimer?.Stop();
                _popupTimer = null;
                _currentQuoteText = null;
                ClosePopup(false);
            }
        };
        _popupTimer.Start();
    }

    private void OnAnswer(string answer)
    {
        if (!IsPopupVisible)
        {
            return;
        }

        switch (_currentPopupKind)
        {
            case IdlePopupKind.ImpossibleQuestion:
                OnImpossibleQuestionAnswer(answer);
                break;
            case IdlePopupKind.Hydration:
                OnHydrationConfirm();
                break;
            case IdlePopupKind.PoseForCamera:
                OnPoseForCameraConfirm();
                break;
            case IdlePopupKind.Curtsy:
                OnCurtsyConfirm();
                break;
            case IdlePopupKind.MotivationalQuote:
                OnMotivationalQuoteConfirm();
                break;
        }
    }

    private void OnPoseForCameraConfirm()
    {
        _popupTimer?.Stop();
        _popupTimer = null;

        IsPoseCountdownActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        PoseCountdown = 3;

        _poseCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _poseCountdownTimer.Tick += async (_, _) =>
        {
            PoseCountdown--;
            if (PoseCountdown <= 0)
            {
                _poseCountdownTimer?.Stop();
                _poseCountdownTimer = null;
                await CapturePoseAsync();
            }
        };
        _poseCountdownTimer.Start();
    }

    private async Task CapturePoseAsync()
    {
        var item = _currentPoseItem;
        _currentPoseItem = null;
        IsPoseCountdownActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        if (item is null)
        {
            _mainVm.Webcam.Stop();
            PopupOutcomeMessage = Resources.Get("PoseForCameraFailedMessage");
            ShowPopupOutcome(true);
            return;
        }

        var slotName = _saveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var path = Path.Combine(dir, $"pose_{timestamp}.png");

        var success = false;
        try
        {
            success = await _mainVm.Webcam.CaptureFrameAsync(path);
        }
        catch
        {
            /* Webcam unavailable — treated as capture failure below */
        }

        _mainVm.Webcam.Stop();

        if (success)
        {
            _saveRepo.SavePoseCheckRecord(new PoseCheckRecord
            {
                ItemId = item.Id,
                ItemName = item.Name,
                PhotoPath = path,
                CapturedAt = DateTime.UtcNow,
                Reviewed = false,
                IsCompliant = null
            });

            try
            {
                CapturedPosePhoto = new Bitmap(path);
            }
            catch
            {
                CapturedPosePhoto = null;
            }
        }

        PopupOutcomeMessage = Resources.Get(success ? "PoseForCameraCapturedMessage" : "PoseForCameraFailedMessage");
        ShowPopupOutcome(true);
    }

    private void OnCurtsyConfirm()
    {
        _popupTimer?.Stop();
        _popupTimer = null;

        IsCurtsyCountdownActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        CurtsyCountdown = 3;

        _curtsyCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _curtsyCountdownTimer.Tick += async (_, _) =>
        {
            CurtsyCountdown--;
            if (CurtsyCountdown <= 0)
            {
                _curtsyCountdownTimer?.Stop();
                _curtsyCountdownTimer = null;
                await StartCurtsyRecordingAsync();
            }
        };
        _curtsyCountdownTimer.Start();
    }

    private async Task StartCurtsyRecordingAsync()
    {
        IsCurtsyCountdownActive = false;
        IsCurtsyRecordingActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        CurtsyRecordingSecondsLeft = _settings.CurtsyVideoDurationSeconds;

        var slotName = _saveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var path = Path.Combine(dir, $"curtsy_{timestamp}.avi");

        _curtsyRecordingCts = new CancellationTokenSource();
        var success = false;
        try
        {
            await _mainVm.Webcam.RecordVideoAsync(path, _settings.CurtsyVideoDurationSeconds,
                new Progress<int>(r => CurtsyRecordingSecondsLeft = r), _curtsyRecordingCts.Token);
            success = true;
        }
        catch
        {
            /* Webcam unavailable/cancelled — treated as capture failure below */
        }

        _curtsyRecordingCts?.Dispose();
        _curtsyRecordingCts = null;
        IsCurtsyRecordingActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        _mainVm.Webcam.Stop();

        if (success)
        {
            _saveRepo.SaveCurtsyCheckRecord(new CurtsyCheckRecord
            {
                VideoPath = path,
                RecordedAt = DateTime.UtcNow,
                Reviewed = false,
                IsCompliant = null
            });
        }

        PopupOutcomeMessage = Resources.Get(success ? "CurtsyCapturedMessage" : "CurtsyFailedMessage");
        ShowPopupOutcome(true);
    }

    private void OnMotivationalQuoteConfirm()
    {
        _popupTimer?.Stop();
        _popupTimer = null;
        IsQuotePopupActive = true;

        if (SpeechRecognitionService.DetectMicrophone())
        {
            StartQuoteListening();
        }
        else
        {
            StartQuoteTyping();
        }
    }

    private async void StartQuoteListening()
    {
        IsQuoteListeningActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        QuoteAttemptsRemaining = QuoteMaxAttempts;
        QuoteListenCountdown = _settings.QuoteListenTimeoutSeconds;

        _quoteListenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _quoteListenTimer.Tick += (_, _) => QuoteListenCountdown--;
        _quoteListenTimer.Start();

        _quoteListenCts = new CancellationTokenSource();
        var success = false;
        try
        {
            success = await SpeechRecognitionService.ListenForPhraseAsync(
                _currentQuoteText!,
                QuoteMaxAttempts,
                TimeSpan.FromSeconds(_settings.QuoteListenTimeoutSeconds),
                _settings.QuoteRecognitionMinConfidence,
                new Progress<int>(remaining => QuoteAttemptsRemaining = remaining),
                _quoteListenCts.Token);
        }
        catch
        {
            /* Speech engine unavailable — a mismatch here IS punishable, unlike Pose/Curtsy's
               technical-failure fallback, because verification is the whole point of this popup */
        }

        _quoteListenTimer?.Stop();
        _quoteListenTimer = null;
        _quoteListenCts?.Dispose();
        _quoteListenCts = null;
        IsQuoteListeningActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        PopupOutcomeMessage = Resources.Get(success ? "MotivationalQuoteSuccessMessage" : "MotivationalQuoteFailedMessage");
        ShowPopupOutcome(success);
    }

    private void StartQuoteTyping()
    {
        IsQuoteTypingActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        QuoteTypedAnswer = "";
        QuoteListenCountdown = _settings.QuoteListenTimeoutSeconds;

        _quoteListenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _quoteListenTimer.Tick += (_, _) =>
        {
            QuoteListenCountdown--;
            if (QuoteListenCountdown <= 0)
            {
                _quoteListenTimer?.Stop();
                _quoteListenTimer = null;
                FinishQuoteTyping(false);
            }
        };
        _quoteListenTimer.Start();
    }

    private void OnSubmitQuoteTypedAnswer()
    {
        if (!IsQuoteTypingActive)
        {
            return;
        }

        var match = string.Equals(QuoteTypedAnswer.Trim(), _currentQuoteText?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (match)
        {
            _quoteListenTimer?.Stop();
            _quoteListenTimer = null;
            FinishQuoteTyping(true);
        }
    }

    private void FinishQuoteTyping(bool success)
    {
        IsQuoteTypingActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        PopupOutcomeMessage = Resources.Get(success ? "MotivationalQuoteSuccessMessage" : "MotivationalQuoteFailedMessage");
        ShowPopupOutcome(success);
    }

    private void OnImpossibleQuestionAnswer(string answer)
    {
        if (_currentQuestion is null)
        {
            return;
        }

        _popupTimer?.Stop();
        _popupTimer = null;

        var isYes = PopupAnswers.Count > 0 && answer == PopupAnswers[0];
        var positive = ImpossibleQuestionBank.RollOutcome();
        var outcomeKey = isYes
            ? (positive ? _currentQuestion.YesPositiveKey : _currentQuestion.YesNegativeKey)
            : (positive ? _currentQuestion.NoPositiveKey : _currentQuestion.NoNegativeKey);

        PopupOutcomeMessage = Resources.Get(outcomeKey);
        ShowPopupOutcome(positive);
    }

    private void OnHydrationConfirm()
    {
        _popupTimer?.Stop();
        _popupTimer = null;

        PopupOutcomeMessage = Resources.Get("HydrationConfirmedMessage");
        ShowPopupOutcome(true);
    }

    private void ShowPopupOutcome(bool positive)
    {
        _pendingOutcomePositive = positive;
        IsShowingPopupOutcome = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        _popupOutcomeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _popupOutcomeTimer.Tick += (_, _) =>
        {
            _popupOutcomeTimer?.Stop();
            _popupOutcomeTimer = null;
            ClosePopup(positive);
        };
        _popupOutcomeTimer.Start();
    }

    private void DismissPopupOutcome()
    {
        if (!IsShowingPopupOutcome)
        {
            return;
        }

        _popupOutcomeTimer?.Stop();
        _popupOutcomeTimer = null;
        ClosePopup(_pendingOutcomePositive);
    }

    private void ClosePopup(bool positive)
    {
        _popupTimer?.Stop();
        _popupTimer = null;
        _popupOutcomeTimer?.Stop();
        _popupOutcomeTimer = null;
        _poseCountdownTimer?.Stop();
        _poseCountdownTimer = null;
        _curtsyCountdownTimer?.Stop();
        _curtsyCountdownTimer = null;
        _curtsyRecordingCts?.Cancel();
        _curtsyRecordingCts?.Dispose();
        _curtsyRecordingCts = null;
        _quoteListenTimer?.Stop();
        _quoteListenTimer = null;
        _quoteListenCts?.Cancel();
        _quoteListenCts?.Dispose();
        _quoteListenCts = null;
        if (IsPoseForCameraPopupActive || IsCurtsyPopupActive)
        {
            _mainVm.Webcam.Stop();
        }

        IsPopupVisible = false;
        IsShowingPopupOutcome = false;
        IsPoseCountdownActive = false;
        IsPoseForCameraPopupActive = false;
        IsCurtsyCountdownActive = false;
        IsCurtsyRecordingActive = false;
        IsCurtsyPopupActive = false;
        IsQuotePopupActive = false;
        IsQuoteListeningActive = false;
        IsQuoteTypingActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        _currentQuestion = null;
        _currentPoseItem = null;
        _currentQuoteText = null;
        QuoteTypedAnswer = "";
        CapturedPosePhoto?.Dispose();
        CapturedPosePhoto = null;
        _sessionLoop.ReportPopupResult(positive);

        if (!positive)
        {
            _saveRepo.AssignRandomPunishment();
            RefreshPunishments();
        }
    }

    // --- Chore start → naviga a CameraSetupView ---
    private void OnStartChore(ChoreItemViewModel choreVm)
    {
        if (IsBusy)
        {
            return;
        }

        ChoreResultMessage = "";

        // Stop timers before navigating away
        _sessionLoop.Pause();
        _countdownTimer?.Stop();

        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == choreVm.ChoreId);
        if (chore is null)
        {
            return;
        }

        // Webcam available — navigate to camera setup
        if (WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex))
        {
            _mainVm.NavigateTo(new CameraSetupViewModel(_mainVm, _saveRepo, chore));
        }
        else
        {
            // No webcam — enter busy mode directly
            _activeChore = choreVm;
            EnterBusyMode();
        }
    }

    private void EnterBusyMode()
    {
        if (_activeChore is null)
        {
            return;
        }

        _sessionLoop.Pause();
        _countdownTimer?.Stop();
        IsIdle = false;
        IsBusy = true;
        BusyChoreName = _activeChore.Name;
        BusyChoreDescription = _activeChore.Description;
        BusyChoreValueDisplay = $"${Math.Round(_activeChore.Value * (decimal)_outfitBonus, 2):F2}";
        _choreStartTime = DateTime.UtcNow;
        BusyElapsed = "00:00";

        _choreTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _choreTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _choreStartTime;
            BusyElapsed = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        };
        _choreTimer.Start();

        StartSilentCapture();
    }

    private void OnEndChore()
    {
        if (!IsBusy || _activeChore is null)
        {
            return;
        }

        _choreTimer?.Stop();
        _choreTimer = null;
        StopSilentCapture();

        var elapsed = DateTime.UtcNow - _choreStartTime;
        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == _activeChore.ChoreId);
        var tooEarly = chore is not null && elapsed.TotalMinutes < chore.MinCompletionMinutes;

        if (tooEarly)
        {
            _saveRepo.AssignRandomPunishment();
            RefreshPunishments();
            ChoreResultMessage = Resources.Get("DashboardChoreRejectedMessage");
        }
        else
        {
            var state = _saveRepo.GetGameState();
            if (state is not null)
            {
                var credit = Math.Round(_activeChore.Value * (decimal)_outfitBonus, 0);
                state.CurrentDebt = Math.Max(0, state.CurrentDebt - credit);
                _saveRepo.SaveGameState(state);
            }

            if (chore is not null)
            {
                _saveRepo.UpdateChoreCompletion(chore.Id, DateTime.UtcNow);
            }

            ChoreResultMessage = "";
        }

        _activeChore = null;
        IsBusy = false;
        IsIdle = true;
        RefreshDebt();
        LoadChores();

        _sessionLoop.Resume();
        _countdownTimer?.Start();
    }

    // --- Silent media capture ---
    private void StartSilentCapture()
    {
        _captureCts = new CancellationTokenSource();
        var ct = _captureCts.Token;
        var slotName = _saveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "saves", slotName);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var delaySec = _random.Next(_settings.BusyCaptureMinIntervalSeconds,
                    _settings.BusyCaptureMaxIntervalSeconds + 1);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    if (_random.Next(2) == 0)
                    {
                        var path = Path.Combine(dir, $"chore_{timestamp}.png");
                        await WebcamPreviewService.CaptureFrameStandaloneAsync(path, ct, _settings.SelectedCameraIndex);
                    }
                    else
                    {
                        var duration = _random.Next(3, 11);
                        var path = Path.Combine(dir, $"chore_{timestamp}.avi");
                        await WebcamPreviewService.RecordVideoStandaloneAsync(path, duration, ct: ct,
                            cameraIndex: _settings.SelectedCameraIndex);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    /* Webcam unavailable — ignore */
                }
            }
        }, ct);
    }

    private void StopSilentCapture()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = null;
    }

    // --- New dashboard command handlers ---
    private void OnStartTask(TaskItemViewModel task)
    {
        var choreVm = Chores.FirstOrDefault(c => c.ChoreId == task.ChoreId);
        if (choreVm is not null)
        {
            OnStartChore(choreVm);
        }
    }

    private async Task OpenPunishmentsAsync()
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (owner is null)
        {
            return;
        }

        var dialog = new PunishmentListDialog(Punishments);
        await dialog.ShowDialog(owner);

        if (dialog.DialogResult && dialog.SelectedPunishment is not null)
        {
            OnStartPunishment(dialog.SelectedPunishment);
        }
    }

    private void OnStartPunishment(PunishmentItemViewModel punishment)
    {
        // TODO: navigate to punishment execution view (spec TBD)
    }

    private void OnEmergencyBreak()
    {
        // TODO: brief confirmation dialog, then log event and navigate to end view
        EndSession();
    }

    private void EndSession()
    {
        _sessionLoop.Stop();
        _countdownTimer?.Stop();
        _popupTimer?.Stop();
        _popupOutcomeTimer?.Stop();
        _poseCountdownTimer?.Stop();
        _curtsyCountdownTimer?.Stop();
        _curtsyRecordingCts?.Cancel();
        _curtsyRecordingCts?.Dispose();
        _quoteListenTimer?.Stop();
        _quoteListenCts?.Cancel();
        _quoteListenCts?.Dispose();
        _choreTimer?.Stop();
        StopSilentCapture();
        _mainVm.Webcam.Stop();
        CapturedPosePhoto?.Dispose();
        _autoSave.StopTimer();

        _mainVm.NavigateTo(new EndSessionViewModel(_mainVm, _saveRepo));
    }
}
