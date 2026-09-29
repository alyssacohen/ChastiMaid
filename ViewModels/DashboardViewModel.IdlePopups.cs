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
    [Reactive] private bool _isPoseCapturing;
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

    // --- Idle popup: Curtsy (curtsy video) ---
    [Reactive] private bool _isCurtsyCountdownActive;
    [Reactive] private bool _isCurtsyRecordingActive;
    [Reactive] private bool _isCurtsyPopupActive;
    [Reactive] private int _curtsyCountdown;
    [Reactive] private int _curtsyRecordingSecondsLeft;
    private DispatcherTimer? _curtsyCountdownTimer;
    private CancellationTokenSource? _curtsyRecordingCts;

    // --- Idle popup: Motivational Quote ---
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
    [Reactive] private bool _isKneelFramingActive;
    [Reactive] private bool _isKneelActive;
    [Reactive] private int _kneelCountdown;
    private DispatcherTimer? _kneelCountdownTimer;
    private CancellationTokenSource? _kneelCts;

    // --- Idle popup: Plug and Play ---
    private ClothingItem? _currentPlugAndPlayCandidate;

    public WebcamPreviewService Webcam => _mainVm.Webcam;

    public bool IsShowingQuestionPanel =>
        !IsShowingPopupOutcome && !IsPoseCountdownActive && !IsPoseCapturing && !IsCurtsyCountdownActive &&
        !IsCurtsyRecordingActive && !IsQuoteListeningActive && !IsQuoteTypingActive && !IsKneelFramingActive &&
        !IsKneelActive;

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

    // Popups allowed to interrupt a task in progress (IsBusy) — everything else requires full attention.
    private static readonly IdlePopupKind[] InterruptibleDuringTaskKinds =
    [
        IdlePopupKind.Hydration,
        IdlePopupKind.PlugAndPlayDon,
        IdlePopupKind.PlugAndPlayRemove
    ];

    /// <summary>
    ///     Rings the desk bell and, in the same beat, has the employer speak a random summon phrase
    ///     (TTS only — no on-screen text) naming the player by PlayerName or one of the configured
    ///     Appellatives, picked at random to multiply the perceived variety.
    /// </summary>
    private void PlaySummonBell()
    {
        SoundEffectService.PlayDeskBell();

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.PlayerName))
        {
            candidates.Add(_settings.PlayerName.Trim());
        }

        candidates.AddRange(_settings.ParseAppellatives());
        if (candidates.Count == 0)
        {
            return;
        }

        var appellative = candidates[_random.Next(candidates.Count)];
        var phrase = string.Format(Resources.Get(SummonPhraseBank.GetRandom(_random)), appellative);
        _ = _mainVm.Tts.SpeakAsync(phrase);
    }

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

    private record PopupHandler(Func<bool> IsAvailable, Action Show, Action<string> OnAnswer);

    /// <summary>Availability check, opener and answer handler of each popup kind.</summary>
    private PopupHandler Handler(IdlePopupKind kind) => kind switch
    {
        IdlePopupKind.ImpossibleQuestion => new(() => true, ShowImpossibleQuestionPopup, OnImpossibleQuestionAnswer),
        IdlePopupKind.Hydration => new(IsHydrationAvailable, ShowHydrationPopup, _ => OnHydrationConfirm()),
        IdlePopupKind.PoseForCamera => new(IsPoseForCameraAvailable, ShowPoseForCameraPopup,
            _ => OnPoseForCameraConfirm()),
        IdlePopupKind.SelfCheck => new(IsSelfCheckAvailable, ShowSelfCheckPopup, OnSelfCheckAnswer),
        IdlePopupKind.Curtsy => new(() => _settings.CurtsyEnabled && IsWebcamAvailable(), ShowCurtsyPopup,
            _ => OnCurtsyConfirm()),
        IdlePopupKind.MotivationalQuote => new(() => _settings.QuoteReadingEnabled, ShowMotivationalQuotePopup,
            _ => OnMotivationalQuoteConfirm()),
        IdlePopupKind.Kneel => new(() => _settings.KneelEnabled && IsWebcamAvailable(), ShowKneelPopup,
            _ => OnKneelConfirm()),
        IdlePopupKind.PlugAndPlayDon => new(IsPlugAndPlayDonAvailable, ShowPlugAndPlayDonPopup,
            _ => OnPlugAndPlayDonConfirm()),
        IdlePopupKind.PlugAndPlayRemove => new(IsPlugAndPlayRemoveAvailable, ShowPlugAndPlayRemovePopup,
            _ => OnPlugAndPlayRemoveConfirm()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private bool IsWebcamAvailable() => WebcamPreviewService.DetectWebcam(_settings.SelectedCameraIndex);

    private void ShowIdlePopup()
    {
        if (IsPopupVisible)
        {
            return;
        }

        var kinds = IsBusy ? InterruptibleDuringTaskKinds : Enum.GetValues<IdlePopupKind>();
        var candidates = kinds.Where(k => Handler(k).IsAvailable()).ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        Handler(candidates[_random.Next(candidates.Length)]).Show();
    }

    /// <summary>Shows the popup's question and answers, then rings the summon bell.</summary>
    private void OpenPopup(IdlePopupKind kind, string question, params string[] answers)
    {
        _currentPopupKind = kind;
        PopupQuestion = question;
        PopupAnswers = [.. answers];
        _popupShownAt = DateTime.UtcNow;
        IsShowingPopupOutcome = false;
        IsPopupVisible = true;

        PlaySummonBell();
    }

    /// <summary>Assigns a punishment that already includes lateness, so ClosePopup doesn't add it again.</summary>
    private void Punish(int severity, string reason)
    {
        _saveRepo.AssignPunishment((severity, severity), reason);
        RefreshPunishments();
        RefreshDebt();
        _popupSecondsLate = 0;
    }

    private string NewMediaPath(string prefix, string extension) =>
        Path.Combine(AppPaths.SaveDir(_saveRepo.GetGameState()?.SlotName ?? "unknown"),
            $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{extension}");

    /// <summary>Counts <paramref name="setCount" /> down from 3 once per second, then runs <paramref name="onDone" />.</summary>
    private static DispatcherTimer StartCountdown(Action<int> setCount, Func<Task> onDone)
    {
        var remaining = 3;
        setCount(remaining);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += async (_, _) =>
        {
            setCount(--remaining);
            if (remaining <= 0)
            {
                timer.Stop();
                await onDone();
            }
        };
        timer.Start();
        return timer;
    }

    private static void StopTimer(ref DispatcherTimer? timer)
    {
        timer?.Stop();
        timer = null;
    }

    private static void CancelCts(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void ShowImpossibleQuestionPopup()
    {
        _currentQuestion = ImpossibleQuestionBank.GetRandom();
        OpenPopup(IdlePopupKind.ImpossibleQuestion, Resources.Get(_currentQuestion.QuestionKey),
            Resources.Get("ImpossibleQuestionYes"), Resources.Get("ImpossibleQuestionNo"));
    }

    private void ShowHydrationPopup()
    {
        _lastHydrationPopupAt = DateTime.UtcNow;
        OpenPopup(IdlePopupKind.Hydration, Resources.Get(HydrationReminderBank.GetRandom()),
            Resources.Get("HydrationConfirm"));
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

        IsPoseForCameraPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        _currentPoseItem = candidates[_random.Next(candidates.Count)];
        OpenPopup(IdlePopupKind.PoseForCamera,
            string.Format(Resources.Get(PoseForCameraInstructionBank.GetRandom()), _currentPoseItem.Name),
            Resources.Get("PoseForCameraConfirm"));
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

        _currentSelfCheckRecord = record;
        _currentSelfCheckQueriedItem = queriedItem;
        IsSelfCheckPopupActive = true;
        OpenPopup(IdlePopupKind.SelfCheck,
            string.Format(Resources.Get(SelfCheckInstructionBank.GetRandom()), queriedItem.Name),
            Resources.Get("SelfCheckYes"), Resources.Get("SelfCheckNo"));
    }

    private void ShowCurtsyPopup()
    {
        if (!_mainVm.Webcam.Start(_settings.SelectedCameraIndex))
        {
            return;
        }

        _ = _mainVm.Webcam.RefreshAvailableCamerasAsync(_settings.SelectedCameraIndex);

        IsCurtsyPopupActive = true;
        this.RaisePropertyChanged(nameof(IsWebcamPreviewNeededInQuestionPanel));
        OpenPopup(IdlePopupKind.Curtsy, Resources.Get(CurtsyInstructionBank.GetRandom()),
            Resources.Get("CurtsyConfirm"));
    }

    private void ShowMotivationalQuotePopup()
    {
        CurrentQuoteText = Resources.Get(EmployerPhraseBank.GetRandom());
        OpenPopup(IdlePopupKind.MotivationalQuote, Resources.Get("MotivationalQuoteInstruction"),
            Resources.Get("MotivationalQuoteConfirm"));
    }

    private void ShowKneelPopup()
    {
        OpenPopup(IdlePopupKind.Kneel, Resources.Get("KneelSummon"), Resources.Get("KneelSummonConfirm"));

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
                    var path = NewMediaPath("kneel", "png");
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

        StopTimer(ref _kneelCountdownTimer);
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

        _currentPlugAndPlayCandidate = candidates[_random.Next(candidates.Count)];
        OpenPopup(IdlePopupKind.PlugAndPlayDon,
            string.Format(Resources.Get("PlugAndPlayDonPromptFormat"), _currentPlugAndPlayCandidate.Name),
            Resources.Get("PlugAndPlayDonConfirm"));
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

        OpenPopup(IdlePopupKind.PlugAndPlayRemove,
            string.Format(Resources.Get("PlugAndPlayRemovePromptFormat"), item.Name),
            Resources.Get("PlugAndPlayRemoveConfirm"));
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

        Handler(_currentPopupKind).OnAnswer(answer);
    }

    private void OnPoseForCameraConfirm()
    {
        IsPoseCountdownActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        _poseCountdownTimer = StartCountdown(n => PoseCountdown = n, CapturePoseAsync);
    }

    private async Task CapturePoseAsync()
    {
        var item = _currentPoseItem;
        _currentPoseItem = null;
        IsPoseCountdownActive = false;
        IsPoseCapturing = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));

        if (item is null)
        {
            _mainVm.Webcam.Stop();
            IsPoseCapturing = false;
            PopupOutcomeMessage = Resources.Get("PoseForCameraFailedMessage");
            ShowPopupOutcome(true);
            return;
        }

        var path = NewMediaPath("pose", "png");

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

        IsPoseCapturing = false;
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
            Punish(baseSeverity + _popupSecondsLate, reason);
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
        _curtsyCountdownTimer = StartCountdown(n => CurtsyCountdown = n, StartCurtsyRecordingAsync);
    }

    private async Task StartCurtsyRecordingAsync()
    {
        IsCurtsyCountdownActive = false;
        IsCurtsyRecordingActive = true;
        this.RaisePropertyChanged(nameof(IsShowingQuestionPanel));
        CurtsyRecordingSecondsLeft = _settings.CurtsyVideoDurationSeconds;

        var path = NewMediaPath("curtsy", "avi");

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

        StopTimer(ref _quoteListenTimer);
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
            Punish(_popupSecondsLate + InfractionSeverity.MotivationalQuoteFailedBonus,
                "PunishmentReasonMotivationalQuoteFailed");
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
            Punish(_popupSecondsLate + _random.Next(InfractionSeverity.ImpossibleQuestionFailed.Min,
                InfractionSeverity.ImpossibleQuestionFailed.Max + 1), "PunishmentReasonImpossibleQuestionFailed");
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
        _popupOutcomeTimer.Tick += (_, _) => ClosePopup(positive);
        _popupOutcomeTimer.Start();
    }

    private void DismissPopupOutcome()
    {
        if (IsShowingPopupOutcome)
        {
            ClosePopup(_pendingOutcomePositive);
        }
    }

    private void ClosePopup(bool positive)
    {
        StopTimer(ref _popupOutcomeTimer);
        StopTimer(ref _poseCountdownTimer);
        StopTimer(ref _curtsyCountdownTimer);
        StopTimer(ref _quoteListenTimer);
        StopTimer(ref _kneelCountdownTimer);
        CancelCts(ref _curtsyRecordingCts);
        CancelCts(ref _quoteListenCts);
        CancelCts(ref _kneelCts);
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
        IsQuoteListeningActive = false;
        IsQuoteTypingActive = false;
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
            Punish((int)Math.Round(_popupSecondsLate * InfractionSeverity.PopupLateSeverityPerSecond),
                "PunishmentReasonPopupLate");
        }
    }
}
