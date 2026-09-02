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
    [Reactive] private int _forcedOutfitMaxSessions;
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
    [Reactive] private LanguageOption _selectedLanguage = null!;

    // --- Tab selection with interception ---
    private int _selectedTabIndex;

    // Snapshot for General tab dirty detection
    private (string, int, decimal, decimal, double, decimal, int, CameraOption, LanguageOption) _snapshot;
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
        _forcedOutfitMaxSessions = s.ForcedOutfitMaxSessions;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == s.Language) ?? AvailableLanguages[0];

        // Show a default immediately so Settings opens instantly; the real camera list and hardware
        // status are probed off the UI thread and applied once ready (see LoadHardwareAvailability).
        _selectedCamera = new CameraOption(s.SelectedCameraIndex, $"Camera {s.SelectedCameraIndex}");
        AvailableCameras = [_selectedCamera];
        _ = LoadHardwareAvailability(s.SelectedCameraIndex);

        foreach (var c in _repo.GetChores())
        {
            Chores.Add(WireChild(new ChoreEditViewModel(c, id => RemoveFrom(Chores, id, _repo.DeleteChore)),
                () => IsChoresDirty = true));
        }

        foreach (var p in _repo.GetForcedOutfitPresets())
        {
            ForcedOutfitPresets.Add(BuildForcedOutfitPresetSummary(p));
        }

        foreach (var p in _repo.GetPermissionTypes())
        {
            PermissionTypes.Add(WireChild(new PermissionTypeEditViewModel(p), () => IsPermissionsDirty = true));
        }

        var allWardrobe = _repo.GetWardrobe().ToList();

        var initTags = allWardrobe.SelectMany(i => i.Tags).Distinct().OrderBy(s => s).ToList();
        var initSlots = allWardrobe.SelectMany(i => i.Slots).Distinct().OrderBy(s => s).ToList();
        foreach (var w in allWardrobe)
        {
            var vm = new WardrobeEditViewModel(w, id => RemoveWardrobeItem(id), initTags, initSlots,
                ExpandWardrobeItem);
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
            OutfitRules.Add(WireChild(
                new OutfitRuleEditViewModel(r, AllUsedTags,
                    id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true));
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

        this.WhenAnyValue(
                x => x.PlayerName, x => x.AutoSaveInterval, x => x.MinDebt,
                x => x.MaxDebt, x => x.DailyInterestRate, x => x.IdleWageAmount,
                (_, _, _, _, _, _) => Unit.Default).Merge(this.WhenAnyValue(
                x => x.IdleWageInterval, x => x.SelectedCamera, x => x.SelectedLanguage,
                (_, _, _) => Unit.Default))
            .Subscribe(_ => UpdateGeneralDirty());

        this.WhenAnyValue(x => x.PlugAndPlayEnabled).Skip(1).Subscribe(_ => IsGeneralDirty = true);

        this.WhenAnyValue(x => x.DiaperPlayEnabled).Skip(1).Subscribe(_ => IsGeneralDirty = true);

        this.WhenAnyValue(x => x.CaptureMinInterval, x => x.CaptureMaxInterval, (_, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsChoresDirty = true);

        this.WhenAnyValue(
                x => x.PopupMinInterval, x => x.PopupMaxInterval, x => x.PopupTimeout,
                x => x.HydrationMinInterval, x => x.CurtsyVideoDuration, x => x.QuoteListenTimeout,
                x => x.QuoteMatchMinSimilarityPercent, (_, _, _, _, _, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPopupsDirty = true);

        this.WhenAnyValue(
                x => x.KneelMinDuration, x => x.KneelMaxDuration,
                x => x.KneelPhotoMinInterval, x => x.KneelPhotoMaxInterval,
                (_, _, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPopupsDirty = true);

        this.WhenAnyValue(x => x.HydrationEnabled, x => x.CurtsyEnabled, x => x.QuoteReadingEnabled,
                x => x.KneelEnabled, (_, _, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPopupsDirty = true);

        this.WhenAnyValue(x => x.FineSeverityValue, x => x.CornertimeEnabled, x => x.CornertimeSeverityValue,
                x => x.CornertimeMinRequiredSeconds, x => x.CornertimeMaxOverrunSeconds,
                x => x.CornertimeMaxRequiredSeconds,
                (_, _, _, _, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.CornertimeMovementCooldownMinSeconds, x => x.CornertimeMovementCooldownMaxSeconds,
                x => x.CornertimeMovementSensitivityPercent, x => x.CornertimeMovementPenaltyMinSeconds,
                x => x.CornertimeMovementPenaltyMaxSeconds, (_, _, _, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.CornertimeVoiceEnabled)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.WritingLinesEnabled, x => x.WritingLinesSeverityValue,
                x => x.WritingLinesPenaltyLinesPerMistake, (_, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.WritingLinesMinLines, x => x.WritingLinesMaxLines, (_, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.WritingLinesNoIdleEnabled, x => x.WritingLinesNoIdleTimeoutSeconds,
                x => x.WritingLinesNoIdleLinesToAdd, (_, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.WritingLinesInterruptionsEnabled, x => x.WritingLinesInterruptionMinIntervalSeconds,
                x => x.WritingLinesInterruptionMaxIntervalSeconds, (_, _, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        this.WhenAnyValue(x => x.ForcedOutfitEnabled, x => x.ForcedOutfitMaxSessions, (_, _) => Unit.Default)
            .Skip(1).Subscribe(_ => IsPunishmentsDirty = true);

        Chores.CollectionChanged += (_, _) => IsChoresDirty = true;
        WardrobeItems.CollectionChanged += (_, _) =>
        {
            IsWardrobeDirty = true;
            UpdateSelectedCount();
        };
        OutfitRules.CollectionChanged += (_, _) => IsRulesDirty = true;

        SaveCommand = ReactiveCommand.Create(SaveAndBack);
        QuickSaveCommand = ReactiveCommand.Create(Save);
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
        AddChoreCommand = ReactiveCommand.Create(() =>
            Chores.Add(WireChild(
                new ChoreEditViewModel(new Chore(), id => RemoveFrom(Chores, id, _repo.DeleteChore)),
                () => IsChoresDirty = true)));
        ShowAddItemPanelCommand = ReactiveCommand.Create(() => IsAddingNewItem = true);
        CancelAddItemCommand = ReactiveCommand.Create(ResetNewItemPanel);
        var canAddWardrobe = this.WhenAnyValue(x => x.NewItemName, name => !string.IsNullOrWhiteSpace(name));
        AddWardrobeItemCommand = ReactiveCommand.Create(AddWardrobeItemFromNewRow, canAddWardrobe);
        AddOutfitRuleCommand = ReactiveCommand.Create(() =>
            OutfitRules.Add(WireChild(
                new OutfitRuleEditViewModel(new OutfitRule(), AllUsedTags,
                    id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true)));
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
            foreach (var item in WardrobeItems)
            {
                item.IsExpanded = false;
            }

            if (IsAddingNewItem)
            {
                ResetNewItemPanel();
            }
        });
        ExportContentCommand = ReactiveCommand.CreateFromTask(ExportContent);
        ImportContentCommand = ReactiveCommand.CreateFromTask(ImportContent);
    }

    private async Task ExportContent()
    {
        var folder = await PickFolder(Resources.Get("SettingsExportFolderDialogTitle"));
        if (folder is null)
        {
            return;
        }

        new ContentExportService().ExportAll(_repo, folder);
        ContentActionStatusMessage = string.Format(Resources.Get("SettingsContentExportedStatus"),
            _repo.GetWardrobe().Count, _repo.GetOutfitRules().Count, _repo.GetChores().Count);
    }

    private async Task ImportContent()
    {
        var folder = await PickFolder(Resources.Get("SettingsImportFolderDialogTitle"));
        if (folder is null)
        {
            return;
        }

        var loader = new ContentLoaderService(_repo.GetGameSettings().Language);
        var result = loader.ImportAll(folder, _repo);

        var message = string.Format(Resources.Get("SettingsContentImportedStatus"),
            result.WardrobeImported, result.RulesImported, result.TasksImported);
        if (result.RulesDiscarded > 0)
        {
            var missingTags = string.Join(", ", result.DiscardedRules.SelectMany(r => r.MissingTags).Distinct());
            message += string.Format(Resources.Get("SettingsContentImportDiscardedStatus"),
                result.RulesDiscarded, missingTags);
        }

        // Rebuild the whole tab from the repo (same pattern as "Discard" — see TryChangeTab) so
        // wardrobe/rules/chores collections reflect the just-replaced content.
        var fresh = new SettingsViewModel(_mainVm, _returnTo, message);
        _mainVm.ReplaceSettingsCache(fresh);
        NavigateAway(fresh);
    }

    private static async Task<string?> PickFolder(string title)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
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
        var availability = HardwareAvailabilityChecker.Check(indices, persistedCameraIndex, _mainVm.Stt);

        Dispatcher.UIThread.Post(() =>
        {
            AvailableCameras = options;
            SelectedCamera = options.FirstOrDefault(c => c.Index == persistedCameraIndex) ?? options[0];
            WebcamStatusDisplay = Resources.Get(availability.HasWebcam ? "HardwareStatusAvailable" : "HardwareStatusUnavailable");
            var speechAvailable = availability.HasSpeechRecognizer && availability.HasMicrophone;
            SpeechRecognitionStatusDisplay = Resources.Get(
                speechAvailable ? "HardwareStatusAvailable" : "HardwareStatusUnavailable");
            IsMicrophoneAvailableForTest = speechAvailable;
            TestQuoteUnavailableTooltip = speechAvailable ? null : Resources.Get("HardwareCheckSpeechRecognizerMissing");
        });
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

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("system", Resources.Get("LanguageSystem")),
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
    public ICommand ExportContentCommand { get; }
    public ICommand ImportContentCommand { get; }

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
    ///     on any existing item.
    /// </summary>
    private void SyncEligibilityChips(WardrobeEditViewModel item)
    {
        SyncEligibilityChip(PlugAndPlayItems, item, 4, w => new PlugAndPlayItemEditViewModel(w, null),
            _repo.DeletePlugAndPlayItem);
        SyncEligibilityChip(DiaperPlayItems, item, 0, w => new DiaperPlayItemEditViewModel(w, null),
            _repo.DeleteDiaperPlayItem);
        SyncEligibilityChip(ChastityDeviceItems, item, 0, w => new ChastityDeviceItemEditViewModel(w, null),
            _repo.DeleteChastityDeviceItem);
    }

    private void SyncEligibilityChip<TChip>(
        ObservableCollection<TChip> chips,
        WardrobeEditViewModel item,
        int requiredLayer,
        Func<WardrobeEditViewModel, TChip> factory,
        Action<int> deleteConfig)
        where TChip : class, IWardrobeEligibilityChip
    {
        var existingChip = chips.FirstOrDefault(c => ReferenceEquals(c.Source, item));
        var shouldExist = item.Layers.Contains(requiredLayer);

        if (shouldExist && existingChip is null)
        {
            chips.Add(WireChild(factory(item), () => IsGeneralDirty = true));
        }
        else if (!shouldExist && existingChip is not null)
        {
            chips.Remove(existingChip);
            if (existingChip.ConfigId is { } configId)
            {
                deleteConfig(configId);
            }
        }
    }

    /// <summary>Removes this item's chip (and any persisted config) from every eligibility list.</summary>
    private void RemoveEligibilityChips(WardrobeEditViewModel item)
    {
        RemoveEligibilityChip(PlugAndPlayItems, item, _repo.DeletePlugAndPlayItem);
        RemoveEligibilityChip(DiaperPlayItems, item, _repo.DeleteDiaperPlayItem);
        RemoveEligibilityChip(ChastityDeviceItems, item, _repo.DeleteChastityDeviceItem);
    }

    private static void RemoveEligibilityChip<TChip>(
        ObservableCollection<TChip> chips, WardrobeEditViewModel item, Action<int> deleteConfig)
        where TChip : class, IWardrobeEligibilityChip
    {
        var chip = chips.FirstOrDefault(c => ReferenceEquals(c.Source, item));
        if (chip is null)
        {
            return;
        }

        chips.Remove(chip);
        if (chip.ConfigId is { } configId)
        {
            deleteConfig(configId);
        }
    }

    private void RemoveWardrobeItem(int id)
    {
        var item = WardrobeItems.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        WardrobeItems.Remove(item);
        RemoveEligibilityChips(item);
        if (id > 0)
        {
            _repo.DeleteClothingItem(id);
        }
    }

    private void ExpandWardrobeItem(WardrobeEditViewModel target)
    {
        foreach (var item in WardrobeItems)
        {
            item.IsExpanded = false;
        }

        target.IsExpanded = true;
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

        var vm = new WardrobeEditViewModel(item, id => RemoveWardrobeItem(id), AllUsedTags, AllUsedSlots,
            ExpandWardrobeItem);
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
        var toDelete = WardrobeItems.Where(x => x.IsSelected).ToList();
        foreach (var item in toDelete)
        {
            WardrobeItems.Remove(item);
            RemoveEligibilityChips(item);
            if (item.Id > 0)
            {
                _repo.DeleteClothingItem(item.Id);
            }
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

    // --- Forced Outfit presets ---
    private ForcedOutfitPresetSummaryViewModel BuildForcedOutfitPresetSummary(ForcedOutfitPreset preset)
    {
        return new ForcedOutfitPresetSummaryViewModel(preset, _repo, OpenForcedOutfitPresetEditor,
            id => RemoveFrom(ForcedOutfitPresets, id, _repo.DeleteForcedOutfitPreset));
    }

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
            ForcedOutfitPresets.Add(BuildForcedOutfitPresetSummary(p));
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
                Save();
                SetTab(newIndex);
                break;
            case SaveDialogResult.Discard:
                var fresh = new SettingsViewModel(_mainVm, _returnTo);
                _mainVm.ReplaceSettingsCache(fresh);
                NavigateAway(fresh);
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

    private (string, int, decimal, decimal, double, decimal, int, CameraOption, LanguageOption)
        CurrentValues()
    {
        return (_playerName, _autoSaveInterval, _minDebt, _maxDebt, _dailyInterestRate, _idleWageAmount,
            _idleWageInterval, _selectedCamera, _selectedLanguage);
    }

    private void UpdateGeneralDirty()
    {
        IsGeneralDirty = CurrentValues() != _snapshot;
    }

    private void Save()
    {
        SaveAll();
        TakeSnapshot();
        IsGeneralDirty = IsChoresDirty = IsPopupsDirty = IsPunishmentsDirty = IsWardrobeDirty = IsRulesDirty =
            IsPermissionsDirty = false;
    }

    private void SaveAndBack()
    {
        Save();
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
        s.ForcedOutfitMaxSessions = ForcedOutfitMaxSessions;
        _repo.SaveGameSettings(s);

        foreach (var c in Chores)
        {
            var model = c.ToModel();
            _repo.SaveChore(model);
            c.Id = model.Id; // newly-added rows have Id 0 until the first save assigns a real one;
                              // without this, every subsequent save would upsert as a fresh duplicate
        }

        foreach (var w in WardrobeItems)
        {
            var model = w.ToModel();
            _repo.SaveClothingItem(model);
            w.Id = model.Id;
        }

        foreach (var p in PermissionTypes)
        {
            var model = p.ToModel();
            _repo.SavePermissionType(model);
            p.Id = model.Id;
        }

        foreach (var r in OutfitRules)
        {
            var model = r.ToModel();
            _repo.SaveOutfitRule(model);
            r.Id = model.Id;
        }

        foreach (var p in PlugAndPlayItems)
        {
            if (p.IsEligible)
            {
                var model = p.ToModel();
                _repo.SavePlugAndPlayItem(model);
                p.ConfigId = model.Id;
            }
            else if (p.ConfigId is { } configId)
            {
                _repo.DeletePlugAndPlayItem(configId);
                p.ConfigId = null;
            }
        }

        foreach (var d in DiaperPlayItems)
        {
            if (d.IsEligible)
            {
                var model = d.ToModel();
                _repo.SaveDiaperPlayItem(model);
                d.ConfigId = model.Id;
            }
            else if (d.ConfigId is { } configId)
            {
                _repo.DeleteDiaperPlayItem(configId);
                d.ConfigId = null;
            }
        }

        foreach (var c in ChastityDeviceItems)
        {
            if (c.IsEligible)
            {
                var model = c.ToModel();
                _repo.SaveChastityDeviceItem(model);
                c.ConfigId = model.Id;
            }
            else if (c.ConfigId is { } configId)
            {
                _repo.DeleteChastityDeviceItem(configId);
                c.ConfigId = null;
            }
        }
    }
}

public record LanguageOption(string Code, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public record OutfitRelationOption(OutfitRelation Code, string DisplayName)
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

    public PermissionTypeEditViewModel(PermissionTypeConfig config)
    {
        Id = config.Id;
        Kind = config.Kind;
        Name = PermissionTypeItemViewModel.GetName(config.Kind);
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
    private readonly ProfileRepository _repo;
    [Reactive] private bool _isEnabled;

    public ForcedOutfitPresetSummaryViewModel(ForcedOutfitPreset preset, ProfileRepository repo,
        Action<ForcedOutfitPresetSummaryViewModel> onEdit, Action<int> onDelete)
    {
        _repo = repo;
        Id = preset.Id;
        Name = preset.Name;
        SessionsPerSeverityPoint = preset.SessionsPerSeverityPoint;
        IsFullOutfit = preset.IsFullOutfit;
        _isEnabled = preset.IsEnabled;
        SummaryLabel = string.Format(Resources.Get("SettingsForcedOutfitPresetSummaryFormat"),
            preset.SessionsPerSeverityPoint,
            Resources.Get(preset.IsFullOutfit ? "SettingsForcedOutfitFullBadge" : "SettingsForcedOutfitPartialBadge"));

        this.WhenAnyValue(x => x.IsEnabled).Skip(1).Subscribe(enabled =>
        {
            preset.IsEnabled = enabled;
            _repo.SaveForcedOutfitPreset(preset);
        });

        EditCommand = ReactiveCommand.Create(() => onEdit(this));
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; }
    public string Name { get; }
    public double SessionsPerSeverityPoint { get; }
    public bool IsFullOutfit { get; }
    public string SummaryLabel { get; }

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
}

/// <summary>
///     Implemented by the per-mechanism wardrobe eligibility chip VMs (Plug &amp; Play, Diaper Play,
///     Chastity Device). Holding a live reference to the source <see cref="WardrobeEditViewModel" />
///     instead of a copied id keeps the chip correct across renames and across the Id reassignment
///     that happens when a brand-new (unsaved, Id == 0) wardrobe item is first persisted.
/// </summary>
public interface IWardrobeEligibilityChip : INotifyPropertyChanged
{
    WardrobeEditViewModel Source { get; }
    int? ConfigId { get; }
}

public partial class PlugAndPlayItemEditViewModel : ViewModelBase, IWardrobeEligibilityChip
{
    [Reactive] private bool _isEligible;
    [Reactive] private int _minDurationMinutes;
    [Reactive] private int _maxDurationMinutes;

    public PlugAndPlayItemEditViewModel(WardrobeEditViewModel source, PlugAndPlayItemConfig? existing)
    {
        Source = source;
        ConfigId = existing?.Id;
        _isEligible = existing is not null;
        _minDurationMinutes = existing?.MinDurationMinutes ?? 30;
        _maxDurationMinutes = existing?.MaxDurationMinutes ?? 120;
    }

    public WardrobeEditViewModel Source { get; }
    public int ClothingItemId => Source.Id;
    public string Name => Source.Name;
    public int? ConfigId { get; internal set; }

    public PlugAndPlayItemConfig ToModel()
    {
        return new PlugAndPlayItemConfig
        {
            Id = ConfigId ?? 0,
            ClothingItemId = ClothingItemId,
            MinDurationMinutes = MinDurationMinutes,
            MaxDurationMinutes = MaxDurationMinutes,
        };
    }
}

public partial class DiaperPlayItemEditViewModel : ViewModelBase, IWardrobeEligibilityChip
{
    [Reactive] private bool _isEligible;

    public DiaperPlayItemEditViewModel(WardrobeEditViewModel source, DiaperPlayItemConfig? existing)
    {
        Source = source;
        ConfigId = existing?.Id;
        _isEligible = existing is not null;
    }

    public WardrobeEditViewModel Source { get; }
    public int ClothingItemId => Source.Id;
    public string Name => Source.Name;
    public int? ConfigId { get; internal set; }

    public DiaperPlayItemConfig ToModel()
    {
        return new DiaperPlayItemConfig
        {
            Id = ConfigId ?? 0,
            ClothingItemId = ClothingItemId,
        };
    }
}

public partial class ChastityDeviceItemEditViewModel : ViewModelBase, IWardrobeEligibilityChip
{
    [Reactive] private bool _isEligible;

    public ChastityDeviceItemEditViewModel(WardrobeEditViewModel source, ChastityDeviceItemConfig? existing)
    {
        Source = source;
        ConfigId = existing?.Id;
        _isEligible = existing is not null;
    }

    public WardrobeEditViewModel Source { get; }
    public int ClothingItemId => Source.Id;
    public string Name => Source.Name;
    public int? ConfigId { get; internal set; }

    public ChastityDeviceItemConfig ToModel()
    {
        return new ChastityDeviceItemConfig
        {
            Id = ConfigId ?? 0,
            ClothingItemId = ClothingItemId,
        };
    }
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
        Action<int> onDelete,
        IEnumerable<string>? tagSuggestions = null,
        IEnumerable<string>? slotSuggestions = null,
        Action<WardrobeEditViewModel>? onExpandRequest = null)
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

        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
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
    [Reactive] private OutfitRelationOption _relationOption = null!;

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
    public static IReadOnlyList<OutfitRelationOption> RelationOptions { get; } =
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
