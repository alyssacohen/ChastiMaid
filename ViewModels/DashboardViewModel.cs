using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class ChoreItemViewModel : ViewModelBase
{
    public int ChoreId { get; }
    public string Name { get; }
    public string Description { get; }
    public decimal Value { get; }
    public ICommand StartChoreCommand { get; }

    public ChoreItemViewModel(Chore chore, Action<ChoreItemViewModel> onStart)
    {
        ChoreId = chore.Id;
        Name = chore.Name;
        Description = chore.Description;
        Value = chore.Value;
        StartChoreCommand = ReactiveCommand.Create(() => onStart(this));
    }
}

public class DashboardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly AutoSaveService _autoSave;
    private readonly SessionLoopService _sessionLoop;
    private readonly GameSettings _settings;
    private DispatcherTimer? _countdownTimer;

    private string _debtDisplay = "";
    public string DebtDisplay { get => _debtDisplay; set => this.RaiseAndSetIfChanged(ref _debtDisplay, value); }

    private decimal _currentDebt;
    public decimal CurrentDebt { get => _currentDebt; set => this.RaiseAndSetIfChanged(ref _currentDebt, value); }

    public bool CanComplete => CurrentDebt <= 0;

    private string _payRate = "";
    public string PayRate { get => _payRate; set => this.RaiseAndSetIfChanged(ref _payRate, value); }

    private string _timeToNextWage = "";
    public string TimeToNextWage { get => _timeToNextWage; set => this.RaiseAndSetIfChanged(ref _timeToNextWage, value); }

    private bool _isIdle = true;
    public bool IsIdle { get => _isIdle; set => this.RaiseAndSetIfChanged(ref _isIdle, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => this.RaiseAndSetIfChanged(ref _isBusy, value); }

    // --- Busy mode ---
    private string _busyChoreName = "";
    public string BusyChoreName { get => _busyChoreName; set => this.RaiseAndSetIfChanged(ref _busyChoreName, value); }

    private string _busyChoreDescription = "";
    public string BusyChoreDescription { get => _busyChoreDescription; set => this.RaiseAndSetIfChanged(ref _busyChoreDescription, value); }

    private decimal _busyChoreValue;
    public decimal BusyChoreValue { get => _busyChoreValue; set => this.RaiseAndSetIfChanged(ref _busyChoreValue, value); }

    private string _busyElapsed = "00:00";
    public string BusyElapsed { get => _busyElapsed; set => this.RaiseAndSetIfChanged(ref _busyElapsed, value); }

    // --- Trivia popup ---
    private bool _isPopupVisible;
    public bool IsPopupVisible { get => _isPopupVisible; set => this.RaiseAndSetIfChanged(ref _isPopupVisible, value); }

    private string _popupQuestion = "";
    public string PopupQuestion { get => _popupQuestion; set => this.RaiseAndSetIfChanged(ref _popupQuestion, value); }

    private ObservableCollection<string> _popupAnswers = [];
    public ObservableCollection<string> PopupAnswers { get => _popupAnswers; set => this.RaiseAndSetIfChanged(ref _popupAnswers, value); }

    private int _popupCountdown;
    public int PopupCountdown { get => _popupCountdown; set => this.RaiseAndSetIfChanged(ref _popupCountdown, value); }

    private int _correctAnswerIndex;

    public ObservableCollection<ChoreItemViewModel> Chores { get; } = [];

    public string CurrentOutfit { get; }

    public ICommand EndSessionCommand { get; }
    public ICommand CompleteGameCommand { get; }
    public ICommand AnswerCommand { get; }
    public ICommand EndChoreCommand { get; }

    private ChoreItemViewModel? _activeChore;
    private DateTime _choreStartTime;
    private DispatcherTimer? _choreTimer;
    private DispatcherTimer? _popupTimer;
    private CancellationTokenSource? _captureCts;
    private readonly Random _random = new();

    /// <summary>Costruttore standard — entra in idle mode.</summary>
    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
        : this(mainVm, saveRepo, null) { }

    /// <summary>Se startChore non è null, entra direttamente in busy mode.</summary>
    public DashboardViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Chore? startChore)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _autoSave = mainVm.AutoSaveService;
        _settings = saveRepo.GetGameSettings();

        _autoSave.Bind(saveRepo);
        _autoSave.StartTimer(mainVm.ProfileRepository.GetProfile().AutoSaveIntervalMinutes);

        var state = saveRepo.GetGameState();
        CurrentDebt = state?.CurrentDebt ?? 0;
        DebtDisplay = FormatDebt(CurrentDebt);
        CurrentOutfit = state?.CurrentOutfit ?? "No outfit set";
        PayRate = $"Wage: {_settings.IdleWageAmount} cr / {_settings.IdleWageIntervalMinutes} min";

        foreach (var chore in saveRepo.GetChores())
            Chores.Add(new ChoreItemViewModel(chore, OnStartChore));

        _sessionLoop = new SessionLoopService(saveRepo, _settings);
        _sessionLoop.OnIdleWagePaid = () => Dispatcher.UIThread.Post(RefreshDebt);
        _sessionLoop.OnPopupTriggered = () => Dispatcher.UIThread.Post(ShowTriviaPopup);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateWageCountdown();

        EndSessionCommand = ReactiveCommand.Create(EndSession);
        CompleteGameCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new GameCompleteViewModel(_mainVm, _saveRepo)));
        AnswerCommand = ReactiveCommand.Create<string>(OnAnswer);
        EndChoreCommand = ReactiveCommand.Create(OnEndChore);

        if (startChore is not null)
        {
            // Trova il ChoreItemViewModel corrispondente
            _activeChore = Chores.FirstOrDefault(c => c.ChoreId == startChore.Id);
            if (_activeChore is not null)
                EnterBusyMode();
            else
                StartIdleMode();
        }
        else
        {
            StartIdleMode();
        }
    }

    private void StartIdleMode()
    {
        _sessionLoop.Start();
        _countdownTimer?.Start();
    }

    private static string FormatDebt(decimal debt) => $"Debt: {debt:F0} credits";

    private void RefreshDebt()
    {
        var state = _saveRepo.GetGameState();
        CurrentDebt = state?.CurrentDebt ?? 0;
        DebtDisplay = FormatDebt(CurrentDebt);
        this.RaisePropertyChanged(nameof(CanComplete));
    }

    private void UpdateWageCountdown()
    {
        if (!IsIdle) return;
        var elapsed = DateTime.UtcNow - _sessionLoop.LastWageTime;
        var remaining = TimeSpan.FromMinutes(_sessionLoop.WageIntervalMinutes) - elapsed;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        TimeToNextWage = $"Next wage in {remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    // --- Trivia popup ---
    private void ShowTriviaPopup()
    {
        if (IsPopupVisible || IsBusy) return;

        var trivia = TriviaBank.GetRandom();
        PopupQuestion = trivia.Question;
        PopupAnswers = new ObservableCollection<string>(trivia.Answers);
        _correctAnswerIndex = trivia.CorrectIndex;
        PopupCountdown = _settings.PopupTimeoutSeconds;
        IsPopupVisible = true;

        if (OperatingSystem.IsWindows())
#pragma warning disable CA1416
            Task.Run(() => Console.Beep(800, 300));
#pragma warning restore CA1416

        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _popupTimer.Tick += (_, _) =>
        {
            PopupCountdown--;
            if (PopupCountdown <= 0) ClosePopup(false);
        };
        _popupTimer.Start();
    }

    private void OnAnswer(string answer)
    {
        if (!IsPopupVisible) return;
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
        if (IsBusy) return;

        // Ferma i timer prima di navigare via
        _sessionLoop.Pause();
        _countdownTimer?.Stop();

        var chore = _saveRepo.GetChores().FirstOrDefault(c => c.Id == choreVm.ChoreId);
        if (chore is null) return;

        // Se la webcam è disponibile, naviga alla vista camera setup
        if (MediaCaptureService.DetectWebcam())
        {
            _mainVm.NavigateTo(new CameraSetupViewModel(_mainVm, _saveRepo, chore));
        }
        else
        {
            // Nessuna webcam — entra direttamente in busy mode
            _activeChore = choreVm;
            EnterBusyMode();
        }
    }

    private void EnterBusyMode()
    {
        if (_activeChore is null) return;

        _sessionLoop.Pause();
        _countdownTimer?.Stop();
        IsIdle = false;
        IsBusy = true;
        BusyChoreName = _activeChore.Name;
        BusyChoreDescription = _activeChore.Description;
        BusyChoreValue = _activeChore.Value;
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
        if (!IsBusy || _activeChore is null) return;

        _choreTimer?.Stop();
        _choreTimer = null;
        StopSilentCapture();

        var state = _saveRepo.GetGameState();
        if (state is not null)
        {
            state.CurrentDebt = Math.Max(0, state.CurrentDebt - _activeChore.Value);
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
                var delaySec = _random.Next(_settings.BusyCaptureMinIntervalSeconds, _settings.BusyCaptureMaxIntervalSeconds + 1);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                if (ct.IsCancellationRequested) break;

                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    if (_random.Next(2) == 0)
                    {
                        var path = Path.Combine(dir, $"chore_{timestamp}.png");
                        await MediaCaptureService.CaptureFrameAsync(path, ct);
                    }
                    else
                    {
                        var duration = _random.Next(3, 11);
                        var path = Path.Combine(dir, $"chore_{timestamp}.avi");
                        await MediaCaptureService.RecordVideoAsync(path, duration, ct: ct);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* Webcam non disponibile — ignora */ }
            }
        }, ct);
    }

    private void StopSilentCapture()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = null;
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

        var sessions = _saveRepo.GetSessions();
        var current = sessions.LastOrDefault(s => s.EndedAt is null);
        if (current is not null)
        {
            current.EndedAt = DateTime.UtcNow;
            current.DebtAtEnd = _saveRepo.GetGameState()?.CurrentDebt ?? 0;
            _saveRepo.UpdateSession(current);
        }

        _autoSave.TriggerSave();
        _mainVm.CloseActiveSave();
        _mainVm.NavigateTo(new WelcomeViewModel(_mainVm));
    }
}
