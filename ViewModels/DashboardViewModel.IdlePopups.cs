using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Idle-popup overlay: the check-in popups that interrupt idle mode (ImpossibleQuestion, Hydration,
///     PoseForCamera, SelfCheck, Curtsy, MotivationalQuote, Kneel, Plug and Play Don/Remove). Split out of
///     <see cref="DashboardViewModel" /> purely for file-level organization — this is still the same type,
///     so popup code keeps calling directly into dashboard-proper state (<c>RefreshPunishments</c>,
///     <c>RefreshOutfitDisplay</c>, <c>_sessionLoop</c>, <c>IsBusy</c>/<c>IsIdle</c>) rather than through
///     any new abstraction. That coupling is intentional: punishments and the outfit drawer genuinely need
///     to react to popup outcomes, and the session loop both drives popups and consumes their result.
/// </summary>
public partial class DashboardViewModel
{
    // --- Idle popup: core dispatch state ---
    [Reactive] private bool _isPopupVisible;
    [Reactive] private bool _isShowingPopupOutcome;
    [Reactive] private ObservableCollection<string> _popupAnswers = [];
    [Reactive] private string _popupOutcomeMessage = "";
    [Reactive] private string _popupQuestion = "";
    private DispatcherTimer? _popupOutcomeTimer;
    private DateTime _popupShownAt;
    private int _popupSecondsLate;
    private bool _pendingOutcomePositive;
    private IdlePopupKind _currentPopupKind;
    private DateTime _lastHydrationPopupAt = DateTime.MinValue;
    private ImpossibleQuestion? _currentQuestion;

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

    // --- Idle popup: Self-check (quizzes a past Pose for Camera photo) ---
    [Reactive] private bool _isSelfCheckPopupActive;
    [Reactive] private Bitmap? _selfCheckPhoto;
    private PoseCheckRecord? _currentSelfCheckRecord;
    private ClothingItem? _currentSelfCheckQueriedItem;

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
    [Reactive] private bool _isQuoteProcessing;
    [Reactive] private bool _isQuoteTypingActive;
    [Reactive] private int _quoteListenCountdown;
    [Reactive] private int _quoteAttemptsRemaining;
    [Reactive] private string _quoteTypedAnswer = "";
    [Reactive] private string _currentQuoteText = "";
    private const int QuoteMaxAttempts = 3;
    private DispatcherTimer? _quoteListenTimer;
    private CancellationTokenSource? _quoteListenCts;

    // --- Idle popup: Kneel ---
    [Reactive] private bool _isKneelPopupActive;
    [Reactive] private bool _isKneelFramingActive;
    [Reactive] private bool _isKneelActive;
    [Reactive] private int _kneelCountdown;
    private DispatcherTimer? _kneelCountdownTimer;
    private CancellationTokenSource? _kneelCts;

    // --- Idle popup: Plug and Play ---
    private ClothingItem? _currentPlugAndPlayCandidate;

    public WebcamPreviewService Webcam => _mainVm.Webcam;

    public bool IsShowingQuestionPanel =>
        !IsShowingPopupOutcome && !IsPoseCountdownActive && !IsCurtsyCountdownActive && !IsCurtsyRecordingActive &&
        !IsQuoteListeningActive && !IsQuoteTypingActive && !IsKneelFramingActive && !IsKneelActive;

    public bool IsWebcamPreviewNeededInQuestionPanel => IsPoseForCameraPopupActive || IsCurtsyPopupActive;

    public ICommand AnswerCommand { get; }
    public ICommand DismissPopupOutcomeCommand { get; }
    public ICommand SubmitQuoteTypedAnswerCommand { get; }
    public ICommand ConfirmKneelFramingCommand { get; }

    private enum IdlePopupKind
    {
        ImpossibleQuestion,
        Hydration,
        PoseForCamera,
        SelfCheck,
        Curtsy,
        MotivationalQuote,
        Kneel,
        PlugAndPlayDon,
        PlugAndPlayRemove
    }

    private static readonly IdlePopupKind[] EnabledIdlePopupKinds =
    [
        IdlePopupKind.ImpossibleQuestion,
        IdlePopupKind.Hydration,
        IdlePopupKind.PoseForCamera,
        IdlePopupKind.SelfCheck,
        IdlePopupKind.Curtsy,
        IdlePopupKind.MotivationalQuote,
        IdlePopupKind.Kneel,
        IdlePopupKind.PlugAndPlayDon,
        IdlePopupKind.PlugAndPlayRemove
    ];

    // Popups allowed to interrupt a task in progress (IsBusy) — everything else requires full attention.
    private static readonly IdlePopupKind[] InterruptibleDuringTaskKinds =
    [
        IdlePopupKind.Hydration,
        IdlePopupKind.PlugAndPlayDon,
        IdlePopupKind.PlugAndPlayRemove
    ];

    private bool IsHydrationAvailable()
    {
        var intervalMinutes = _settings.HydrationMinIntervalMinutes;
        if (_saveRepo.GetGameState()?.DiaperPlayActiveThisSession == true)
        {
            intervalMinutes = Math.Max(1, intervalMinutes / 2);
        }

        return _settings.HydrationEnabled &&
            DateTime.UtcNow - _lastHydrationPopupAt >= TimeSpan.FromMinutes(intervalMinutes);
    }

    private bool IsPoseForCameraAvailable()
    {
        var itemIds = _saveRepo.GetGameState()?.CurrentOutfitItemIds;
        var hasOutfitItems = itemIds is not null && itemIds.Count > 0;
        var hasActivePlugAndPlay = _saveRepo.GetActivePlugAndPlayAccessory() is not null;
        if (!hasOutfitItems && !hasActivePlugAndPlay)
        {
            return false;
        }

        return WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex);
    }

    private bool IsSelfCheckAvailable()
    {
        return _saveRepo.GetPendingSelfCheckablePoseChecks().Count > 0;
    }

    private bool ConflictsWithCurrentOutfit(ClothingItem candidate)
    {
        var wardrobe = _saveRepo.GetWardrobe();
        var wornIds = _saveRepo.GetGameState()?.CurrentOutfitItemIds ?? [];
        var worn = wardrobe.Where(i => wornIds.Contains(i.Id)).ToList();

        if (worn.Any(w => w.Layers.Intersect(candidate.Layers).Any() && w.Slots.Intersect(candidate.Slots).Any()))
        {
            return true;
        }

        var engine = new OutfitRuleEngine(_saveRepo.GetOutfitRules());
        return engine.ConflictsWithAny(candidate, worn);
    }

    private IEnumerable<ClothingItem> GetEligiblePlugAndPlayCandidates()
    {
        var wardrobe = _saveRepo.GetWardrobe();
        return _saveRepo.GetPlugAndPlayItems()
            .Select(c => wardrobe.FirstOrDefault(i => i.Id == c.ClothingItemId))
            .Where(item => item is not null && !ConflictsWithCurrentOutfit(item))!;
    }

    private bool IsPlugAndPlayDonAvailable()
    {
        if (!_settings.PlugAndPlayEnabled || _saveRepo.GetActivePlugAndPlayAccessory() is not null)
        {
            return false;
        }

        var forced = _saveRepo.GetActiveForcedOutfitPunishment();
        if (forced is { ForceFullOutfit: true })
        {
            return false;
        }

        return GetEligiblePlugAndPlayCandidates().Any();
    }

    private bool IsPlugAndPlayRemoveAvailable()
    {
        var active = _saveRepo.GetActivePlugAndPlayAccessory();
        return active is not null && DateTime.UtcNow >= active.RemoveAfterUtc;
    }

    private bool IsKindAvailable(IdlePopupKind kind)
    {
        return kind switch
        {
            IdlePopupKind.Hydration => IsHydrationAvailable(),
            IdlePopupKind.PoseForCamera => IsPoseForCameraAvailable(),
            IdlePopupKind.SelfCheck => IsSelfCheckAvailable(),
            IdlePopupKind.Curtsy => _settings.CurtsyEnabled && WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex),
            IdlePopupKind.MotivationalQuote => _settings.QuoteReadingEnabled,
            IdlePopupKind.Kneel => _settings.KneelEnabled && WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex),
            IdlePopupKind.PlugAndPlayDon => IsPlugAndPlayDonAvailable(),
            IdlePopupKind.PlugAndPlayRemove => IsPlugAndPlayRemoveAvailable(),
            _ => true
        };
    }

    private void ShowIdlePopup()
    {
        if (IsPopupVisible)
        {
            return;
        }

        var kinds = IsBusy ? InterruptibleDuringTaskKinds : EnabledIdlePopupKinds;
        var candidates = kinds.Where(IsKindAvailable).ToArray();
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
            case IdlePopupKind.SelfCheck:
                ShowSelfCheckPopup();
                break;
            case IdlePopupKind.Curtsy:
                ShowCurtsyPopup();
                break;
            case IdlePopupKind.MotivationalQuote:
                ShowMotivationalQuotePopup();
                break;
            case IdlePopupKind.Kneel:
                ShowKneelPopup();
                break;
            case IdlePopupKind.PlugAndPlayDon:
                ShowPlugAndPlayDonPopup();
                break;
            case IdlePopupKind.PlugAndPlayRemove:
                ShowPlugAndPlayRemovePopup();
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
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void ShowHydrationPopup()
    {
        _currentPopupKind = IdlePopupKind.Hydration;
        _lastHydrationPopupAt = DateTime.UtcNow;
        PopupQuestion = Resources.Get(HydrationReminderBank.GetRandom());
        PopupAnswers = [Resources.Get("HydrationConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void ShowPoseForCameraPopup()
    {
        var itemIds = _saveRepo.GetGameState()?.CurrentOutfitItemIds ?? [];
        var wardrobe = _saveRepo.GetWardrobe();
        var candidates = wardrobe.Where(i => itemIds.Contains(i.Id)).ToList();
        var activePlugAndPlay = _saveRepo.GetActivePlugAndPlayAccessory();
        if (activePlugAndPlay is not null)
        {
            var pnpItem = wardrobe.FirstOrDefault(i => i.Id == activePlugAndPlay.ClothingItemId);
            if (pnpItem is not null)
            {
                candidates.Add(pnpItem);
            }
        }

        if (candidates.Count == 0 || !_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            return;
        }

        _ = _mainVm.Webcam.RefreshAvailableCamerasAsync(_settings.SelectedCameraIndex);

        _currentPopupKind = IdlePopupKind.PoseForCamera;
        IsPoseForCameraPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        _currentPoseItem = candidates[_random.Next(candidates.Count)];
        PopupQuestion = string.Format(Resources.Get(PoseForCameraInstructionBank.GetRandom()), _currentPoseItem.Name);
        PopupAnswers = [Resources.Get("PoseForCameraConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    /// <summary>
    ///     Picks a pending Pose-for-Camera photo and quizzes whether a queried item is visible in it. The
    ///     queried item is weighted 40% toward the item actually requested at capture time (the only choice
    ///     that can trigger a punishment for answering "no") and 60% uniformly random from the wardrobe —
    ///     a uniform-only pick would rarely land on that item, making "always answer no" a nearly risk-free
    ///     way to game the check.
    /// </summary>
    private void ShowSelfCheckPopup()
    {
        var records = _saveRepo.GetPendingSelfCheckablePoseChecks();
        if (records.Count == 0)
        {
            return;
        }

        var record = records[_random.Next(records.Count)];
        var wardrobe = _saveRepo.GetWardrobe();

        var requestedItem = wardrobe.FirstOrDefault(i => i.Id == record.ItemId);
        var queriedItem = requestedItem is not null && _random.Next(100) < 40
            ? requestedItem
            : wardrobe.Count > 0 ? wardrobe[_random.Next(wardrobe.Count)] : requestedItem;

        if (queriedItem is null)
        {
            return;
        }

        try
        {
            SelfCheckPhoto = new Bitmap(record.PhotoPath);
        }
        catch
        {
            return;
        }

        _currentPopupKind = IdlePopupKind.SelfCheck;
        _currentSelfCheckRecord = record;
        _currentSelfCheckQueriedItem = queriedItem;
        IsSelfCheckPopupActive = true;
        PopupQuestion = string.Format(Resources.Get(SelfCheckInstructionBank.GetRandom()), queriedItem.Name);
        PopupAnswers = [Resources.Get("SelfCheckYes"), Resources.Get("SelfCheckNo")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void ShowCurtsyPopup()
    {
        if (!_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            return;
        }

        _ = _mainVm.Webcam.RefreshAvailableCamerasAsync(_settings.SelectedCameraIndex);

        _currentPopupKind = IdlePopupKind.Curtsy;
        IsCurtsyPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        PopupQuestion = Resources.Get(CurtsyInstructionBank.GetRandom());
        PopupAnswers = [Resources.Get("CurtsyConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void ShowMotivationalQuotePopup()
    {
        _currentPopupKind = IdlePopupKind.MotivationalQuote;
        CurrentQuoteText = Resources.Get(EmployerPhraseBank.GetRandom());
        PopupQuestion = Resources.Get("MotivationalQuoteInstruction");
        PopupAnswers = [Resources.Get("MotivationalQuoteConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void ShowKneelPopup()
    {
        _currentPopupKind = IdlePopupKind.Kneel;
        IsKneelPopupActive = true;
        PopupQuestion = Resources.Get("KneelSummon");
        PopupAnswers = [Resources.Get("KneelSummonConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();

        // Loading the voice model alone takes several seconds; kick it off now so it's ready by
        // the time the user confirms framing, instead of stalling the first narration line.
        _ = _mainVm.Tts.WarmUpAsync();
    }

    private void OnKneelConfirm()
    {
        if (!_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            PopupOutcomeMessage = Resources.Get("KneelFailedMessage");
            ShowPopupOutcome(true);
            return;
        }

        _ = _mainVm.Webcam.RefreshAvailableCamerasAsync(_settings.SelectedCameraIndex);

        IsKneelFramingActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
    }

    private void OnKneelFramingConfirm()
    {
        if (!IsKneelFramingActive)
        {
            return;
        }

        _mainVm.Webcam.Stop();
        IsKneelFramingActive = false;
        IsKneelActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        var totalSeconds = _random.Next(_settings.KneelMinDurationSeconds, _settings.KneelMaxDurationSeconds + 1);
        KneelCountdown = totalSeconds;
        PopupQuestion = Resources.Get(KneelInstructionBank.GetRandom());

        _kneelCts = new CancellationTokenSource();
        _ = _mainVm.Tts.SpeakAsync(PopupQuestion, _kneelCts.Token);

        _kneelCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _kneelCountdownTimer.Tick += (_, _) => KneelCountdown--;
        _kneelCountdownTimer.Start();

        RunKneelCaptureSessionAsync(totalSeconds);
    }

    private async void RunKneelCaptureSessionAsync(int totalSeconds)
    {
        var ct = _kneelCts!.Token;
        var slotName = _saveRepo.GetGameState()?.SlotName ?? "unknown";
        var dir = Path.Combine(AppPaths.Root, "data", "saves", slotName);
        var photos = new List<string>();
        var elapsed = 0;

        try
        {
            while (elapsed < totalSeconds)
            {
                var wait = Math.Min(
                    _random.Next(_settings.KneelPhotoMinIntervalSeconds, _settings.KneelPhotoMaxIntervalSeconds + 1),
                    totalSeconds - elapsed);
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                elapsed += wait;

                try
                {
                    var path = Path.Combine(dir, $"kneel_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                    if (await WebcamPreviewService.CaptureFrameStandaloneAsync(path, ct, _settings.SelectedCameraIndex))
                    {
                        photos.Add(path);
                    }
                }
                catch
                {
                    /* Webcam unavailable mid-session — ignore, no punishment */
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _kneelCountdownTimer?.Stop();
        _kneelCountdownTimer = null;
        _kneelCts?.Dispose();
        _kneelCts = null;
        IsKneelActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        SoundEffectService.PlayHandBell();

        if (photos.Count > 0)
        {
            _saveRepo.SaveKneelCheckRecord(new KneelCheckRecord
            {
                PhotoPaths = photos,
                StartedAt = DateTime.UtcNow.AddSeconds(-totalSeconds)
            });
        }

        PopupOutcomeMessage = Resources.Get(photos.Count > 0 ? "KneelCompletedMessage" : "KneelFailedMessage");
        ShowPopupOutcome(true);
    }

    private void ShowPlugAndPlayDonPopup()
    {
        var candidates = GetEligiblePlugAndPlayCandidates().ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        _currentPopupKind = IdlePopupKind.PlugAndPlayDon;
        _currentPlugAndPlayCandidate = candidates[_random.Next(candidates.Count)];
        PopupQuestion = string.Format(Resources.Get("PlugAndPlayDonPromptFormat"), _currentPlugAndPlayCandidate.Name);
        PopupAnswers = [Resources.Get("PlugAndPlayDonConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void OnPlugAndPlayDonConfirm()
    {
        if (_currentPlugAndPlayCandidate is null)
        {
            return;
        }

        var config = _saveRepo.GetPlugAndPlayItems()
            .FirstOrDefault(c => c.ClothingItemId == _currentPlugAndPlayCandidate.Id);
        var durationMinutes = config is null
            ? 0
            : _random.Next(config.MinDurationMinutes, config.MaxDurationMinutes + 1);

        _saveRepo.SaveActivePlugAndPlayAccessory(new ActivePlugAndPlayAccessory
        {
            ClothingItemId = _currentPlugAndPlayCandidate.Id,
            DonnedAt = DateTime.UtcNow,
            RemoveAfterUtc = DateTime.UtcNow.AddMinutes(durationMinutes)
        });
        _currentPlugAndPlayCandidate = null;
        RefreshOutfitDisplay();

        PopupOutcomeMessage = Resources.Get("PlugAndPlayDonCompletedMessage");
        ShowPopupOutcome(true);
    }

    private void ShowPlugAndPlayRemovePopup()
    {
        var active = _saveRepo.GetActivePlugAndPlayAccessory();
        var item = active is null ? null : _saveRepo.GetWardrobe().FirstOrDefault(i => i.Id == active.ClothingItemId);
        if (item is null)
        {
            return;
        }

        _currentPopupKind = IdlePopupKind.PlugAndPlayRemove;
        PopupQuestion = string.Format(Resources.Get("PlugAndPlayRemovePromptFormat"), item.Name);
        PopupAnswers = [Resources.Get("PlugAndPlayRemoveConfirm")];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        SoundEffectService.PlayDeskBell();
    }

    private void OnPlugAndPlayRemoveConfirm()
    {
        _saveRepo.ClearActivePlugAndPlayAccessory();
        RefreshOutfitDisplay();

        PopupOutcomeMessage = Resources.Get("PlugAndPlayRemoveCompletedMessage");
        ShowPopupOutcome(true);
    }

    private void OnAnswer(string answer)
    {
        if (!IsPopupVisible)
        {
            return;
        }

        _popupSecondsLate = Math.Max(0, (int)(DateTime.UtcNow - _popupShownAt).TotalSeconds - _settings.PopupTimeoutSeconds);

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
            case IdlePopupKind.SelfCheck:
                OnSelfCheckAnswer(answer);
                break;
            case IdlePopupKind.Curtsy:
                OnCurtsyConfirm();
                break;
            case IdlePopupKind.MotivationalQuote:
                OnMotivationalQuoteConfirm();
                break;
            case IdlePopupKind.Kneel:
                OnKneelConfirm();
                break;
            case IdlePopupKind.PlugAndPlayDon:
                OnPlugAndPlayDonConfirm();
                break;
            case IdlePopupKind.PlugAndPlayRemove:
                OnPlugAndPlayRemoveConfirm();
                break;
        }
    }

    private void OnPoseForCameraConfirm()
    {
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
        var dir = Path.Combine(AppPaths.Root, "data", "saves", slotName);
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
            var outfitItemIds = (_saveRepo.GetGameState()?.CurrentOutfitItemIds ?? []).ToList();
            var activePlugAndPlay = _saveRepo.GetActivePlugAndPlayAccessory();
            if (activePlugAndPlay is not null)
            {
                outfitItemIds.Add(activePlugAndPlay.ClothingItemId);
            }

            _saveRepo.SavePoseCheckRecord(new PoseCheckRecord
            {
                ItemId = item.Id,
                ItemName = item.Name,
                PhotoPath = path,
                CapturedAt = DateTime.UtcNow,
                OutfitItemIdsAtCapture = outfitItemIds,
                HasOutfitSnapshot = true,
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

    /// <summary>
    ///     Judges the answer against the two facts already known about the queried item (was it worn that
    ///     session, was it the one actually requested at capture time) — no image analysis of the photo
    ///     itself is involved. See docs/specs/selfcheck.md for the case table this implements.
    /// </summary>
    private void OnSelfCheckAnswer(string answer)
    {
        var record = _currentSelfCheckRecord;
        var item = _currentSelfCheckQueriedItem;
        _currentSelfCheckRecord = null;
        _currentSelfCheckQueriedItem = null;

        if (record is null || item is null)
        {
            ShowPopupOutcome(true);
            return;
        }

        var userSaysVisible = PopupAnswers.Count > 0 && answer == PopupAnswers[0];
        var wasWornThatSession = record.OutfitItemIdsAtCapture.Contains(item.Id);
        var wasTheRequestedItem = item.Id == record.ItemId;

        string? reason = null;
        if (wasTheRequestedItem && !userSaysVisible)
        {
            reason = "PunishmentReasonSelfCheckWronglyHidden";
        }
        else if (!wasWornThatSession && userSaysVisible)
        {
            reason = "PunishmentReasonSelfCheckWronglyVisible";
        }

        var positive = reason is null;
        if (reason is not null)
        {
            // Combined with lateness, same as Impossible Question — not punished separately via the
            // generic per-second path in ClosePopup, hence the _popupSecondsLate reset below.
            var baseSeverity = reason == "PunishmentReasonSelfCheckWronglyHidden"
                ? InfractionSeverity.SelfCheckWronglyHidden
                : InfractionSeverity.SelfCheckWronglyVisible;
            var severity = baseSeverity + _popupSecondsLate;
            _saveRepo.AssignPunishment((severity, severity), reason);
            RefreshPunishments();
            _popupSecondsLate = 0;
        }

        record.Reviewed = true;
        record.IsCompliant = positive;
        _saveRepo.SavePoseCheckRecord(record);

        PopupOutcomeMessage = Resources.Get(positive ? "SelfCheckPassedMessage" : "SelfCheckFailedMessage");
        ShowPopupOutcome(positive);
    }

    private void OnCurtsyConfirm()
    {
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
        var dir = Path.Combine(AppPaths.Root, "data", "saves", slotName);
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
        IsQuotePopupActive = true;

        if (_mainVm.Stt.DetectMicrophone())
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
        IsQuoteProcessing = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        QuoteAttemptsRemaining = QuoteMaxAttempts;
        var attemptSeconds = (int)_mainVm.Stt.AttemptRecordDuration.TotalSeconds;
        QuoteListenCountdown = attemptSeconds;

        // The countdown only tracks the recording window of the current attempt — Whisper's transcription
        // step afterward has no predictable duration, so it gets its own "processing" indicator (see the
        // phase callback below) instead of pretending a number is still counting down.
        _quoteListenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _quoteListenTimer.Tick += (_, _) =>
        {
            if (QuoteListenCountdown > 0)
            {
                QuoteListenCountdown--;
            }
        };
        _quoteListenTimer.Start();

        var phaseProgress = new Progress<SpeechListenPhase>(p =>
        {
            IsQuoteProcessing = p == SpeechListenPhase.Processing;
            if (p == SpeechListenPhase.Listening)
            {
                QuoteListenCountdown = attemptSeconds;
            }
        });

        _quoteListenCts = new CancellationTokenSource();
        var success = false;
        try
        {
            success = await _mainVm.Stt.ListenForPhraseAsync(
                _currentQuoteText,
                QuoteMaxAttempts,
                TimeSpan.FromSeconds(_settings.QuoteListenTimeoutSeconds),
                _settings.QuoteMatchMinSimilarityPercent / 100.0,
                new Progress<int>(remaining => QuoteAttemptsRemaining = remaining),
                phaseProgress,
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
        IsQuoteProcessing = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        FinishMotivationalQuote(success);
    }

    private void StartQuoteTyping()
    {
        IsQuoteTypingActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        QuoteTypedAnswer = "";
        QuoteAttemptsRemaining = QuoteMaxAttempts;
    }

    private void OnSubmitQuoteTypedAnswer()
    {
        if (!IsQuoteTypingActive)
        {
            return;
        }

        var match = string.Equals(QuoteTypedAnswer.Trim(), _currentQuoteText.Trim(), StringComparison.OrdinalIgnoreCase);
        if (match)
        {
            FinishQuoteTyping(true);
            return;
        }

        QuoteAttemptsRemaining--;
        if (QuoteAttemptsRemaining <= 0)
        {
            FinishQuoteTyping(false);
            return;
        }

        QuoteTypedAnswer = "";
    }

    private void FinishQuoteTyping(bool success)
    {
        IsQuoteTypingActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        FinishMotivationalQuote(success);
    }

    private void FinishMotivationalQuote(bool success)
    {
        if (success)
        {
            PopupOutcomeMessage = Resources.Get(_popupSecondsLate > 0
                ? "MotivationalQuoteSuccessLateMessage"
                : "MotivationalQuoteSuccessMessage");
        }
        else
        {
            var severity = _popupSecondsLate + InfractionSeverity.MotivationalQuoteFailedBonus;
            _saveRepo.AssignPunishment((severity, severity), "PunishmentReasonMotivationalQuoteFailed");
            RefreshPunishments();
            _popupSecondsLate = 0;
            PopupOutcomeMessage = Resources.Get("MotivationalQuoteFailedMessage");
        }

        ShowPopupOutcome(success);
    }

    private void OnImpossibleQuestionAnswer(string answer)
    {
        if (_currentQuestion is null)
        {
            return;
        }

        var isYes = PopupAnswers.Count > 0 && answer == PopupAnswers[0];
        var positive = ImpossibleQuestionBank.RollOutcome();
        var outcomeKey = isYes
            ? (positive ? _currentQuestion.YesPositiveKey : _currentQuestion.YesNegativeKey)
            : (positive ? _currentQuestion.NoPositiveKey : _currentQuestion.NoNegativeKey);

        if (!positive)
        {
            var severity = _popupSecondsLate +
                _random.Next(InfractionSeverity.ImpossibleQuestionFailed.Min, InfractionSeverity.ImpossibleQuestionFailed.Max + 1);
            _saveRepo.AssignPunishment((severity, severity), "PunishmentReasonImpossibleQuestionFailed");
            RefreshPunishments();
            _popupSecondsLate = 0;
        }

        PopupOutcomeMessage = Resources.Get(outcomeKey);
        ShowPopupOutcome(positive);
    }

    private void OnHydrationConfirm()
    {
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
        _kneelCountdownTimer?.Stop();
        _kneelCountdownTimer = null;
        _kneelCts?.Cancel();
        _kneelCts?.Dispose();
        _kneelCts = null;
        if (IsPoseForCameraPopupActive || IsCurtsyPopupActive || IsKneelFramingActive)
        {
            _mainVm.Webcam.Stop();
        }

        IsPopupVisible = false;
        IsShowingPopupOutcome = false;
        IsPoseCountdownActive = false;
        IsPoseForCameraPopupActive = false;
        IsSelfCheckPopupActive = false;
        IsCurtsyCountdownActive = false;
        IsCurtsyRecordingActive = false;
        IsCurtsyPopupActive = false;
        IsQuotePopupActive = false;
        IsQuoteListeningActive = false;
        IsQuoteTypingActive = false;
        IsKneelPopupActive = false;
        IsKneelFramingActive = false;
        IsKneelActive = false;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        _currentQuestion = null;
        _currentPoseItem = null;
        _currentSelfCheckRecord = null;
        _currentSelfCheckQueriedItem = null;
        CurrentQuoteText = "";
        QuoteTypedAnswer = "";
        CapturedPosePhoto?.Dispose();
        CapturedPosePhoto = null;
        SelfCheckPhoto?.Dispose();
        SelfCheckPhoto = null;
        _sessionLoop.ReportPopupResult(positive);

        if (_popupSecondsLate > 0)
        {
            var severity = (int)Math.Round(_popupSecondsLate * InfractionSeverity.PopupLateSeverityPerSecond);
            _saveRepo.AssignPunishment((severity, severity), "PunishmentReasonPopupLate");
            RefreshPunishments();
        }

        _popupSecondsLate = 0;
    }
}
