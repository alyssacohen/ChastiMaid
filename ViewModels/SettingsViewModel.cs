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
    private readonly ProfileRepository _repo;
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
    [Reactive] private double _dailyInterestRate;
    [Reactive] private decimal _idleWageAmount;
    [Reactive] private int _idleWageInterval;
    [Reactive] private bool _isAddingNewItem;
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
    [Reactive] private decimal _newItemMultiplier = 1.0m;

    // --- New-item panel state ---
    [Reactive] private string _newItemName = string.Empty;
    [Reactive] private decimal _newItemWeight = 1.0m;

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
    [Reactive] private int? _wardrobeLayerFilter;

    // --- Wardrobe filter & selection ---
    [Reactive] private string _wardrobeNameFilter = string.Empty;
    [Reactive] private int _wardrobeSelectedCount;

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

        var initTags = allWardrobe.SelectMany(i => i.Tags).Distinct().OrderBy(s => s).ToList();
        var initSlots = allWardrobe.SelectMany(i => i.Slots).Distinct().OrderBy(s => s).ToList();
        foreach (var w in allWardrobe)
        {
            var vm = new WardrobeEditViewModel(w, RemoveWardrobeItem, initTags, initSlots, ExpandWardrobeItem);
            WireWardrobeItem(vm);
            WardrobeItems.Add(vm);
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

        foreach (var r in _repo.GetOutfitRules())
        {
            OutfitRules.Add(NewRuleVm(r));
        }

        // Filtered wardrobe view — reactive to filter controls and to add/remove on WardrobeItems
        this.WhenAnyValue(x => x.WardrobeNameFilter, x => x.WardrobeLayerFilter)
            .Subscribe(_ => RefreshFilteredWardrobe());
        WardrobeItems.CollectionChanged += (_, _) => RefreshFilteredWardrobe();

        TakeSnapshot();

        LayerFilterChips = new LayerFilterChipVm[] { new(null, "All", () => WardrobeLayerFilter = null) }
            .Concat(WardrobeLayer.All.Select(l =>
                new LayerFilterChipVm(l.Order, l.DisplayName,
                    () => WardrobeLayerFilter = WardrobeLayerFilter == l.Order ? null : l.Order)))
            .ToList();

        this.WhenAnyValue(x => x.WardrobeLayerFilter)
            .Subscribe(filter =>
            {
                foreach (var chip in LayerFilterChips)
                {
                    chip.IsActive = chip.Key == filter;
                }
            });

        Changed.Subscribe(e => OnSettingChanged(e.PropertyName));

        Chores.CollectionChanged += (_, _) => IsChoresDirty = true;
        WardrobeItems.CollectionChanged += (_, _) =>
        {
            IsWardrobeDirty = true;
            UpdateSelectedCount();
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
        ShowAddItemPanelCommand = ReactiveCommand.Create(() => IsAddingNewItem = true);
        CancelAddItemCommand = ReactiveCommand.Create(ResetNewItemPanel);
        var canAddWardrobe = this.WhenAnyValue(x => x.NewItemName, name => !string.IsNullOrWhiteSpace(name));
        AddWardrobeItemCommand = ReactiveCommand.Create(AddWardrobeItemFromNewRow, canAddWardrobe);
        AddOutfitRuleCommand = ReactiveCommand.Create(() => OutfitRules.Add(NewRuleVm(new OutfitRule())));
        AddForcedOutfitPresetCommand = ReactiveCommand.Create(() => OpenForcedOutfitPresetEditor(new ForcedOutfitPreset()));
        TestRulesCommand = ReactiveCommand.Create(() =>
            TestOutfitDialog = new TestOutfitDialogViewModel(
                WardrobeItems.Select(w => w.ToModel()).ToList(),
                OutfitRules.Select(r => r.ToModel()).ToList()));
        CloseTestOutfitDialogCommand = ReactiveCommand.Create(() => TestOutfitDialog = null);
        DeleteSelectedWardrobeItemsCommand = ReactiveCommand.Create(
            DeleteSelectedWardrobeItems,
            this.WhenAnyValue(x => x.WardrobeSelectedCount, n => n > 0));
        CollapseExpandedWardrobeItemCommand = ReactiveCommand.Create(() =>
        {
            ExpandWardrobeItem(null);
            if (IsAddingNewItem)
            {
                ResetNewItemPanel();
            }
        });
        ExportContractCommand = ReactiveCommand.CreateFromTask(ExportContract);
        ImportContractCommand = ReactiveCommand.CreateFromTask(ImportContract);
    }

    private async Task ExportContract()
    {
        var path = await ContractFileDialogs.PickSaveAsync(Resources.Get("ContractExportDialogTitle"));
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
            ContentActionStatusMessage = string.Format(Resources.Get("ContractExportFailedStatus"), e.Message);
            return;
        }

        // The contract is built from the saved profile, so pending edits in this screen are not in it.
        ContentActionStatusMessage = Resources.Get("ContractExportedStatus") +
                                     (IsDirty ? Resources.Get("ContractExportUnsavedNote") : "");
    }

    private async Task ImportContract()
    {
        var outcome = await new ContractImportFlow(new GameContractService(_repo))
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

            WebcamStatusDisplay = Resources.Get(availability.HasWebcam ? "HardwareStatusAvailable" : "HardwareStatusUnavailable");
            var speechAvailable = availability.HasSpeechRecognizer && availability.HasMicrophone;
            SpeechRecognitionStatusDisplay = Resources.Get(
                speechAvailable ? "HardwareStatusAvailable" : "HardwareStatusUnavailable");
            IsMicrophoneAvailableForTest = speechAvailable;
            TestQuoteUnavailableTooltip = speechAvailable ? null : Resources.Get("HardwareCheckSpeechRecognizerMissing");
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

    public IReadOnlyList<Option<string>> AvailableLanguages { get; } =
    [
        new("en", Resources.Get("LanguageEnglish")),
        new("it", Resources.Get("LanguageItalian")),
    ];

    public ObservableCollection<string> NewItemSlots { get; } = [];
    public ObservableCollection<string> NewItemTags { get; } = [];

    public IReadOnlyList<LayerToggleVm> NewItemLayerToggles { get; } =
        [.. WardrobeLayer.All.Select(l => new LayerToggleVm(l, false, _ => { }))];

    public IEnumerable<string> NewItemSlotSuggestions => AllUsedSlots;
    public IEnumerable<string> NewItemTagSuggestions => AllUsedTags;

    public bool IsDirty =>
        IsGeneralDirty || IsChoresDirty || IsPopupsDirty || IsPunishmentsDirty || IsWardrobeDirty || IsRulesDirty ||
        IsPermissionsDirty;
    public ObservableCollection<WardrobeEditViewModel> FilteredWardrobeItems { get; } = [];

    public decimal DailyInterestRatePercent
    {
        get => Math.Round((decimal)DailyInterestRate * 100m, 2);
        set => DailyInterestRate = (double)(value / 100m);
    }

    public IReadOnlyList<LayerFilterChipVm> LayerFilterChips { get; } = [];

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
    public string TestQuotePhrase => Resources.Get("SettingsTestQuoteFixedPhrase");

    private bool HasTestQuoteResult => TestQuoteScore is not null && !IsTestQuoteListening;

    /// <summary>Whether the last test recording's similarity meets the live (possibly unsaved) threshold.</summary>
    private bool TestQuotePassed => TestQuoteScore is { } score && score * 100 >= QuoteMatchMinSimilarityPercent;

    public bool ShowTestQuotePassResult => HasTestQuoteResult && TestQuotePassed;
    public bool ShowTestQuoteFailResult => HasTestQuoteResult && !TestQuotePassed;

    public string? AppellativesWarning
    {
        get
        {
            var raw = Appellatives.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (raw.Length > GameSettings.AppellativeMaxCount)
            {
                return string.Format(Resources.Get("AppellativesWarningTooManyFormat"), GameSettings.AppellativeMaxCount);
            }

            var tooLong = raw.FirstOrDefault(a => a.Length > GameSettings.AppellativeMaxLength);
            return tooLong is null
                ? null
                : string.Format(Resources.Get("AppellativesWarningTooLongFormat"), tooLong, GameSettings.AppellativeMaxLength);
        }
    }
    public ICommand BackCommand { get; }
    public ICommand SetTabCommand { get; }
    public ICommand AddChoreCommand { get; }
    public ICommand ShowAddItemPanelCommand { get; }
    public ICommand CancelAddItemCommand { get; }
    public ICommand AddWardrobeItemCommand { get; }
    public ICommand AddOutfitRuleCommand { get; }
    public ICommand TestRulesCommand { get; }
    public ICommand CloseTestOutfitDialogCommand { get; }
    public ICommand AddForcedOutfitPresetCommand { get; }
    public ICommand DeleteSelectedWardrobeItemsCommand { get; }
    public ICommand CollapseExpandedWardrobeItemCommand { get; }
    public ICommand ExportContractCommand { get; }
    public ICommand ImportContractCommand { get; }

    // --- Wardrobe helpers ---
    private IEnumerable<string> AllUsedTags => WardrobeItems.SelectMany(w => w.Tags).Distinct().OrderBy(s => s);
    private IEnumerable<string> AllUsedSlots => WardrobeItems.SelectMany(w => w.Slots).Distinct().OrderBy(s => s);

    private void WireWardrobeItem(WardrobeEditViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WardrobeEditViewModel.IsSelected):
                    UpdateSelectedCount();
                    break;
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
            chips.Add(WireChild(factory(item), () => IsGeneralDirty = true));
        }
        else if (!shouldExist && existingChip is not null)
        {
            chips.Remove(existingChip);
            existingChip.DeleteStoredConfig(_repo);
        }
    }

    private void RemoveWardrobeItem(WardrobeEditViewModel item)
    {
        WardrobeItems.Remove(item);
        SyncEligibilityChips(item, removed: true);
        if (item.Id > 0)
        {
            _repo.DeleteClothingItem(item.Id);
        }
    }

    /// <summary>Expands <paramref name="target" /> and collapses every other row; null collapses all.</summary>
    private void ExpandWardrobeItem(WardrobeEditViewModel? target)
    {
        foreach (var item in WardrobeItems)
        {
            item.IsExpanded = ReferenceEquals(item, target);
        }
    }

    private void UpdateSelectedCount()
    {
        WardrobeSelectedCount = WardrobeItems.Count(x => x.IsSelected);
    }

    private void AddWardrobeItemFromNewRow()
    {
        var item = new ClothingItem
        {
            Name = NewItemName,
            Layers = [.. NewItemLayerToggles.Where(t => t.IsChecked).Select(t => t.Order)],
            Slots = [.. NewItemSlots],
            Tags = [.. NewItemTags],
            Multiplier = (double)NewItemMultiplier,
            Weight = (double)NewItemWeight,
        };

        var vm = new WardrobeEditViewModel(item, RemoveWardrobeItem, AllUsedTags, AllUsedSlots, ExpandWardrobeItem);
        WireWardrobeItem(vm);
        WardrobeItems.Add(vm);
        SyncEligibilityChips(vm);

        ResetNewItemPanel();

        IsWardrobeDirty = true;
    }

    private void ResetNewItemPanel()
    {
        NewItemName = string.Empty;
        NewItemMultiplier = 1.0m;
        NewItemWeight = 1.0m;
        foreach (var t in NewItemLayerToggles) t.IsChecked = false;
        NewItemSlots.Clear();
        NewItemTags.Clear();
        IsAddingNewItem = false;
    }

    private void DeleteSelectedWardrobeItems()
    {
        foreach (var item in WardrobeItems.Where(x => x.IsSelected).ToList())
        {
            RemoveWardrobeItem(item);
        }
    }

    private static bool MatchesWardrobeFilter(WardrobeEditViewModel item, string nameFilter, int? layerFilter)
    {
        return (string.IsNullOrEmpty(nameFilter)
                || item.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)
                || item.Tags.Any(t => t.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)))
               && (layerFilter is null || item.Layers.Any(l => l == layerFilter.Value));
    }

    private void RefreshFilteredWardrobe()
    {
        FilteredWardrobeItems.Clear();
        foreach (var item in WardrobeItems.Where(i => MatchesWardrobeFilter(i, WardrobeNameFilter, WardrobeLayerFilter)))
        {
            FilteredWardrobeItems.Add(item);
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
        return WireChild(new OutfitRuleEditViewModel(rule, AllUsedTags,
            id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true);
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
                id => RemoveFrom(ForcedOutfitPresets, id, _repo.DeleteForcedOutfitPreset)));
        }
    }

    // --- Tab change interception ---
    private async Task TryChangeTab(int newIndex)
    {
        if (!IsDirty)
        {
            SetTab(newIndex);
            return;
        }

        switch (await ShowSaveDialog(Resources.Get("SettingsUnsavedTabMessage")))
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

        switch (await ShowSaveDialog(Resources.Get("SettingsUnsavedGeneralMessage")))
        {
            case SaveDialogResult.Save:
                Save();
                NavigateAway(_returnTo());
                break;
            case SaveDialogResult.Discard: NavigateAway(_returnTo()); break;
            case SaveDialogResult.Cancel: break;
        }
    }

    private async Task<SaveDialogResult> ShowSaveDialog(string message)
    {
        var dialog = new SaveDialogViewModel(Resources.Get("SettingsUnsavedChangesTitle"), message);
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
            case nameof(CaptureMinInterval) or nameof(CaptureMaxInterval):
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

        var dialog = new ConfirmDialogViewModel(Resources.Get("LanguageChangeTitle"),
            Resources.Get("LanguageChangeMessage"), Resources.Get("LanguageChangeRestore"),
            Resources.Get("Cancel"), Resources.Get("LanguageChangeKeep"));
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
    ///     reseeded (<see cref="_contentRestored" /> tells the caller to rebuild the screen).
    /// </summary>
    private void Save()
    {
        _contentRestored = _restoreContentOnSave;
        _restoreContentOnSave = false;
        _languageChanged = SelectedLanguage.Code != _repo.GetGameSettings().Language;
        if (_languageChanged)
        {
            App.ApplyLanguage(SelectedLanguage.Code);
            // A name still equal to a language's default follows the language; an edited one is left alone.
            if (new[] { "en", "it" }.Any(l => PlayerName == Resources.Get("DefaultPlayerName", new CultureInfo(l))))
            {
                PlayerName = Resources.Get("DefaultPlayerName");
            }
        }

        SaveAll();

        if (_contentRestored)
        {
            DataSeeder.Seed(_repo, SelectedLanguage.Code);
        }

        TakeSnapshot();
        IsGeneralDirty = IsChoresDirty = IsPopupsDirty = IsPunishmentsDirty = IsWardrobeDirty = IsRulesDirty =
            IsPermissionsDirty = false;
    }

    /// <summary>Save without leaving: rebuilds the screen when the content was reseeded or the language changed.</summary>
    private void SaveInPlace()
    {
        Save();
        if (_contentRestored || _languageChanged)
        {
            RebuildScreen();
        }
    }

    private void SaveAndBack()
    {
        Save();
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

        // Newly-added rows have Id 0 until the first save assigns a real one; writing it back keeps
        // every later save from upserting them again as fresh duplicates.
        foreach (var c in Chores) c.Id = Persist(c.ToModel(), _repo.SaveChore, m => m.Id);
        foreach (var w in WardrobeItems) w.Id = Persist(w.ToModel(), _repo.SaveClothingItem, m => m.Id);
        foreach (var p in PermissionTypes) p.Id = Persist(p.ToModel(), _repo.SavePermissionType, m => m.Id);
        foreach (var r in OutfitRules) r.Id = Persist(r.ToModel(), _repo.SaveOutfitRule, m => m.Id);

        foreach (var chip in PlugAndPlayItems.Concat<EligibilityChipViewModel>(DiaperPlayItems).Concat(ChastityDeviceItems))
        {
            chip.Persist(_repo);
        }
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

public class LayerFilterChipVm(int? key, string displayName, Action onSelect) : ReactiveObject
{
    private bool _isActive;
    public int? Key { get; } = key;
    public string DisplayName { get; } = displayName;

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    public ICommand SelectCommand { get; } = ReactiveCommand.Create(onSelect);
}

public class LayerToggleVm : ReactiveObject
{
    private bool _isChecked;

    public LayerToggleVm(WardrobeLayer layer, bool initiallyChecked, Action<LayerToggleVm> onChanged)
    {
        Order = layer.Order;
        DisplayName = layer.DisplayName;
        _isChecked = initiallyChecked;
        this.WhenAnyValue(x => x.IsChecked).Subscribe(_ => onChanged(this));
    }

    public int Order { get; }
    public string DisplayName { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
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
        Name = PermissionTypeItemViewModel.GetName(config.Kind, l);
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
        Action<ForcedOutfitPresetSummaryViewModel> onEdit, Action<int> onDelete)
    {
        Id = preset.Id;
        Name = preset.Name;
        _isEnabled = preset.IsEnabled;
        SummaryLabel = string.Format(Resources.Get("SettingsForcedOutfitPresetSummaryFormat"),
            preset.HoursPerSeverityPoint,
            Resources.Get(preset.IsFullOutfit ? "SettingsForcedOutfitFullBadge" : "SettingsForcedOutfitPartialBadge"));

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

public partial class PlugAndPlayItemEditViewModel(WardrobeEditViewModel source, PlugAndPlayItemConfig? existing)
    : EligibilityChipViewModel(source, existing?.Id)
{
    [Reactive] private int _minDurationMinutes = existing?.MinDurationMinutes ?? 30;
    [Reactive] private int _maxDurationMinutes = existing?.MaxDurationMinutes ?? 120;

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
    [Reactive] private bool _isExpanded;
    [Reactive] private bool _isSelected;
    [Reactive] private double _multiplier;
    [Reactive] private string _name = "";
    [Reactive] private double _weight;

    public WardrobeEditViewModel(
        ClothingItem item,
        Action<WardrobeEditViewModel> onDelete,
        IEnumerable<string>? tagSuggestions = null,
        IEnumerable<string>? slotSuggestions = null,
        Action<WardrobeEditViewModel?>? onExpandRequest = null)
    {
        Id = item.Id;
        _name = item.Name;
        _multiplier = item.Multiplier;
        _weight = item.Weight;

        Layers = new ObservableCollection<int>(item.Layers);
        Slots = new ObservableCollection<string>(item.Slots);
        Tags = new ObservableCollection<string>(item.Tags);

        LayerToggles = WardrobeLayer.All
            .Select(l => new LayerToggleVm(l, item.Layers.Contains(l.Order), OnLayerToggled))
            .ToList();

        Layers.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(LayerDisplayNames));

        SlotSuggestions = slotSuggestions ?? [];
        TagSuggestions = tagSuggestions ?? [];

        DeleteCommand = ReactiveCommand.Create(() => onDelete(this));
        ToggleExpandedCommand = ReactiveCommand.Create(() =>
        {
            if (!IsExpanded)
            {
                onExpandRequest?.Invoke(this);
            }
            else
            {
                IsExpanded = false;
            }
        });
    }

    public int Id { get; internal set; }

    public ObservableCollection<int> Layers { get; }
    public ObservableCollection<string> Slots { get; }
    public ObservableCollection<string> Tags { get; }

    public IReadOnlyList<LayerToggleVm> LayerToggles { get; }
    public IEnumerable<string> LayerDisplayNames =>
        Layers.Select(o => WardrobeLayer.FindByOrder(o)?.DisplayName ?? o.ToString());

    public IEnumerable<string> SlotSuggestions { get; }
    public IEnumerable<string> TagSuggestions { get; }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand DeleteCommand { get; }

    private void OnLayerToggled(LayerToggleVm toggle)
    {
        if (toggle.IsChecked && !Layers.Contains(toggle.Order))
        {
            Layers.Add(toggle.Order);
        }
        else if (!toggle.IsChecked)
        {
            Layers.Remove(toggle.Order);
        }
    }

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

    public OutfitRuleEditViewModel(OutfitRule rule, IEnumerable<string> tagSuggestions, Action<int> onDelete)
    {
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
    public static IReadOnlyList<Option<OutfitRelation>> RelationOptions { get; } =
    [
        new(OutfitRelation.Requires, Resources.Get("SettingsOutfitRelationRequires")),
        new(OutfitRelation.Incompatible, Resources.Get("SettingsOutfitRelationExcludes")),
    ];

    public OutfitRule ToModel()
    {
        return new OutfitRule
        {
            Id = Id, SourceTag = SourceTag.Trim(), RelatedTag = RelatedTag.Trim(), Relation = RelationOption.Code,
        };
    }
}
