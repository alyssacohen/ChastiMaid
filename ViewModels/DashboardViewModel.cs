using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Converters;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     A single owned permission card offered for staging in the Permissions panel. Tapping toggles
///     <see cref="IsStaged" />; staged cards are combined and consumed when the request is submitted.
/// </summary>
public partial class PermissionCardChipViewModel : ViewModelBase
{
    public PermissionCardChipViewModel(CardId id)
    {
        Id = id;
        Name = CardDisplay.NameOf(id);
    }

    public CardId Id { get; }
    public string Name { get; }

    [Reactive] private bool _isStaged;
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

    private int? _activeChoreId;
    [Reactive] private string _busyChoreDescription = "";

    // --- Busy mode ---
    [Reactive] private string _busyChoreName = "";
    [Reactive] private string _busyChoreValueDisplay = "";
    [Reactive] private string _busyElapsed = "00:00";
    private CancellationTokenSource? _captureCts;
    [Reactive] private string _choreResultMessage = "";
    private DateTime _choreStartTime;
    private double _choreTargetMinutes;
    private DispatcherTimer? _choreTimer;

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

    // --- Chore result overlay (rejection/too-slow punishment feedback) ---
    [Reactive] private bool _isChoreResultVisible;
    private DispatcherTimer? _choreResultTimer;

    [Reactive] private string _payRate = "";

    // --- Pending punishments ---
    [Reactive] private int _pendingPunishmentsCount;
    [Reactive] private bool _isPunishmentsPanelVisible;

    // --- Add occasional task ---
    [Reactive] private bool _isAddTaskFormVisible;
    [Reactive] private string _newTaskName = "";
    [Reactive] private string _newTaskDescription = "";
    [Reactive] private int _newTaskEstimatedMinutes;

    // --- Permissions (break requests) ---
    [Reactive] private bool _isPermissionsPanelVisible;
    [Reactive] private bool _isPermissionOutcomeVisible;
    [Reactive] private string _permissionOutcomeMessage = "";
    [Reactive] private bool _isPermissionOutcomeGranted;
    [Reactive] private bool _isPermissionBreakActive;
    [Reactive] private string _permissionBreakKindName = "";
    [Reactive] private string _timeToNextWage = "";

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
        OutfitMultiplierDisplay = $"×{_outfitBonus:F2}";
        var effectiveWage = Math.Round(_settings.IdleWageAmount * (decimal)_outfitBonus, 2);
        PayRate = string.Format(Resources.Get("DashboardWageFormat"), effectiveWage, _settings.IdleWageIntervalMinutes);

        LoadChores();
        RefreshPunishments();

        Punishments.CollectionChanged += (_, _) =>
        {
            PendingPunishmentsCount = Punishments.Count;
            this.RaisePropertyChanged(nameof(HasPendingPunishments));
        };
        PendingPunishmentsCount = Punishments.Count;

        foreach (var kind in Enum.GetValues<PermissionKind>())
        {
            PermissionTypes.Add(new PermissionTypeItemViewModel(kind, OnRequestPermission));
        }

        // Debt widget
        DebtTotal = state?.InitialDebt ?? 0;
        DebtSettled = (state?.InitialDebt ?? 0) - (state?.CurrentDebt ?? 0);

        // Outfit drawer
        RefreshOutfitDisplay();

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
        DismissChoreResultCommand = ReactiveCommand.Create(DismissChoreResult);
        SubmitQuoteTypedAnswerCommand = ReactiveCommand.Create(OnSubmitQuoteTypedAnswer);
        ConfirmKneelFramingCommand = ReactiveCommand.Create(OnKneelFramingConfirm);
        EndChoreCommand = ReactiveCommand.Create(OnEndChore);

        var canOpenPunishments = this.WhenAnyValue(x => x.PendingPunishmentsCount, c => c > 0);
        ToggleOutfitDrawerCommand = ReactiveCommand.Create(() => IsOutfitDrawerOpen = !IsOutfitDrawerOpen);
        OpenPunishmentsCommand = ReactiveCommand.Create(() => IsPunishmentsPanelVisible = true, canOpenPunishments);
        ClosePunishmentsPanelCommand = ReactiveCommand.Create(() => IsPunishmentsPanelVisible = false);
        OpenAddTaskFormCommand = ReactiveCommand.Create(() =>
        {
            NewTaskName = "";
            NewTaskDescription = "";
            NewTaskEstimatedMinutes = 0;
            IsAddTaskFormVisible = true;
        });
        CloseAddTaskFormCommand = ReactiveCommand.Create(() => IsAddTaskFormVisible = false);
        var canSubmitAddTask = this.WhenAnyValue(x => x.NewTaskName, x => x.NewTaskEstimatedMinutes,
            (name, minutes) => !string.IsNullOrWhiteSpace(name) && minutes > 0);
        SubmitAddTaskCommand = ReactiveCommand.Create(OnSubmitAddTask, canSubmitAddTask);
        StartPunishmentCommand = ReactiveCommand.Create<PunishmentItemViewModel>(OnStartPunishment);
        SoftenPunishmentCommand = ReactiveCommand.CreateFromTask<PunishmentItemViewModel>(OnSoftenPunishment);
        CancelPunishmentCardCommand = ReactiveCommand.CreateFromTask<PunishmentItemViewModel>(OnCancelPunishmentCard);
        RequestPermissionCommand = ReactiveCommand.Create(() =>
        {
            RefreshAvailablePermissionCards();
            IsPermissionsPanelVisible = true;
        });
        ClosePermissionsPanelCommand = ReactiveCommand.Create(() => IsPermissionsPanelVisible = false);
        StartGrantedBreakCommand = ReactiveCommand.Create(() =>
        {
            IsPermissionOutcomeVisible = false;
            IsPermissionBreakActive = true;
        });
        DismissPermissionOutcomeCommand = ReactiveCommand.Create(() => IsPermissionOutcomeVisible = false);
        EndPermissionBreakCommand = ReactiveCommand.Create(OnEndPermissionBreak);
        OpenShopCommand = ReactiveCommand.Create(() =>
        {
            _sessionLoop.Pause();
            _countdownTimer?.Stop();
            _mainVm.NavigateTo(new ShopViewModel(_mainVm, _saveRepo));
        });
        if (startChore is not null && _saveRepo.GetChores().Any(c => c.Id == startChore.Id))
        {
            _activeChoreId = startChore.Id;
            _sessionLoop.Start();
            EnterBusyMode();
        }
        else
        {
            StartIdleMode();
        }
    }

    public string InitialDebtDisplay { get; }
    public string OutfitMultiplierDisplay { get; }

    public bool CanComplete => CurrentDebt <= 0;
    public decimal DebtRemaining => DebtTotal - DebtSettled;
    public double DebtProgress => DebtTotal > 0 ? Math.Clamp((double)(DebtSettled / DebtTotal), 0.0, 1.0) : 0.0;
    public bool HasPendingPunishments => PendingPunishmentsCount > 0;

    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];
    public ObservableCollection<PunishmentItemViewModel> Punishments { get; } = [];
    public ObservableCollection<PermissionTypeItemViewModel> PermissionTypes { get; } = [];
    public ObservableCollection<PermissionCardChipViewModel> AvailablePermissionCards { get; } = [];
    public bool HasAvailablePermissionCards => AvailablePermissionCards.Count > 0;

    public string CurrentOutfit { get; }

    public ICommand EndSessionCommand { get; }
    public ICommand CompleteGameCommand { get; }
    public ICommand DismissChoreResultCommand { get; }
    public ICommand EndChoreCommand { get; }

    // --- New dashboard commands ---
    public ICommand ToggleOutfitDrawerCommand { get; }
    public ICommand OpenPunishmentsCommand { get; }
    public ICommand ClosePunishmentsPanelCommand { get; }
    public ICommand OpenAddTaskFormCommand { get; }
    public ICommand CloseAddTaskFormCommand { get; }
    public ICommand SubmitAddTaskCommand { get; }
    public ICommand StartPunishmentCommand { get; }
    public ICommand SoftenPunishmentCommand { get; }
    public ICommand CancelPunishmentCardCommand { get; }
    public ICommand RequestPermissionCommand { get; }
    public ICommand ClosePermissionsPanelCommand { get; }
    public ICommand StartGrantedBreakCommand { get; }
    public ICommand DismissPermissionOutcomeCommand { get; }
    public ICommand EndPermissionBreakCommand { get; }
    public ICommand OpenShopCommand { get; }

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
        Tasks.Clear();
        foreach (var chore in _saveRepo.GetChores())
        {
            Tasks.Add(new TaskItemViewModel(chore, OnStartTask));
        }
    }

    private void RefreshPunishments()
    {
        Punishments.Clear();
        var owned = _saveRepo.GetInventory().Select(c => c.CardId).ToHashSet();
        var canSoften = owned.Contains(new CardId(CardType.SoftenPunishment, 0));
        var canCancel = owned.Contains(new CardId(CardType.CancelPunishment, 0));
        var activeForcedOutfitId = _saveRepo.GetActiveForcedOutfitPunishment()?.Id;
        foreach (var punishment in _saveRepo.GetActivePunishments())
        {
            var isActiveForcedOutfit = punishment.Kind == PunishmentKind.ForcedOutfit &&
                punishment.Id == activeForcedOutfitId;
            Punishments.Add(new PunishmentItemViewModel(punishment, canSoften, canCancel, isActiveForcedOutfit));
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

    private void RefreshOutfitDisplay()
    {
        var state = _saveRepo.GetGameState();
        var active = _saveRepo.GetActivePlugAndPlayAccessory();
        var pnpItem = active is null ? null : _saveRepo.GetWardrobe().FirstOrDefault(i => i.Id == active.ClothingItemId);
        CurrentOutfitVm = OutfitViewModel.FromString(state?.CurrentOutfit, pnpItem);
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

    private void ShowChoreResultMessage(string message)
    {
        ChoreResultMessage = message;
        IsChoreResultVisible = true;

        _choreResultTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _choreResultTimer.Tick += (_, _) => DismissChoreResult();
        _choreResultTimer.Start();
    }

    private void DismissChoreResult()
    {
        if (!IsChoreResultVisible)
        {
            return;
        }

        _choreResultTimer?.Stop();
        _choreResultTimer = null;
        IsChoreResultVisible = false;
    }

    // --- Chore start → naviga a CameraSetupView ---
    private void OnStartChore(int choreId)
    {
        if (IsBusy)
        {
            return;
        }

        ChoreResultMessage = "";

        // Stop the wage timer before navigating away; keep check-in popups (Hydration, Plug and Play) running.
        _sessionLoop.Pause(pausePopups: false);
        _countdownTimer?.Stop();

        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == choreId);
        if (chore is null)
        {
            return;
        }

        // Webcam available — navigate to camera setup
        if (WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex))
        {
            // CameraSetupViewModel replaces this instance with a brand-new DashboardViewModel on
            // both Confirm and Cancel (see its constructor) — NavigateTo never disposes the page it
            // replaces, so without a full stop here this instance's popup loop would keep running
            // unattended in the background (no view left to show what it triggers) for the rest of
            // the session.
            _sessionLoop.Pause();
            _mainVm.NavigateTo(new CameraSetupViewModel(_mainVm, _saveRepo, chore));
        }
        else
        {
            // No webcam — enter busy mode directly
            _activeChoreId = chore.Id;
            EnterBusyMode();
        }
    }

    private void EnterBusyMode()
    {
        var chore = _activeChoreId is null
            ? null
            : _saveRepo.GetChores().FirstOrDefault(c => c.Id == _activeChoreId);
        if (chore is null)
        {
            return;
        }

        _sessionLoop.Pause(pausePopups: false);
        _countdownTimer?.Stop();
        IsIdle = false;
        IsBusy = true;
        BusyChoreName = chore.Name;
        BusyChoreDescription = chore.Description;
        BusyChoreValueDisplay = CurrencyConverter.Format(Math.Round(chore.Value * (decimal)_outfitBonus, 2));
        _choreStartTime = DateTime.UtcNow;
        _choreTargetMinutes = chore.MinCompletionMinutes * (0.85 + _random.NextDouble() * 0.30);
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
        if (!IsBusy || _activeChoreId is null)
        {
            return;
        }

        // Cancel the still-running interrupt-popup loop first: it kept ticking during busy mode
        // (EnterBusyMode pauses only the wage timer), so without this a Hydration/Plug&Play popup
        // can land in the same instant the user ends the chore, ringing the desk bell right when
        // they're closing something of their own accord.
        _sessionLoop.Pause();

        _choreTimer?.Stop();
        _choreTimer = null;
        StopSilentCapture();

        var elapsed = DateTime.UtcNow - _choreStartTime;
        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == _activeChoreId);
        var tooEarly = chore is not null && elapsed.TotalMinutes < _choreTargetMinutes;
        var tooSlow = chore is not null && !tooEarly && elapsed.TotalMinutes > _choreTargetMinutes * 1.35;

        if (tooEarly || tooSlow)
        {
            var minutesOff = tooEarly
                ? _choreTargetMinutes - elapsed.TotalMinutes
                : elapsed.TotalMinutes - _choreTargetMinutes * 1.35;
            var severity = 5 + (int)Math.Ceiling(minutesOff);

            var reason = (chore!.IsOccasional, tooEarly) switch
            {
                (true, true) => "PunishmentReasonOccasionalTaskTooEarly",
                (true, false) => "PunishmentReasonOccasionalTaskTooSlow",
                (false, true) => "PunishmentReasonChoreTooEarly",
                (false, false) => "PunishmentReasonChoreTooSlow",
            };
            _saveRepo.AssignPunishment((severity, severity), reason);
            RefreshPunishments();
            ShowChoreResultMessage((chore.IsOccasional, tooEarly) switch
            {
                (true, true) => Resources.Get("DashboardOccasionalTaskRejectedMessage"),
                (true, false) => Resources.Get("DashboardOccasionalTaskTooSlowRejectedMessage"),
                (false, true) => Resources.Get("DashboardChoreRejectedMessage"),
                (false, false) => Resources.Get("DashboardChoreTooSlowRejectedMessage"),
            });
        }
        else
        {
            var state = _saveRepo.GetGameState();
            if (state is not null && chore is not null)
            {
                var credit = chore.IsOccasional
                    ? Math.Round(2m * (_settings.IdleWageAmount / _settings.IdleWageIntervalMinutes) *
                        (decimal)elapsed.TotalMinutes * (decimal)_outfitBonus, 0)
                    : Math.Round(chore.Value * (decimal)_outfitBonus, 0);
                state.CurrentDebt -= credit;
                _saveRepo.SaveGameState(state);
            }

            if (chore is not null)
            {
                if (chore.IsOccasional)
                {
                    _saveRepo.DeleteChore(chore.Id);
                }
                else
                {
                    _saveRepo.UpdateChoreCompletion(chore.Id, DateTime.UtcNow);
                }
            }

            ChoreResultMessage = "";
        }

        _activeChoreId = null;
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
        var slotName = _saveRepo.GetGameState()?.SlotName ?? "unknown";
        SilentCaptureLoop.Start(_settings, slotName, "chore", _captureCts.Token);
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
        OnStartChore(task.ChoreId);
    }

    private void OnSubmitAddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName) || NewTaskEstimatedMinutes <= 0)
        {
            return;
        }

        var ratePerMinute = _settings.IdleWageAmount / _settings.IdleWageIntervalMinutes;
        var chore = new Chore
        {
            Name = NewTaskName.Trim(),
            Description = NewTaskDescription.Trim(),
            Value = Math.Round(2m * ratePerMinute * NewTaskEstimatedMinutes, 2),
            MinCompletionMinutes = NewTaskEstimatedMinutes,
            IsOccasional = true
        };

        _saveRepo.AddChore(chore);
        IsAddTaskFormVisible = false;
        LoadChores();
    }

    /// <summary>Rebuilds the staged-card chip list from the owned permission cards. Discards any prior staging.</summary>
    private void RefreshAvailablePermissionCards()
    {
        AvailablePermissionCards.Clear();
        var owned = _saveRepo.GetInventory()
            .Select(c => c.CardId)
            .Where(id => CardCatalog.IsPermissionCard(id.Type))
            .Distinct();
        foreach (var id in owned)
        {
            AvailablePermissionCards.Add(new PermissionCardChipViewModel(id));
        }

        this.RaisePropertyChanged(nameof(HasAvailablePermissionCards));
    }

    private static PermissionModifiers ToPermissionModifiers(CardId id)
    {
        return id.Type switch
        {
            CardType.CooldownBypass => new PermissionModifiers(true, 0, 0, false),
            CardType.DurationBoost => new PermissionModifiers(false, 0, id.Variant, false),
            CardType.ProbabilityBoost => new PermissionModifiers(false, id.Variant, 0, false),
            CardType.GrantPermission => new PermissionModifiers(false, 0, 0, true),
            _ => PermissionModifiers.None,
        };
    }

    private void OnRequestPermission(PermissionKind kind)
    {
        // Cards staged in the Permissions panel are consumed here, regardless of the request's outcome.
        var mods = PermissionModifiers.None;
        foreach (var chip in AvailablePermissionCards.Where(c => c.IsStaged).ToList())
        {
            if (_saveRepo.ConsumeCard(chip.Id))
            {
                mods = mods.CombinedWith(ToPermissionModifiers(chip.Id));
            }
        }

        var result = _saveRepo.RequestPermission(kind, mods);
        PermissionOutcomeMessage = Resources.Get(result.PhraseKey);
        IsPermissionOutcomeGranted = result.Outcome == PermissionRequestOutcome.Granted;

        IsPermissionsPanelVisible = false;
        IsPermissionOutcomeVisible = true;

        if (IsPermissionOutcomeGranted)
        {
            _sessionLoop.Pause();
            _countdownTimer?.Stop();
            PermissionBreakKindName = PermissionTypeItemViewModel.GetName(kind);
        }
    }

    private void OnEndPermissionBreak()
    {
        _saveRepo.EndPermissionBreak();
        RefreshPunishments();
        RefreshDebt();
        IsPermissionBreakActive = false;
        _sessionLoop.Resume();
        _countdownTimer?.Start();
    }

    private void OnStartPunishment(PunishmentItemViewModel punishmentVm)
    {
        IsPunishmentsPanelVisible = false;

        var punishment = _saveRepo.GetActivePunishments().FirstOrDefault(p => p.Id == punishmentVm.PunishmentId);
        if (punishment is null)
        {
            return;
        }

        if (punishment.Kind == PunishmentKind.ForcedOutfit)
        {
            var dialog = new ConfirmDialogViewModel(
                Resources.Get("ForcedOutfitInfoTitle"),
                string.Format(Resources.Get("ForcedOutfitInfoMessageFormat"),
                    punishment.ForcedOutfitPresetName ?? Resources.Get("ForcedOutfitPresetNameFallback"),
                    PunishmentItemViewModel.FormatRemainingTime(punishment)),
                Resources.Get("Ok"),
                null);
            _ = ShowConfirmAsync(dialog);
            return;
        }

        _sessionLoop.Pause();
        _countdownTimer?.Stop();

        switch (punishment.Kind)
        {
            case PunishmentKind.Cornertime:
                if (WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex))
                {
                    _mainVm.NavigateTo(new CornertimePunishmentViewModel(_mainVm, _saveRepo, punishment));
                }
                else
                {
                    _sessionLoop.Resume();
                    _countdownTimer?.Start();
                }

                break;

            case PunishmentKind.WritingLines:
                _mainVm.NavigateTo(new WritingLinesPunishmentViewModel(_mainVm, _saveRepo, punishment));
                break;
        }
    }

    private async Task OnSoftenPunishment(PunishmentItemViewModel punishmentVm)
    {
        var dialog = new ConfirmDialogViewModel(
            Resources.Get("CardSoftenPunishmentName"),
            string.Format(Resources.Get("PunishmentCardConfirmSoftenFormat"), punishmentVm.Name),
            Resources.Get("Confirm"),
            Resources.Get("Cancel"));
        var confirmed = await ShowConfirmAsync(dialog);
        if (!confirmed)
        {
            return;
        }

        if (_saveRepo.ConsumeCard(new CardId(CardType.SoftenPunishment, 0)))
        {
            _saveRepo.SoftenPunishment(punishmentVm.PunishmentId);
            RefreshPunishments();
        }
    }

    private async Task OnCancelPunishmentCard(PunishmentItemViewModel punishmentVm)
    {
        var dialog = new ConfirmDialogViewModel(
            Resources.Get("CardCancelPunishmentName"),
            string.Format(Resources.Get("PunishmentCardConfirmCancelFormat"), punishmentVm.Name),
            Resources.Get("Confirm"),
            Resources.Get("Cancel"));
        var confirmed = await ShowConfirmAsync(dialog);
        if (!confirmed)
        {
            return;
        }

        if (_saveRepo.ConsumeCard(new CardId(CardType.CancelPunishment, 0)))
        {
            _saveRepo.CancelPunishment(punishmentVm.PunishmentId);
            RefreshPunishments();
        }
    }

    private void EndSession()
    {
        _sessionLoop.Stop();
        _countdownTimer?.Stop();
        _popupOutcomeTimer?.Stop();
        _choreResultTimer?.Stop();
        _poseCountdownTimer?.Stop();
        _curtsyCountdownTimer?.Stop();
        _curtsyRecordingCts?.Cancel();
        _curtsyRecordingCts?.Dispose();
        _quoteListenTimer?.Stop();
        _quoteListenCts?.Cancel();
        _quoteListenCts?.Dispose();
        _kneelCountdownTimer?.Stop();
        _kneelCts?.Cancel();
        _kneelCts?.Dispose();
        _choreTimer?.Stop();
        StopSilentCapture();
        _mainVm.Webcam.Stop();
        CapturedPosePhoto?.Dispose();
        _autoSave.StopTimer();

        var activePlugAndPlay = _saveRepo.GetActivePlugAndPlayAccessory();
        if (activePlugAndPlay is not null)
        {
            var state = _saveRepo.GetGameState();
            if (state is not null)
            {
                var remainingMinutes = Math.Max(0, (activePlugAndPlay.RemoveAfterUtc - DateTime.UtcNow).TotalMinutes);
                state.PendingPlugAndPlaySeedItemId = activePlugAndPlay.ClothingItemId;
                state.PendingPlugAndPlayRemainingMinutes = remainingMinutes;
                _saveRepo.SaveGameState(state);
            }

            _saveRepo.ClearActivePlugAndPlayAccessory();
        }

        _mainVm.NavigateTo(new EndSessionViewModel(_mainVm, _saveRepo));
    }
}
