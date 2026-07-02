using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Views.Dialogs;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

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
        ValueDisplay = Program.TestMode
            ? $"${effective:F2} (${chore.Value:F2} × {multiplier:F2})"
            : $"${effective:F2}";
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
    private DateTime _choreStartTime;
    private DispatcherTimer? _choreTimer;

    private int _correctAnswerIndex;

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

    // --- Trivia popup ---
    [Reactive] private bool _isPopupVisible;

    [Reactive] private string _payRate = "";

    // --- Pending punishments ---
    [Reactive] private int _pendingPunishmentsCount;
    [Reactive] private ObservableCollection<string> _popupAnswers = [];
    [Reactive] private int _popupCountdown;
    [Reactive] private string _popupQuestion = "";
    private DispatcherTimer? _popupTimer;
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
            state.LastSessionAt = UtcNow();
            saveRepo.SaveGameState(state);
        }

        CurrentDebt = state?.CurrentDebt ?? 0;
        DebtDisplay = FormatDebt(CurrentDebt);
        InitialDebtDisplay = $"Initial: ${state?.InitialDebt ?? 0:F2}";
        CurrentOutfit = state?.CurrentOutfit ?? "No outfit set";

        _outfitBonus = state?.EffectiveMultiplier ?? 1.0;
        var effectiveWage = Math.Round(_settings.IdleWageAmount * (decimal)_outfitBonus, 2);
        PayRate = Program.TestMode
            ? $"Wage: ${effectiveWage:F2} / {_settings.IdleWageIntervalMinutes} min (${_settings.IdleWageAmount:F2} × {_outfitBonus:F2})"
            : $"Wage: ${effectiveWage:F2} / {_settings.IdleWageIntervalMinutes} min";

        foreach (var chore in saveRepo.GetChores())
        {
            Chores.Add(new ChoreItemViewModel(chore, _outfitBonus, OnStartChore));
            Tasks.Add(new TaskItemViewModel(chore, OnStartTask));
        }

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
        _sessionLoop.OnPopupTriggered = () => Dispatcher.UIThread.Post(ShowTriviaPopup);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateWageCountdown();

        EndSessionCommand = ReactiveCommand.Create(EndSession);
        CompleteGameCommand =
            ReactiveCommand.Create(() => _mainVm.NavigateTo(new GameCompleteViewModel(_mainVm, _saveRepo)));
        AnswerCommand = ReactiveCommand.Create<string>(OnAnswer);
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

    public ICommand EndSessionCommand { get; }
    public ICommand CompleteGameCommand { get; }
    public ICommand AnswerCommand { get; }
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
        return $"Debt: ${debt:F2}";
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

        var elapsed = UtcNow() - _sessionLoop.LastWageTime;
        var interval = TimeSpan.FromMinutes(_sessionLoop.WageIntervalMinutes);
        var remaining = interval - elapsed;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        TimeToNextWage = $"Next wage in {remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    // --- Trivia popup ---
    private void ShowTriviaPopup()
    {
        if (IsPopupVisible || IsBusy)
        {
            return;
        }

        var trivia = TriviaBank.GetRandom();
        PopupQuestion = trivia.Question;
        PopupAnswers = new ObservableCollection<string>(trivia.Answers);
        _correctAnswerIndex = trivia.CorrectIndex;
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
#pragma warning disable CA1416
        {
            Task.Run(() => Console.Beep(800, 300));
        }
#pragma warning restore CA1416

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0)
            {
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

        var correct = _correctAnswerIndex < PopupAnswers.Count && PopupAnswers[_correctAnswerIndex] == answer;
        ClosePopup(correct);
    }

    private void ClosePopup(bool correct)
    {
        _popupTimer?.Stop();
        _popupTimer = null;
        IsPopupVisible = false;
        _sessionLoop.ReportPopupResult(correct);
    }

    // --- Chore start → naviga a CameraSetupView ---
    private void OnStartChore(ChoreItemViewModel choreVm)
    {
        if (IsBusy)
        {
            return;
        }

        // Stop timers before navigating away
        _sessionLoop.Pause();
        _countdownTimer?.Stop();

        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == choreVm.ChoreId);
        if (chore is null)
        {
            return;
        }

        // Webcam available — navigate to camera setup
        if (WebcamPreviewService.DetectWebcam())
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
        BusyChoreValueDisplay = Program.TestMode
            ? $"${Math.Round(_activeChore.Value * (decimal)_outfitBonus, 2):F2} (${_activeChore.Value:F2} × {_outfitBonus:F2})"
            : $"${Math.Round(_activeChore.Value * (decimal)_outfitBonus, 2):F2}";
        _choreStartTime = UtcNow();
        BusyElapsed = "00:00";

        _choreTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _choreTimer.Tick += (_, _) =>
        {
            var elapsed = UtcNow() - _choreStartTime;
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

        var state = _saveRepo.GetGameState();
        if (state is not null)
        {
            var credit = Math.Round(_activeChore.Value * (decimal)_outfitBonus, 0);
            state.CurrentDebt = Math.Max(0, state.CurrentDebt - credit);
            _saveRepo.SaveGameState(state);
        }

        _activeChore = null;
        IsBusy = false;
        IsIdle = true;
        RefreshDebt();

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
                        await WebcamPreviewService.CaptureFrameStandaloneAsync(path, ct);
                    }
                    else
                    {
                        var duration = _random.Next(3, 11);
                        var path = Path.Combine(dir, $"chore_{timestamp}.avi");
                        await WebcamPreviewService.RecordVideoStandaloneAsync(path, duration, ct: ct);
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
        _choreTimer?.Stop();
        StopSilentCapture();
        _mainVm.Webcam.Stop();
        _autoSave.StopTimer();

        _mainVm.NavigateTo(new EndSessionViewModel(_mainVm, _saveRepo));
    }
}
