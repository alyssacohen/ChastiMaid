using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Services.Content;
using ChastiMaid.Services.Contracts;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private ILocalizer L => _mainVm.Localizer;
    private readonly ProfileRepository _repo;
    private readonly LinkedRanges _ranges;
    private Func<ViewModelBase> _returnTo;

    // --- Dialog ---
    [Reactive] private ViewModelBase? _activeDialog;

    // Replace-confirmation for importing a contract; separate from ActiveDialog, whose overlay is the
    // Save/Discard/Cancel dialog.
    [Reactive] private ConfirmDialogViewModel? _contractDialog;
    [Reactive] private TestOutfitDialogViewModel? _testOutfitDialog;
    [Reactive] private IReadOnlyList<CameraOption> _availableCameras = [];
    [Reactive] private int _autoSaveInterval;
    [Reactive] private int _captureMaxInterval;
    [Reactive] private int _captureMinInterval;
    [Reactive] private int _captureVideoMax;
    [Reactive] private int _captureVideoMin;
    [Reactive] private double _dailyInterestRate;
    [Reactive] private decimal _idleWageAmount;
    [Reactive] private int _idleWageInterval;
    [Reactive] private bool _isChoresDirty;
    [Reactive] private string? _contentActionStatusMessage;
    [Reactive] private bool _plugAndPlayEnabled;
    [Reactive] private bool _diaperPlayEnabled;
    [Reactive] private bool _hydrationEnabled;
    [Reactive] private bool _curtsyEnabled;
    [Reactive] private bool _quoteReadingEnabled;
    [Reactive] private bool _kneelEnabled;
    [Reactive] private int _kneelMinDuration;
    [Reactive] private int _kneelMaxDuration;
    [Reactive] private int _kneelPhotoMinInterval;
    [Reactive] private int _kneelPhotoMaxInterval;

    // --- Punishments ---
    [Reactive] private decimal _fineSeverityValue;
    [Reactive] private bool _cornertimeEnabled;
    [Reactive] private double _cornertimeSeverityValue;
    [Reactive] private int _cornertimeMinRequiredSeconds;
    [Reactive] private int _cornertimeMaxOverrunSeconds;
    [Reactive] private int _cornertimeMaxRequiredSeconds;
    [Reactive] private int _cornertimeMovementCooldownMinSeconds;
    [Reactive] private int _cornertimeMovementCooldownMaxSeconds;
    [Reactive] private int _cornertimeMovementSensitivityPercent;
    [Reactive] private int _cornertimeMovementPenaltyMinSeconds;
    [Reactive] private int _cornertimeMovementPenaltyMaxSeconds;
    [Reactive] private bool _cornertimeVoiceEnabled;
    [Reactive] private bool _writingLinesEnabled;
    [Reactive] private double _writingLinesSeverityValue;
    [Reactive] private int _writingLinesPenaltyLinesPerMistake;
    [Reactive] private int _writingLinesMinLines;
    [Reactive] private int _writingLinesMaxLines;
    [Reactive] private bool _writingLinesNoIdleEnabled;
    [Reactive] private int _writingLinesNoIdleTimeoutSeconds;
    [Reactive] private int _writingLinesNoIdleLinesToAdd;
    [Reactive] private bool _writingLinesInterruptionsEnabled;
    [Reactive] private int _writingLinesInterruptionMinIntervalSeconds;
    [Reactive] private int _writingLinesInterruptionMaxIntervalSeconds;
    [Reactive] private bool _forcedOutfitEnabled;
    [Reactive] private double _forcedOutfitMaxHours;
    [Reactive] private string _speechRecognitionStatusDisplay = "";
    [Reactive] private string _webcamStatusDisplay = "";

    // --- Per-tab dirty flags ---
    [Reactive] private bool _isGeneralDirty;
    [Reactive] private bool _isPopupsDirty;
    [Reactive] private bool _isPunishmentsDirty;
    [Reactive] private bool _isRulesDirty;
    [Reactive] private bool _isWardrobeDirty;
    [Reactive] private bool _isPermissionsDirty;
    [Reactive] private decimal _maxDebt;
    [Reactive] private decimal _minDebt;

    // --- General tab fields ---
    [Reactive] private int _curtsyVideoDuration;
    [Reactive] private int _hydrationMinInterval;
    [Reactive] private string _playerName = "";
    [Reactive] private string _appellatives = "";
    [Reactive] private int _popupMaxInterval;
    [Reactive] private int _popupMinInterval;
    [Reactive] private int _popupTimeout;
    [Reactive] private int _quoteListenTimeout;
    [Reactive] private int _quoteMatchMinSimilarityPercent;
    [Reactive] private bool _isTestQuoteListening;
    [Reactive] private double? _testQuoteScore;

    /// <summary>
    ///     Optimistically true until <see cref="LoadHardwareAvailability" /> reports otherwise — avoids a
    ///     false "unavailable" flash while the background hardware scan is still running.
    /// </summary>
    [Reactive] private bool _isMicrophoneAvailableForTest = true;

    [Reactive] private string? _testQuoteUnavailableTooltip;
    [Reactive] private CameraOption _selectedCamera = null!;
    [Reactive] private Option<string> _selectedLanguage = null!;

    // --- Tab selection with interception ---
    private int _selectedTabIndex;

    // Snapshot for General tab dirty detection
    private (string, string, int, decimal, decimal, double, decimal, int, CameraOption, Option<string>) _snapshot;

    public SettingsViewModel(MainWindowViewModel mainVm, Func<ViewModelBase> returnTo,
        string? initialContentActionStatusMessage = null)
    {
        _mainVm = mainVm;
        _repo = mainVm.ProfileRepository;
        _returnTo = returnTo;
        _contentActionStatusMessage = initialContentActionStatusMessage;

        var s = _repo.GetGameSettings();
        _playerName = s.PlayerName;
        _appellatives = s.Appellatives;
        _autoSaveInterval = s.AutoSaveIntervalMinutes;
        _minDebt = s.MinDebt;
        _maxDebt = s.MaxDebt;
        _dailyInterestRate = s.DailyInterestRate;
        _idleWageAmount = s.IdleWageAmount;
        _idleWageInterval = s.IdleWageIntervalMinutes;
        _popupMinInterval = s.PopupMinIntervalSeconds;
        _popupMaxInterval = s.PopupMaxIntervalSeconds;
        _popupTimeout = s.PopupTimeoutSeconds;
        _hydrationEnabled = s.HydrationEnabled;
        _hydrationMinInterval = s.HydrationMinIntervalMinutes;
        _curtsyEnabled = s.CurtsyEnabled;
        _curtsyVideoDuration = s.CurtsyVideoDurationSeconds;
        _quoteReadingEnabled = s.QuoteReadingEnabled;
        _quoteListenTimeout = s.QuoteListenTimeoutSeconds;
        _quoteMatchMinSimilarityPercent = s.QuoteMatchMinSimilarityPercent;
        _captureMinInterval = s.BusyCaptureMinIntervalSeconds;
        _captureMaxInterval = s.BusyCaptureMaxIntervalSeconds;
        _captureVideoMin = s.BusyCaptureVideoMinSeconds;
        _captureVideoMax = s.BusyCaptureVideoMaxSeconds;
        _kneelEnabled = s.KneelEnabled;
        _kneelMinDuration = s.KneelMinDurationSeconds;
        _kneelMaxDuration = s.KneelMaxDurationSeconds;
        _kneelPhotoMinInterval = s.KneelPhotoMinIntervalSeconds;
        _kneelPhotoMaxInterval = s.KneelPhotoMaxIntervalSeconds;
        _plugAndPlayEnabled = s.PlugAndPlayEnabled;
        _diaperPlayEnabled = s.DiaperPlayEnabled;
        _fineSeverityValue = s.FineSeverityValue;
        _cornertimeEnabled = s.CornertimeEnabled;
        _cornertimeSeverityValue = s.CornertimeSeverityValue;
        _cornertimeMinRequiredSeconds = s.CornertimeMinRequiredSeconds;
        _cornertimeMaxOverrunSeconds = s.CornertimeMaxOverrunSeconds;
        _cornertimeMaxRequiredSeconds = s.CornertimeMaxRequiredSeconds;
        _cornertimeMovementCooldownMinSeconds = s.CornertimeMovementCooldownMinSeconds;
        _cornertimeMovementCooldownMaxSeconds = s.CornertimeMovementCooldownMaxSeconds;
        _cornertimeMovementSensitivityPercent = s.CornertimeMovementSensitivityPercent;
        _cornertimeMovementPenaltyMinSeconds = s.CornertimeMovementPenaltyMinSeconds;
        _cornertimeMovementPenaltyMaxSeconds = s.CornertimeMovementPenaltyMaxSeconds;
        _cornertimeVoiceEnabled = s.CornertimeVoiceEnabled;
        _writingLinesEnabled = s.WritingLinesEnabled;
        _writingLinesSeverityValue = s.WritingLinesSeverityValue;
        _writingLinesPenaltyLinesPerMistake = s.WritingLinesPenaltyLinesPerMistake;
        _writingLinesMinLines = s.WritingLinesMinLines;
        _writingLinesMaxLines = s.WritingLinesMaxLines;
        _writingLinesNoIdleEnabled = s.WritingLinesNoIdleEnabled;
        _writingLinesNoIdleTimeoutSeconds = s.WritingLinesNoIdleTimeoutSeconds;
        _writingLinesNoIdleLinesToAdd = s.WritingLinesNoIdleLinesToAdd;
        _writingLinesInterruptionsEnabled = s.WritingLinesInterruptionsEnabled;
        _writingLinesInterruptionMinIntervalSeconds = s.WritingLinesInterruptionMinIntervalSeconds;
        _writingLinesInterruptionMaxIntervalSeconds = s.WritingLinesInterruptionMaxIntervalSeconds;
        _forcedOutfitEnabled = s.ForcedOutfitEnabled;
        _forcedOutfitMaxHours = s.ForcedOutfitMaxHours;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == s.Language) ?? AvailableLanguages[0];
        _confirmedLanguage = _selectedLanguage.Code;

        // Show a default immediately so Settings opens instantly; the real camera list and hardware
        // status are probed off the UI thread and applied once ready (see LoadHardwareAvailability).
        _selectedCamera = new CameraOption(s.SelectedCameraIndex, $"Camera {s.SelectedCameraIndex}");
        AvailableCameras = [_selectedCamera];
        _ = LoadHardwareAvailability(s.SelectedCameraIndex);

        foreach (var c in _repo.GetChores())
        {
            Chores.Add(NewChoreVm(c));
        }

        RefreshForcedOutfitPresets();

        foreach (var p in _repo.GetPermissionTypes())
        {
            PermissionTypes.Add(WireChild(new PermissionTypeEditViewModel(p, _mainVm.Localizer), () => IsPermissionsDirty = true));
        }

        var allWardrobe = _repo.GetWardrobe().ToList();
        foreach (var w in allWardrobe)
        {
            WardrobeItems.Add(NewWardrobeVm(w));
        }

        // Eligibility chip lists are derived from WardrobeItems (not the raw ClothingItem snapshot)
        // so each chip holds a live reference to its WardrobeEditViewModel — see WireWardrobeItem /
        // SyncEligibilityChips for how they stay in sync as items are added/edited/removed later.
        var plugAndPlayConfigs = _repo.GetPlugAndPlayItems();
        var diaperPlayConfigs = _repo.GetDiaperPlayItems();
        var chastityDeviceConfigs = _repo.GetChastityDeviceItems();
        foreach (var w in WardrobeItems)
        {
            if (w.Layers.Contains(4)) // 4 = Accessories layer
            {
                var existing = plugAndPlayConfigs.FirstOrDefault(c => c.ClothingItemId == w.Id);
                PlugAndPlayItems.Add(WireChild(
                    new PlugAndPlayItemEditViewModel(w, existing),
                    () => IsGeneralDirty = true));
            }

            if (w.Layers.Contains(0)) // 0 = Underwear layer
            {
                var existingDiaper = diaperPlayConfigs.FirstOrDefault(c => c.ClothingItemId == w.Id);
                DiaperPlayItems.Add(WireChild(
                    new DiaperPlayItemEditViewModel(w, existingDiaper),
                    () => IsGeneralDirty = true));

                var existingDevice = chastityDeviceConfigs.FirstOrDefault(c => c.ClothingItemId == w.Id);
                ChastityDeviceItems.Add(WireChild(
                    new ChastityDeviceItemEditViewModel(w, existingDevice),
                    () => IsGeneralDirty = true));
            }
        }

        // The Wardrobe first: its tag list feeds the rules' suggestions.
        var allRules = _repo.GetOutfitRules();
        Wardrobe = new WardrobeViewModel(WardrobeItems, OutfitRules,
            WardrobeCatalog.Merge(_repo.GetWardrobeCatalog(), allWardrobe, allRules), NewWardrobeVm, L);
        foreach (var r in allRules)
        {
            OutfitRules.Add(NewRuleVm(r));
        }

        TakeSnapshot();

        // The same pairs as GameContractService.Pairs, by ViewModel name.
        _ranges = new LinkedRanges(this,
            (nameof(MinDebt), nameof(MaxDebt)),
            (nameof(PopupMinInterval), nameof(PopupMaxInterval)),
            (nameof(CaptureMinInterval), nameof(CaptureMaxInterval)),
            (nameof(CaptureVideoMin), nameof(CaptureVideoMax)),
            (nameof(KneelMinDuration), nameof(KneelMaxDuration)),
            (nameof(KneelPhotoMinInterval), nameof(KneelPhotoMaxInterval)),
            (nameof(CornertimeMinRequiredSeconds), nameof(CornertimeMaxRequiredSeconds)),
            (nameof(CornertimeMovementCooldownMinSeconds), nameof(CornertimeMovementCooldownMaxSeconds)),
            (nameof(CornertimeMovementPenaltyMinSeconds), nameof(CornertimeMovementPenaltyMaxSeconds)),
            (nameof(WritingLinesMinLines), nameof(WritingLinesMaxLines)),
            (nameof(WritingLinesInterruptionMinIntervalSeconds), nameof(WritingLinesInterruptionMaxIntervalSeconds)));

        Changed.Subscribe(e => OnSettingChanged(e.PropertyName));

        Chores.CollectionChanged += (_, _) => IsChoresDirty = true;
        WardrobeItems.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<WardrobeEditViewModel>() ?? [])
            {
                SyncEligibilityChips(item, removed: true);
            }

            foreach (var item in e.NewItems?.OfType<WardrobeEditViewModel>() ?? [])
            {
                SyncEligibilityChips(item);
            }

            IsWardrobeDirty = true;
        };
        OutfitRules.CollectionChanged += (_, _) => IsRulesDirty = true;

        SaveCommand = ReactiveCommand.Create(SaveAndBack);
        QuickSaveCommand = ReactiveCommand.Create(SaveInPlace);
        StartTestQuoteListeningCommand = ReactiveCommand.Create(StartTestQuoteListening,
            this.WhenAnyValue(x => x.IsTestQuoteListening, x => x.IsMicrophoneAvailableForTest,
                (listening, micAvailable) => !listening && micAvailable));
        StopTestQuoteListeningCommand = ReactiveCommand.CreateFromTask(StopTestQuoteListeningAsync,
            this.WhenAnyValue(x => x.IsTestQuoteListening));
        this.WhenAnyValue(x => x.TestQuoteScore, x => x.QuoteMatchMinSimilarityPercent, x => x.IsTestQuoteListening,
                (_, _, _) => Unit.Default)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ShowTestQuotePassResult));
                this.RaisePropertyChanged(nameof(ShowTestQuoteFailResult));
                this.RaisePropertyChanged(nameof(TestQuoteResultText));
            });
        BackCommand = ReactiveCommand.CreateFromTask(ConfirmBack);
        SetTabCommand = ReactiveCommand.Create<string?>(s =>
        {
            if (int.TryParse(s, out var i))
            {
                SelectedTabIndex = i;
            }
        });
        AddChoreCommand = ReactiveCommand.Create(() => Chores.Add(NewChoreVm(new Chore())));
        AddOutfitRuleCommand = ReactiveCommand.Create(() => OutfitRules.Add(NewRuleVm(new OutfitRule())));
        AddForcedOutfitPresetCommand = ReactiveCommand.Create(() => OpenForcedOutfitPresetEditor(new ForcedOutfitPreset()));
        TestRulesCommand = ReactiveCommand.Create(() =>
            TestOutfitDialog = new TestOutfitDialogViewModel(
                WardrobeItems.Select(w => w.ToModel()).ToList(),
                OutfitRules.Select(r => r.ToModel()).ToList(), L));
        CloseTestOutfitDialogCommand = ReactiveCommand.Create(() => TestOutfitDialog = null);
        ExportContractCommand = ReactiveCommand.CreateFromTask(ExportContract);
        ImportContractCommand = ReactiveCommand.CreateFromTask(ImportContract);
    }

    private async Task ExportContract()
    {
        var path = await ContractFileDialogs.PickSaveAsync(L.T("Export contract"));
        if (path is null)
        {
            return;
        }

        try
        {
            var contracts = new GameContractService(_repo);
            await File.WriteAllTextAsync(path, contracts.Serialize(contracts.Build()));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ContentActionStatusMessage = L.T("The contract could not be written: {0}", e.Message);
            return;
        }

        // The contract is built from the saved profile, so pending edits in this screen are not in it.
        ContentActionStatusMessage = L.T("Contract exported.") +
                                     (IsDirty ? L.T(" Unsaved changes in Settings are not included.") : "");
    }

    private async Task ImportContract()
    {
        var outcome = await new ContractImportFlow(new GameContractService(_repo), _mainVm.Localizer)
            .RunAsync(ShowContractDialog, IsDirty);

        switch (outcome.Kind)
        {
            case ContractImportOutcomeKind.Failed:
                ContentActionStatusMessage = outcome.Message;
                break;
            case ContractImportOutcomeKind.Applied:
                RebuildScreen(outcome.Message);
                break;
        }
    }

    private async Task<bool> ShowContractDialog(ConfirmDialogViewModel dialog)
    {
        ContractDialog = dialog;
        var confirmed = await dialog.Result;
        ContractDialog = null;
        return confirmed;
    }

    /// <summary>
    ///     Rebuilds the whole screen from the repo so every collection reflects the saved profile — used after
    ///     a content/contract import replaced it, and by "Discard" to drop pending edits.
    /// </summary>
    private void RebuildScreen(string? statusMessage = null)
    {
        var fresh = new SettingsViewModel(_mainVm, _returnTo, statusMessage);
        _mainVm.ReplaceSettingsCache(fresh);
        NavigateAway(fresh);
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    /// <summary>
    ///     Probes cameras and other optional hardware off the UI thread, then applies the results once
    ///     ready — see the constructor for why this isn't done synchronously.
    /// </summary>
    private async Task LoadHardwareAvailability(int persistedCameraIndex)
    {
        var indices = await Task.Run(() => WebcamPreviewService.EnumerateCameras());
        if (indices.Count == 0)
        {
            indices = [persistedCameraIndex];
        }

        var names = await WebcamPreviewService.GetFriendlyNamesAsync(indices);
        var options = indices.Zip(names, (idx, name) => new CameraOption(idx, name)).ToList();
        var availability = HardwareAvailability.Check(indices, persistedCameraIndex, _mainVm.Stt);

        Dispatcher.UIThread.Post(() =>
        {
            AvailableCameras = options;
            if (options.Any(c => c.Index == persistedCameraIndex))
            {
                SyncSelectedCamera(persistedCameraIndex);
            }
            else
            {
                SelectedCamera = options[0]; // saved camera is gone: a real change for the user to save
            }

            WebcamStatusDisplay = availability.HasWebcam ? L.T("Available") : L.T("Not available");
            var speechAvailable = availability.HasSpeechRecognizer && availability.HasMicrophone;
            SpeechRecognitionStatusDisplay = speechAvailable ? L.T("Available") : L.T("Not available");
            IsMicrophoneAvailableForTest = speechAvailable;
            TestQuoteUnavailableTooltip = speechAvailable ? null : L.T("No speech recognition engine is installed upon this machine. You will be asked to write out any spoken citation by hand instead.");
        });
    }

    /// <summary>
    ///     Shows <paramref name="index" /> as the saved camera — its real name replacing the startup placeholder,
    ///     or a pick made in a live selector elsewhere — without marking the General tab dirty.
    /// </summary>
    internal void SyncSelectedCamera(int index)
    {
        var option = AvailableCameras.FirstOrDefault(c => c.Index == index) ?? new CameraOption(index, $"Camera {index}");
        _snapshot.Item9 = option;
        SelectedCamera = option;
    }

    /// <summary>Repoints the "back"/"discard" navigation target when the cached instance is reopened from a different screen.</summary>
    internal void SetReturnTarget(Func<ViewModelBase> returnTo)
    {
        _returnTo = returnTo;
    }

    // --- Collections ---
    public ObservableCollection<ChoreEditViewModel> Chores { get; } = [];
    public ObservableCollection<ForcedOutfitPresetSummaryViewModel> ForcedOutfitPresets { get; } = [];
    public ObservableCollection<PermissionTypeEditViewModel> PermissionTypes { get; } = [];
    public ObservableCollection<PlugAndPlayItemEditViewModel> PlugAndPlayItems { get; } = [];
    public ObservableCollection<DiaperPlayItemEditViewModel> DiaperPlayItems { get; } = [];
    public ObservableCollection<ChastityDeviceItemEditViewModel> ChastityDeviceItems { get; } = [];
    public ObservableCollection<WardrobeEditViewModel> WardrobeItems { get; } = [];
    public ObservableCollection<OutfitRuleEditViewModel> OutfitRules { get; } = [];

    // Each language under its own name (endonym), so it can be found whatever the current language.
    public IReadOnlyList<Option<string>> AvailableLanguages { get; } =
    [
        new("en", "English"),
        new("it", "Italiano"),
    ];

    public bool IsDirty =>
        IsGeneralDirty || IsChoresDirty || IsPopupsDirty || IsPunishmentsDirty || IsWardrobeDirty || IsRulesDirty ||
        IsPermissionsDirty;

    /// <summary>The master/detail Wardrobe screen, working on <see cref="WardrobeItems" /> and <see cref="OutfitRules" />.</summary>
    public WardrobeViewModel Wardrobe { get; }

    public decimal DailyInterestRatePercent
    {
        get => Math.Round((decimal)DailyInterestRate * 100m, 2);
        set => DailyInterestRate = (double)(value / 100m);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex != value)
            {
                _ = TryChangeTab(value);
            }
        }
    }

    // --- Commands ---
    public ICommand SaveCommand { get; }
    public ICommand QuickSaveCommand { get; }
    public ICommand StartTestQuoteListeningCommand { get; }
    public ICommand StopTestQuoteListeningCommand { get; }

    /// <summary>A fixed phrase (not one from the in-game quote bank) chosen to exercise normalization: mixed
    /// case, an apostrophe, a question mark, a percentage, an em dash.</summary>
    public string TestQuotePhrase => L.T("Well, isn't that lovely? A 100% success rate — obviously.");

    private bool HasTestQuoteResult => TestQuoteScore is not null && !IsTestQuoteListening;

    /// <summary>Whether the last test recording's similarity meets the live (possibly unsaved) threshold.</summary>
    private bool TestQuotePassed => TestQuoteScore is { } score && score * 100 >= QuoteMatchMinSimilarityPercent;

    public bool ShowTestQuotePassResult => HasTestQuoteResult && TestQuotePassed;
    public bool ShowTestQuoteFailResult => HasTestQuoteResult && !TestQuotePassed;

    public string TestQuoteResultText => TestQuotePassed
        ? L.T("Recognized: {0}%. The threshold is met.", ScorePercent)
        : L.T("Recognized: {0}%. Below the threshold: try again, speaking clearly.", ScorePercent);

    private int ScorePercent => (int)Math.Round((TestQuoteScore ?? 0) * 100);

    public string? AppellativesWarning => GameSettings.AppellativesWarning(Appellatives, L);
    public ICommand BackCommand { get; }
    public ICommand SetTabCommand { get; }
    public ICommand AddChoreCommand { get; }
    public ICommand AddOutfitRuleCommand { get; }
    public ICommand TestRulesCommand { get; }
    public ICommand CloseTestOutfitDialogCommand { get; }
    public ICommand AddForcedOutfitPresetCommand { get; }
    public ICommand ExportContractCommand { get; }
    public ICommand ImportContractCommand { get; }

    // --- Wardrobe helpers ---

    /// <summary>
    ///     Eligibility chips taken off their list (item deleted, layer removed). Their stored config is deleted on Save,
    ///     so Discard and undo can still bring a chip back with its settings.
    /// </summary>
    private readonly List<EligibilityChipViewModel> _detachedChips = [];

    private WardrobeEditViewModel NewWardrobeVm(ClothingItem item)
    {
        var vm = new WardrobeEditViewModel(item, L);
        WireWardrobeItem(vm);
        return vm;
    }

    private void WireWardrobeItem(WardrobeEditViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WardrobeEditViewModel.Name):
                case nameof(WardrobeEditViewModel.Multiplier):
                case nameof(WardrobeEditViewModel.Weight):
                    IsWardrobeDirty = true;
                    break;
            }
        };
        vm.Layers.CollectionChanged += (_, _) =>
        {
            IsWardrobeDirty = true;
            SyncEligibilityChips(vm);
        };
        vm.Slots.CollectionChanged += (_, _) => IsWardrobeDirty = true;
        vm.Tags.CollectionChanged += (_, _) => IsWardrobeDirty = true;
    }

    /// <summary>
    ///     Adds/removes this item's chip from each eligibility list (Plug &amp; Play, Diaper Play,
    ///     Chastity Device) so the lists reflect the item's current Layers immediately — needed both
    ///     for a freshly-added row (whose initial Layers never fire Layers.CollectionChanged, since
    ///     they're set directly in the WardrobeEditViewModel constructor) and for later layer toggles
    ///     on any existing item. <paramref name="removed" /> drops the item's chips (and persisted
    ///     configs) from every list.
    /// </summary>
    private void SyncEligibilityChips(WardrobeEditViewModel item, bool removed = false)
    {
        SyncEligibilityChip(PlugAndPlayItems, item, !removed && item.Layers.Contains(4), // 4 = Accessories
            w => new PlugAndPlayItemEditViewModel(w, null));
        SyncEligibilityChip(DiaperPlayItems, item, !removed && item.Layers.Contains(0), // 0 = Underwear
            w => new DiaperPlayItemEditViewModel(w, null));
        SyncEligibilityChip(ChastityDeviceItems, item, !removed && item.Layers.Contains(0),
            w => new ChastityDeviceItemEditViewModel(w, null));
    }

    private void SyncEligibilityChip<TChip>(
        ObservableCollection<TChip> chips,
        WardrobeEditViewModel item,
        bool shouldExist,
        Func<WardrobeEditViewModel, TChip> factory)
        where TChip : EligibilityChipViewModel
    {
        var existingChip = chips.FirstOrDefault(c => ReferenceEquals(c.Source, item));
        if (shouldExist && existingChip is null)
        {
            var chip = _detachedChips.OfType<TChip>().FirstOrDefault(c => ReferenceEquals(c.Source, item))
                       ?? WireChild(factory(item), () => IsGeneralDirty = true);
            _detachedChips.Remove(chip);
            chips.Add(chip);
        }
        else if (!shouldExist && existingChip is not null)
        {
            chips.Remove(existingChip);
            _detachedChips.Add(existingChip);
        }
    }

    // --- Generic helpers ---
    private static T WireChild<T>(T vm, Action markDirty) where T : INotifyPropertyChanged
    {
        vm.PropertyChanged += (_, _) => markDirty();
        return vm;
    }

    private static void RemoveFrom<T>(ObservableCollection<T> collection, int id, Action<int> deleteFromRepo)
        where T : class
    {
        var item = collection.FirstOrDefault(i => ((dynamic)i).Id == id);
        if (item is null)
        {
            return;
        }

        collection.Remove(item);
        if (id > 0)
        {
            deleteFromRepo(id);
        }
    }

    private ChoreEditViewModel NewChoreVm(Chore chore)
    {
        return WireChild(new ChoreEditViewModel(chore, id => RemoveFrom(Chores, id, _repo.DeleteChore)),
            () => IsChoresDirty = true);
    }

    private OutfitRuleEditViewModel NewRuleVm(OutfitRule rule)
    {
        // Removed by reference (new rules all have Id 0); the database follows on Save, like wardrobe items (SaveAll).
        OutfitRuleEditViewModel vm = null!;
        vm = WireChild(new OutfitRuleEditViewModel(rule, Wardrobe.Tags, _ => OutfitRules.Remove(vm), L),
            () => IsRulesDirty = true);
        return vm;
    }

    /// <summary>Saves <paramref name="model" /> and returns the Id the save assigned to it.</summary>
    private static int Persist<T>(T model, Action<T> save, Func<T, int> id)
    {
        save(model);
        return id(model);
    }

    // --- Forced Outfit presets ---
    private void OpenForcedOutfitPresetEditor(ForcedOutfitPresetSummaryViewModel summary)
    {
        var preset = _repo.GetForcedOutfitPresets().FirstOrDefault(p => p.Id == summary.Id) ?? new ForcedOutfitPreset();
        OpenForcedOutfitPresetEditor(preset);
    }

    private void OpenForcedOutfitPresetEditor(ForcedOutfitPreset preset)
    {
        NavigateAway(new ForcedOutfitPresetEditorViewModel(_mainVm, _repo, preset, () =>
        {
            RefreshForcedOutfitPresets();
            return this;
        }));
    }

    private void RefreshForcedOutfitPresets()
    {
        ForcedOutfitPresets.Clear();
        foreach (var p in _repo.GetForcedOutfitPresets())
        {
            ForcedOutfitPresets.Add(new ForcedOutfitPresetSummaryViewModel(p, _repo, OpenForcedOutfitPresetEditor,
                id => RemoveFrom(ForcedOutfitPresets, id, _repo.DeleteForcedOutfitPreset), L));
        }
    }

    // --- Tab change interception ---
    private async Task TryChangeTab(int newIndex)
    {
        // A name being typed in the Wardrobe is committed first; a refused (duplicate) one keeps the tab and its box.
        if (!Wardrobe.CommitRename())
        {
            this.RaisePropertyChanged(nameof(SelectedTabIndex));
            return;
        }

        if (!IsDirty)
        {
            SetTab(newIndex);
            return;
        }

        switch (await ShowSaveDialog(L.T("You have unsaved changes in this tab.")))
        {
            case SaveDialogResult.Save:
                SaveInPlace();
                SetTab(newIndex);
                break;
            case SaveDialogResult.Discard:
                RebuildScreen();
                break;
            case SaveDialogResult.Cancel: this.RaisePropertyChanged(nameof(SelectedTabIndex)); break;
        }
    }

    private void SetTab(int index)
    {
        Wardrobe.DismissNotice();
        _selectedTabIndex = index;
        this.RaisePropertyChanged(nameof(SelectedTabIndex));
    }

    /// <summary>
    ///     Every navigation away from this screen goes through here instead of <c>_mainVm.NavigateTo</c>
    ///     directly, so an in-progress "try your sensitivity" test recording is always stopped — otherwise the
    ///     microphone would stay open until the 90s safety cap even though nothing on screen shows it anymore.
    /// </summary>
    private void NavigateAway(ViewModelBase target)
    {
        if (IsTestQuoteListening)
        {
            _mainVm.Stt.CancelTestListening();
            IsTestQuoteListening = false;
        }

        _mainVm.NavigateTo(target);
    }

    private async Task ConfirmBack()
    {
        if (!IsDirty)
        {
            NavigateAway(_returnTo());
            return;
        }

        switch (await ShowSaveDialog(L.T("You have unsaved changes. What would you like to do?")))
        {
            case SaveDialogResult.Save:
                if (Save())
                {
                    NavigateAway(_returnTo());
                }

                break;
            case SaveDialogResult.Discard:
                // The cached screen still holds the discarded edits; the next visit must reload the profile.
                _mainVm.InvalidateSettingsCache();
                NavigateAway(_returnTo());
                break;
            case SaveDialogResult.Cancel: break;
        }
    }

    private async Task<SaveDialogResult> ShowSaveDialog(string message)
    {
        var dialog = new SaveDialogViewModel(L.T("Unsaved Changes"), message);
        ActiveDialog = dialog;
        var result = await dialog.Result;
        ActiveDialog = null;
        return result;
    }

    // --- Snapshot & dirty (tuple comparison) ---
    private void TakeSnapshot()
    {
        _snapshot = CurrentValues();
    }

    private (string, string, int, decimal, decimal, double, decimal, int, CameraOption, Option<string>)
        CurrentValues()
    {
        return (_playerName, _appellatives, _autoSaveInterval, _minDebt, _maxDebt, _dailyInterestRate,
            _idleWageAmount, _idleWageInterval, _selectedCamera, _selectedLanguage);
    }

    private void UpdateGeneralDirty()
    {
        IsGeneralDirty = CurrentValues() != _snapshot;
    }

    /// <summary>
    ///     Routes every property change to its tab's dirty flag. The General fields in the snapshot tuple are
    ///     compared against it, so reverting one clears the flag again; every other tab is dirty on any edit.
    ///     Non-General tabs are matched by property-name prefix, so a new setting only needs a matching name.
    /// </summary>
    private void OnSettingChanged(string? name)
    {
        switch (name)
        {
            case nameof(PlayerName) or nameof(AutoSaveInterval) or nameof(MinDebt) or nameof(MaxDebt)
                or nameof(DailyInterestRate) or nameof(IdleWageAmount) or nameof(IdleWageInterval)
                or nameof(SelectedCamera):
                UpdateGeneralDirty();
                break;
            case nameof(SelectedLanguage):
                UpdateGeneralDirty();
                _ = ConfirmLanguageChangeAsync();
                break;
            case nameof(Appellatives):
                UpdateGeneralDirty();
                this.RaisePropertyChanged(nameof(AppellativesWarning));
                break;
            case nameof(PlugAndPlayEnabled) or nameof(DiaperPlayEnabled):
                IsGeneralDirty = true;
                break;
            case nameof(CaptureMinInterval) or nameof(CaptureMaxInterval)
                or nameof(CaptureVideoMin) or nameof(CaptureVideoMax):
                IsChoresDirty = true;
                break;
            case nameof(FineSeverityValue):
            case { } n when n.StartsWith("Cornertime") || n.StartsWith("WritingLines") || n.StartsWith("ForcedOutfit"):
                IsPunishmentsDirty = true;
                break;
            case { } n when n.StartsWith("Popup") || n.StartsWith("Hydration") || n.StartsWith("Curtsy") ||
                            n.StartsWith("Quote") || n.StartsWith("Kneel"):
                IsPopupsDirty = true;
                break;
        }
    }

    private bool _contentRestored;
    private bool _languageChanged;
    private bool _restoreContentOnSave;
    private bool _revertingLanguage;
    private string _confirmedLanguage = "";

    /// <summary>
    ///     Asked as soon as the language selector changes (not at save time, so the other edits keep their
    ///     context): restore the new language's defaults on the next save, keep the current content, or cancel
    ///     and put the selector back.
    /// </summary>
    private async Task ConfirmLanguageChangeAsync()
    {
        var code = SelectedLanguage.Code;
        if (_revertingLanguage || code == _confirmedLanguage)
        {
            return;
        }

        if (code == _repo.GetGameSettings().Language)
        {
            _restoreContentOnSave = false;
            _confirmedLanguage = code;
            return;
        }

        // In the chosen language, so the player who picked it can read the warning; Cancel has the focus and Esc
        // cancels, so a language picked by mistake is easy to back out of.
        var target = new Localizer();
        target.SetLanguage(code);
        var dialog = new ConfirmDialogViewModel(target.T("Change language"),
            target.T("Wardrobe, outfit rules, chores and gameplay settings are stored in the current language. Restore replaces them with the defaults of the new language when you save; your edits to them are lost. Keep leaves them unchanged."),
            target.T("Restore"), target.T("Cancel"), target.T("Keep")) { CancelIsDefault = true };
        if (!await ShowContractDialog(dialog))
        {
            _revertingLanguage = true;
            SelectedLanguage = AvailableLanguages.First(l => l.Code == _confirmedLanguage);
            _revertingLanguage = false;
            return;
        }

        _confirmedLanguage = code;
        _restoreContentOnSave = !dialog.SecondaryChosen;
        SaveInPlace();
    }

    /// <summary>
    ///     Saves everything; when the user chose to restore defaults for a new language, the profile is also
    ///     reseeded (<see cref="_contentRestored" /> tells the caller to rebuild the screen). A Wardrobe rename in
    ///     progress is committed first; when its name is refused (duplicate) nothing is saved and false is returned.
    /// </summary>
    private bool Save()
    {
        CommitRanges();
        if (!Wardrobe.CommitRename())
        {
            return false;
        }

        // Saving commits the deletes: an undo afterwards could no longer bring back the deleted eligibility configs.
        Wardrobe.DismissNotice();
        _contentRestored = _restoreContentOnSave;
        _restoreContentOnSave = false;
        _languageChanged = SelectedLanguage.Code != _repo.GetGameSettings().Language;
        if (_languageChanged)
        {
            App.ApplyLanguage(SelectedLanguage.Code);
        }

        SaveAll();

        if (_contentRestored)
        {
            DataSeeder.Seed(_repo, SelectedLanguage.Code, L);
        }

        TakeSnapshot();
        IsGeneralDirty = IsChoresDirty = IsPopupsDirty = IsPunishmentsDirty = IsWardrobeDirty = IsRulesDirty =
            IsPermissionsDirty = false;
        return true;
    }

    /// <summary>Save without leaving: rebuilds the screen when the content was reseeded or the language changed.</summary>
    private void SaveInPlace()
    {
        if (!Save())
        {
            return;
        }

        if (_contentRestored || _languageChanged)
        {
            RebuildScreen();
        }
    }

    private void SaveAndBack()
    {
        if (!Save())
        {
            return;
        }

        if (_languageChanged)
        {
            // Texts are resolved when a screen is built, so drop the cached one built in the old language.
            _mainVm.InvalidateSettingsCache();
        }

        NavigateAway(_returnTo());
    }

    /// <summary>
    ///     Starts an open-ended test recording of <see cref="TestQuotePhrase" /> — no fixed duration, the user
    ///     decides when they're done by pressing Stop (<see cref="StopTestQuoteListeningAsync" />).
    /// </summary>
    private void StartTestQuoteListening()
    {
        TestQuoteScore = null;
        IsTestQuoteListening = true;
        _mainVm.Stt.StartTestListening();
    }

    /// <summary>
    ///     Stops the recording started by <see cref="StartTestQuoteListening" /> and reports the raw similarity
    ///     against <see cref="TestQuotePhrase" />, so the user can tune
    ///     <see cref="QuoteMatchMinSimilarityPercent" /> before saving — compared against the live (possibly
    ///     unsaved) UI value, not what's persisted yet.
    /// </summary>
    private async Task StopTestQuoteListeningAsync()
    {
        try
        {
            TestQuoteScore = await _mainVm.Stt.StopTestListeningAsync(TestQuotePhrase);
        }
        catch
        {
            TestQuoteScore = 0;
        }
        finally
        {
            IsTestQuoteListening = false;
        }
    }

    /// <summary>
    ///     An edit ended (focus left a field, a spinner step, a save): every min/max pair out of order is put back in
    ///     order, the side edited last pushing the other one.
    /// </summary>
    public void CommitRanges()
    {
        _ranges.Commit();
        foreach (var item in PlugAndPlayItems)
        {
            item.Ranges.Commit();
        }
    }

    // --- Persist ---
    private void SaveAll()
    {
        var s = _repo.GetGameSettings();
        s.PlayerName = PlayerName;
        s.Appellatives = Appellatives;
        s.AutoSaveIntervalMinutes = AutoSaveInterval;
        s.MinDebt = MinDebt;
        s.MaxDebt = MaxDebt;
        s.DailyInterestRate = DailyInterestRate;
        s.IdleWageAmount = IdleWageAmount;
        s.IdleWageIntervalMinutes = IdleWageInterval;
        s.PopupMinIntervalSeconds = PopupMinInterval;
        s.PopupMaxIntervalSeconds = PopupMaxInterval;
        s.PopupTimeoutSeconds = PopupTimeout;
        s.HydrationEnabled = HydrationEnabled;
        s.HydrationMinIntervalMinutes = HydrationMinInterval;
        s.CurtsyEnabled = CurtsyEnabled;
        s.CurtsyVideoDurationSeconds = CurtsyVideoDuration;
        s.QuoteReadingEnabled = QuoteReadingEnabled;
        s.QuoteListenTimeoutSeconds = QuoteListenTimeout;
        s.QuoteMatchMinSimilarityPercent = QuoteMatchMinSimilarityPercent;
        s.BusyCaptureMinIntervalSeconds = CaptureMinInterval;
        s.BusyCaptureMaxIntervalSeconds = CaptureMaxInterval;
        s.BusyCaptureVideoMinSeconds = CaptureVideoMin;
        s.BusyCaptureVideoMaxSeconds = CaptureVideoMax;
        s.KneelEnabled = KneelEnabled;
        s.KneelMinDurationSeconds = KneelMinDuration;
        s.KneelMaxDurationSeconds = KneelMaxDuration;
        s.KneelPhotoMinIntervalSeconds = KneelPhotoMinInterval;
        s.KneelPhotoMaxIntervalSeconds = KneelPhotoMaxInterval;
        s.SelectedCameraIndex = SelectedCamera.Index;
        _mainVm.Webcam.PreferredCameraIndex = SelectedCamera.Index;
        s.Language = SelectedLanguage.Code;
        s.PlugAndPlayEnabled = PlugAndPlayEnabled;
        s.DiaperPlayEnabled = DiaperPlayEnabled;
        s.FineSeverityValue = FineSeverityValue;
        s.CornertimeEnabled = CornertimeEnabled;
        s.CornertimeSeverityValue = CornertimeSeverityValue;
        s.CornertimeMinRequiredSeconds = CornertimeMinRequiredSeconds;
        s.CornertimeMaxOverrunSeconds = CornertimeMaxOverrunSeconds;
        s.CornertimeMaxRequiredSeconds = CornertimeMaxRequiredSeconds;
        s.CornertimeMovementCooldownMinSeconds = CornertimeMovementCooldownMinSeconds;
        s.CornertimeMovementCooldownMaxSeconds = CornertimeMovementCooldownMaxSeconds;
        s.CornertimeMovementSensitivityPercent = CornertimeMovementSensitivityPercent;
        s.CornertimeMovementPenaltyMinSeconds = CornertimeMovementPenaltyMinSeconds;
        s.CornertimeMovementPenaltyMaxSeconds = CornertimeMovementPenaltyMaxSeconds;
        s.CornertimeVoiceEnabled = CornertimeVoiceEnabled;
        s.WritingLinesEnabled = WritingLinesEnabled;
        s.WritingLinesSeverityValue = WritingLinesSeverityValue;
        s.WritingLinesMinLines = WritingLinesMinLines;
        s.WritingLinesMaxLines = WritingLinesMaxLines;
        s.WritingLinesPenaltyLinesPerMistake = WritingLinesPenaltyLinesPerMistake;
        s.WritingLinesNoIdleEnabled = WritingLinesNoIdleEnabled;
        s.WritingLinesNoIdleTimeoutSeconds = WritingLinesNoIdleTimeoutSeconds;
        s.WritingLinesNoIdleLinesToAdd = WritingLinesNoIdleLinesToAdd;
        s.WritingLinesInterruptionsEnabled = WritingLinesInterruptionsEnabled;
        s.WritingLinesInterruptionMinIntervalSeconds = WritingLinesInterruptionMinIntervalSeconds;
        s.WritingLinesInterruptionMaxIntervalSeconds = WritingLinesInterruptionMaxIntervalSeconds;
        s.ForcedOutfitEnabled = ForcedOutfitEnabled;
        s.ForcedOutfitMaxHours = ForcedOutfitMaxHours;
        _repo.SaveGameSettings(s);

        var catalog = Wardrobe.NormalizeForSave();

        // Wardrobe items and rules removed on screen leave the database only now. Done before the upserts below,
        // which give new rows (Id 0) their real Id.
        var keptItems = WardrobeItems.Select(w => w.Id).ToHashSet();
        foreach (var gone in _repo.GetWardrobe().Where(i => !keptItems.Contains(i.Id)))
        {
            _repo.DeleteClothingItem(gone.Id);
        }

        var keptRules = OutfitRules.Select(r => r.Id).ToHashSet();
        foreach (var gone in _repo.GetOutfitRules().Where(r => !keptRules.Contains(r.Id)))
        {
            _repo.DeleteOutfitRule(gone.Id);
        }

        // Newly-added rows have Id 0 until the first save assigns a real one; writing it back keeps
        // every later save from upserting them again as fresh duplicates.
        foreach (var c in Chores) c.Id = Persist(c.ToModel(), _repo.SaveChore, m => m.Id);
        foreach (var w in WardrobeItems) w.Id = Persist(w.ToModel(), _repo.SaveClothingItem, m => m.Id);
        foreach (var p in PermissionTypes) p.Id = Persist(p.ToModel(), _repo.SavePermissionType, m => m.Id);
        foreach (var r in OutfitRules) r.Id = Persist(r.ToModel(), _repo.SaveOutfitRule, m => m.Id);
        _repo.SaveWardrobeCatalog(catalog);

        foreach (var chip in PlugAndPlayItems.Concat<EligibilityChipViewModel>(DiaperPlayItems).Concat(ChastityDeviceItems))
        {
            chip.Persist(_repo);
        }

        foreach (var chip in _detachedChips)
        {
            chip.DeleteStoredConfig(_repo);
        }

        _detachedChips.Clear();
    }
}

/// <summary>A ComboBox entry: a stored code shown under a localized label.</summary>
public record Option<T>(T Code, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public partial class ChoreEditViewModel : ViewModelBase
{
    [Reactive] private string _description = "";
    [Reactive] private int _frequencyDays;
    [Reactive] private int _minCompletionMinutes;
    [Reactive] private string _name = "";
    [Reactive] private decimal _value;

    public ChoreEditViewModel(Chore chore, Action<int> onDelete)
    {
        Id = chore.Id;
        _name = chore.Name;
        _description = chore.Description;
        _value = chore.Value;
        _frequencyDays = chore.FrequencyDays;
        _minCompletionMinutes = chore.MinCompletionMinutes;
        LastCompletedAt = chore.LastCompletedAt;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; internal set; }
    public DateTime? LastCompletedAt { get; }
    public ICommand DeleteCommand { get; }

    public Chore ToModel()
    {
        return new Chore
        {
            Id = Id, Name = Name, Description = Description, Value = Value, FrequencyDays = FrequencyDays,
            MinCompletionMinutes = MinCompletionMinutes, LastCompletedAt = LastCompletedAt,
        };
    }
}

public partial class PermissionTypeEditViewModel : ViewModelBase
{
    [Reactive] private int _baseGrantPercent;
    [Reactive] private int _durationMinutes;
    [Reactive] private int _cooldownMinutes;

    public PermissionTypeEditViewModel(PermissionTypeConfig config, ILocalizer l)
    {
        Id = config.Id;
        Kind = config.Kind;
        Name = PermissionKindDisplay.NameOf(config.Kind, l);
        _baseGrantPercent = config.BaseGrantPercent;
        _durationMinutes = config.DurationMinutes;
        _cooldownMinutes = config.CooldownMinutes;
    }

    public int Id { get; internal set; }
    public PermissionKind Kind { get; }
    public string Name { get; }

    public PermissionTypeConfig ToModel()
    {
        return new PermissionTypeConfig
        {
            Id = Id, Kind = Kind, BaseGrantPercent = BaseGrantPercent,
            DurationMinutes = DurationMinutes, CooldownMinutes = CooldownMinutes,
        };
    }
}

public partial class ForcedOutfitPresetSummaryViewModel : ViewModelBase
{
    [Reactive] private bool _isEnabled;

    public ForcedOutfitPresetSummaryViewModel(ForcedOutfitPreset preset, ProfileRepository repo,
        Action<ForcedOutfitPresetSummaryViewModel> onEdit, Action<int> onDelete, ILocalizer L)
    {
        Id = preset.Id;
        Name = preset.Name;
        _isEnabled = preset.IsEnabled;
        SummaryLabel = L.T("{0:0.00}h/point · {1}",
            preset.HoursPerSeverityPoint,
            preset.IsFullOutfit ? L.T("Full outfit") : L.T("Partial outfit"));

        this.WhenAnyValue(x => x.IsEnabled).Skip(1).Subscribe(enabled =>
        {
            preset.IsEnabled = enabled;
            repo.SaveForcedOutfitPreset(preset);
        });

        EditCommand = ReactiveCommand.Create(() => onEdit(this));
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; }
    public string Name { get; }
    public string SummaryLabel { get; }

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
}

/// <summary>
///     Base of the per-mechanism wardrobe eligibility chip VMs (Plug &amp; Play, Diaper Play,
///     Chastity Device). Holding a live reference to the source <see cref="WardrobeEditViewModel" />
///     instead of a copied id keeps the chip correct across renames and across the Id reassignment
///     that happens when a brand-new (unsaved, Id == 0) wardrobe item is first persisted.
/// </summary>
public abstract partial class EligibilityChipViewModel : ViewModelBase
{
    [Reactive] private bool _isEligible;

    protected EligibilityChipViewModel(WardrobeEditViewModel source, int? configId)
    {
        Source = source;
        ConfigId = configId;
        _isEligible = configId is not null;
    }

    public WardrobeEditViewModel Source { get; }
    public int ClothingItemId => Source.Id;
    public string Name => Source.Name;
    public int? ConfigId { get; private set; }

    /// <summary>Saves this chip's config and returns its Id.</summary>
    protected abstract int SaveConfig(ProfileRepository repo);

    protected abstract void DeleteConfig(ProfileRepository repo, int configId);

    /// <summary>Saves the config when eligible, deletes any stored one otherwise.</summary>
    public void Persist(ProfileRepository repo)
    {
        if (IsEligible)
        {
            ConfigId = SaveConfig(repo);
        }
        else
        {
            DeleteStoredConfig(repo);
        }
    }

    public void DeleteStoredConfig(ProfileRepository repo)
    {
        if (ConfigId is { } configId)
        {
            DeleteConfig(repo, configId);
            ConfigId = null;
        }
    }
}

public partial class PlugAndPlayItemEditViewModel : EligibilityChipViewModel
{
    [Reactive] private int _minDurationMinutes;
    [Reactive] private int _maxDurationMinutes;

    public PlugAndPlayItemEditViewModel(WardrobeEditViewModel source, PlugAndPlayItemConfig? existing)
        : base(source, existing?.Id)
    {
        _minDurationMinutes = existing?.MinDurationMinutes ?? 30;
        _maxDurationMinutes = existing?.MaxDurationMinutes ?? 120;
        Ranges = new LinkedRanges(this, (nameof(MinDurationMinutes), nameof(MaxDurationMinutes)));
    }

    public LinkedRanges Ranges { get; }

    protected override int SaveConfig(ProfileRepository repo)
    {
        var model = new PlugAndPlayItemConfig
        {
            Id = ConfigId ?? 0,
            ClothingItemId = ClothingItemId,
            MinDurationMinutes = MinDurationMinutes,
            MaxDurationMinutes = MaxDurationMinutes,
        };
        repo.SavePlugAndPlayItem(model);
        return model.Id;
    }

    protected override void DeleteConfig(ProfileRepository repo, int configId) => repo.DeletePlugAndPlayItem(configId);
}

public class DiaperPlayItemEditViewModel(WardrobeEditViewModel source, DiaperPlayItemConfig? existing)
    : EligibilityChipViewModel(source, existing?.Id)
{
    protected override int SaveConfig(ProfileRepository repo)
    {
        var model = new DiaperPlayItemConfig { Id = ConfigId ?? 0, ClothingItemId = ClothingItemId };
        repo.SaveDiaperPlayItem(model);
        return model.Id;
    }

    protected override void DeleteConfig(ProfileRepository repo, int configId) => repo.DeleteDiaperPlayItem(configId);
}

public class ChastityDeviceItemEditViewModel(WardrobeEditViewModel source, ChastityDeviceItemConfig? existing)
    : EligibilityChipViewModel(source, existing?.Id)
{
    protected override int SaveConfig(ProfileRepository repo)
    {
        var model = new ChastityDeviceItemConfig { Id = ConfigId ?? 0, ClothingItemId = ClothingItemId };
        repo.SaveChastityDeviceItem(model);
        return model.Id;
    }

    protected override void DeleteConfig(ProfileRepository repo, int configId) =>
        repo.DeleteChastityDeviceItem(configId);
}

public partial class WardrobeEditViewModel : ViewModelBase
{
    /// <summary>The single item the Wardrobe detail pane shows (highlighted row); presentation only, never saved.</summary>
    [Reactive] private bool _isActive;

    /// <summary>The row shows the rename box; presentation only, never saved.</summary>
    [Reactive] private bool _isEditing;

    /// <summary>Ticked in the Wardrobe list (the checked set); presentation only, never saved.</summary>
    [Reactive] private bool _isSelected;
    [Reactive] private double _multiplier;
    [Reactive] private string _name = "";
    [Reactive] private double _weight;

    public WardrobeEditViewModel(ClothingItem item, ILocalizer l)
    {
        _l = l;
        Id = item.Id;
        _name = item.Name;
        _multiplier = item.Multiplier;
        _weight = item.Weight;

        Layers = new ObservableCollection<int>(item.Layers);
        Slots = new ObservableCollection<string>(item.Slots);
        Tags = new ObservableCollection<string>(item.Tags);

        Layers.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(LayerDisplayNames));
            this.RaisePropertyChanged(nameof(LayerCaption));
        };
    }

    public int Id { get; internal set; }

    public ObservableCollection<int> Layers { get; }
    public ObservableCollection<string> Slots { get; }
    public ObservableCollection<string> Tags { get; }

    private readonly ILocalizer _l;

    public IEnumerable<string> LayerDisplayNames =>
        Layers.Select(o => WardrobeLayer.FindByOrder(o) is { } layer ? _l.T(layer.DisplayName) : o.ToString());

    /// <summary>The layers as one line for the Wardrobe list, e.g. "Underwear · Base".</summary>
    public string LayerCaption => string.Join(" · ", LayerDisplayNames);

    public ClothingItem ToModel()
    {
        return new ClothingItem
        {
            Id = Id,
            Name = Name,
            Layers = [.. Layers],
            Slots = [.. Slots],
            Tags = [.. Tags],
            Multiplier = Multiplier,
            Weight = Weight,
        };
    }
}

public partial class OutfitRuleEditViewModel : ViewModelBase
{
    [Reactive] private string _sourceTag;
    [Reactive] private string _relatedTag;
    [Reactive] private Option<OutfitRelation> _relationOption = null!;

    public OutfitRuleEditViewModel(OutfitRule rule, IEnumerable<string> tagSuggestions, Action<int> onDelete,
        ILocalizer l)
    {
        RelationOptions =
        [
            new(OutfitRelation.Requires, l.T("Requires")),
            new(OutfitRelation.Incompatible, l.T("Excludes")),
        ];
        Id = rule.Id;
        TagSuggestions = tagSuggestions;
        _sourceTag = rule.SourceTag;
        _relatedTag = rule.RelatedTag;
        _relationOption = RelationOptions.First(o => o.Code == rule.Relation);
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; internal set; }

    public IEnumerable<string> TagSuggestions { get; }
    public ICommand DeleteCommand { get; }

    // Relation is stored internally as a compact enum code (OutfitRelation); the ComboBox
    // shows a localized label instead of the raw enum name via this display wrapper.
    public IReadOnlyList<Option<OutfitRelation>> RelationOptions { get; }

    public OutfitRule ToModel()
    {
        return new OutfitRule
        {
            Id = Id, SourceTag = SourceTag.Trim(), RelatedTag = RelatedTag.Trim(), Relation = RelationOption.Code,
        };
    }
}
