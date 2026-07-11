using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ReadOnlyObservableCollection<WardrobeEditViewModel> _filteredWardrobeItems = null!;
    private readonly MainWindowViewModel _mainVm;
    private readonly ProfileRepository _repo;
    private readonly Func<ViewModelBase> _returnTo;

    // --- Dialog ---
    [Reactive] private ViewModelBase? _activeDialog;
    [Reactive] private int _autoSaveInterval;
    [Reactive] private int _captureMaxInterval;
    [Reactive] private int _captureMinInterval;
    [Reactive] private double _dailyInterestRate;
    [Reactive] private decimal _idleWageAmount;
    [Reactive] private int _idleWageInterval;
    [Reactive] private bool _isAddingNewItem;
    [Reactive] private bool _isChoresDirty;

    // --- Per-tab dirty flags ---
    [Reactive] private bool _isGeneralDirty;
    [Reactive] private bool _isPunishmentsDirty;
    [Reactive] private bool _isRulesDirty;
    [Reactive] private bool _isWardrobeDirty;
    [Reactive] private decimal _maxDebt;
    [Reactive] private decimal _minDebt;
    [Reactive] private decimal _newItemMultiplier = 1.0m;

    // --- New-item panel state ---
    [Reactive] private string _newItemName = string.Empty;
    [Reactive] private decimal _newItemWeight = 1.0m;

    // --- General tab fields ---
    [Reactive] private int _hydrationMinInterval;
    [Reactive] private string _playerName = "";
    [Reactive] private int _popupMaxInterval;
    [Reactive] private int _popupMinInterval;
    [Reactive] private int _popupTimeout;
    [Reactive] private int _selectedCameraIndex;
    [Reactive] private LanguageOption _selectedLanguage = null!;

    // --- Tab selection with interception ---
    private int _selectedTabIndex;

    // Snapshot for General tab dirty detection
    private (string, int, decimal, decimal, double, decimal, int, int, int, int, int, int, int, int, LanguageOption) _snapshot;
    [Reactive] private int? _wardrobeLayerFilter;

    // --- Wardrobe filter & selection ---
    [Reactive] private string _wardrobeNameFilter = string.Empty;
    [Reactive] private int _wardrobeSelectedCount;

    public SettingsViewModel(MainWindowViewModel mainVm, Func<ViewModelBase> returnTo)
    {
        _mainVm = mainVm;
        _repo = mainVm.ProfileRepository;
        _returnTo = returnTo;

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
        _hydrationMinInterval = s.HydrationMinIntervalMinutes;
        _captureMinInterval = s.BusyCaptureMinIntervalSeconds;
        _captureMaxInterval = s.BusyCaptureMaxIntervalSeconds;
        _selectedCameraIndex = s.SelectedCameraIndex;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == s.Language) ?? AvailableLanguages[0];
        AvailableCameras = WebcamPreviewService.EnumerateCameras();
        if (AvailableCameras.Count == 0)
        {
            AvailableCameras = [0];
        }

        foreach (var c in _repo.GetChores())
        {
            Chores.Add(WireChild(new ChoreEditViewModel(c, id => RemoveFrom(Chores, id, _repo.DeleteChore)),
                () => IsChoresDirty = true));
        }

        foreach (var p in _repo.GetPunishmentTemplates())
        {
            Punishments.Add(WireChild(
                new PunishmentEditViewModel(p, id => RemoveFrom(Punishments, id, _repo.DeletePunishmentTemplate)),
                () => IsPunishmentsDirty = true));
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
            AvailableItems.Add(new ClothingItemOption(w.Id, w.Name));
        }

        foreach (var r in _repo.GetOutfitRules())
        {
            OutfitRules.Add(WireChild(
                new OutfitRuleEditViewModel(r, AvailableItems,
                    id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true));
        }

        // Filtered wardrobe view — reactive to filter controls
        WardrobeItems.ToObservableChangeSet()
            .Filter(this.WhenAnyValue(
                x => x.WardrobeNameFilter,
                x => x.WardrobeLayerFilter,
                (name, layer) =>
                    (Func<WardrobeEditViewModel, bool>)(item => MatchesWardrobeFilter(item, name, layer))))
            .Bind(out _filteredWardrobeItems)
            .Subscribe();

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
                x => x.IdleWageInterval, x => x.PopupMinInterval, x => x.PopupMaxInterval,
                x => x.PopupTimeout, x => x.CaptureMinInterval, x => x.CaptureMaxInterval,
                (_, _, _, _, _, _) => Unit.Default)).Merge(this.WhenAnyValue(x => x.HydrationMinInterval)
                .Select(_ => Unit.Default)).Merge(this.WhenAnyValue(x => x.SelectedCameraIndex)
                .Select(_ => Unit.Default)).Merge(this.WhenAnyValue(x => x.SelectedLanguage)
                .Select(_ => Unit.Default))
            .Subscribe(_ => UpdateGeneralDirty());

        Chores.CollectionChanged += (_, _) => IsChoresDirty = true;
        Punishments.CollectionChanged += (_, _) => IsPunishmentsDirty = true;
        WardrobeItems.CollectionChanged += (_, _) =>
        {
            IsWardrobeDirty = true;
            UpdateSelectedCount();
        };
        OutfitRules.CollectionChanged += (_, _) => IsRulesDirty = true;

        SaveCommand = ReactiveCommand.Create(Save);
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
        AddPunishmentCommand = ReactiveCommand.Create(() =>
            Punishments.Add(WireChild(
                new PunishmentEditViewModel(new PunishmentTemplate(),
                    id => RemoveFrom(Punishments, id, _repo.DeletePunishmentTemplate)),
                () => IsPunishmentsDirty = true)));
        ShowAddItemPanelCommand = ReactiveCommand.Create(() => IsAddingNewItem = true);
        var canAddWardrobe = this.WhenAnyValue(x => x.NewItemName, name => !string.IsNullOrWhiteSpace(name));
        AddWardrobeItemCommand = ReactiveCommand.Create(AddWardrobeItemFromNewRow, canAddWardrobe);
        AddOutfitRuleCommand = ReactiveCommand.Create(() =>
            OutfitRules.Add(WireChild(
                new OutfitRuleEditViewModel(new OutfitRule(), AvailableItems,
                    id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true)));
        DeleteSelectedWardrobeItemsCommand = ReactiveCommand.Create(
            DeleteSelectedWardrobeItems,
            this.WhenAnyValue(x => x.WardrobeSelectedCount, n => n > 0));
        CollapseExpandedWardrobeItemCommand = ReactiveCommand.Create(() =>
        {
            foreach (var item in WardrobeItems)
            {
                item.IsExpanded = false;
            }
        });
    }

    // --- Collections ---
    public ObservableCollection<ChoreEditViewModel> Chores { get; } = [];
    public ObservableCollection<PunishmentEditViewModel> Punishments { get; } = [];
    public ObservableCollection<WardrobeEditViewModel> WardrobeItems { get; } = [];
    public ObservableCollection<OutfitRuleEditViewModel> OutfitRules { get; } = [];
    public ObservableCollection<ClothingItemOption> AvailableItems { get; } = [];

    public IReadOnlyList<int> AvailableCameras { get; private set; } = [0];

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

    public bool IsDirty => IsGeneralDirty || IsChoresDirty || IsPunishmentsDirty || IsWardrobeDirty || IsRulesDirty;
    public ReadOnlyObservableCollection<WardrobeEditViewModel> FilteredWardrobeItems => _filteredWardrobeItems;

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
    public ICommand BackCommand { get; }
    public ICommand SetTabCommand { get; }
    public ICommand AddChoreCommand { get; }
    public ICommand AddPunishmentCommand { get; }
    public ICommand ShowAddItemPanelCommand { get; }
    public ICommand AddWardrobeItemCommand { get; }
    public ICommand AddOutfitRuleCommand { get; }
    public ICommand DeleteSelectedWardrobeItemsCommand { get; }
    public ICommand CollapseExpandedWardrobeItemCommand { get; }

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
        vm.Layers.CollectionChanged += (_, _) => IsWardrobeDirty = true;
        vm.Slots.CollectionChanged += (_, _) => IsWardrobeDirty = true;
        vm.Tags.CollectionChanged += (_, _) => IsWardrobeDirty = true;
    }

    private void RemoveWardrobeItem(int id)
    {
        var item = WardrobeItems.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return;
        }

        WardrobeItems.Remove(item);
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

        NewItemName = string.Empty;
        NewItemMultiplier = 1.0m;
        NewItemWeight = 1.0m;
        foreach (var t in NewItemLayerToggles) t.IsChecked = false;
        NewItemSlots.Clear();
        NewItemTags.Clear();
        IsAddingNewItem = false;

        IsWardrobeDirty = true;
    }

    private void DeleteSelectedWardrobeItems()
    {
        var toDelete = WardrobeItems.Where(x => x.IsSelected).ToList();
        foreach (var item in toDelete)
        {
            WardrobeItems.Remove(item);
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
            case SaveDialogResult.Discard: _mainVm.NavigateTo(new SettingsViewModel(_mainVm, _returnTo)); break;
            case SaveDialogResult.Cancel: this.RaisePropertyChanged(nameof(SelectedTabIndex)); break;
        }
    }

    private void SetTab(int index)
    {
        _selectedTabIndex = index;
        this.RaisePropertyChanged(nameof(SelectedTabIndex));
    }

    private async Task ConfirmBack()
    {
        if (!IsDirty)
        {
            _mainVm.NavigateTo(_returnTo());
            return;
        }

        switch (await ShowSaveDialog(Resources.Get("SettingsUnsavedGeneralMessage")))
        {
            case SaveDialogResult.Save:
                Save();
                _mainVm.NavigateTo(_returnTo());
                break;
            case SaveDialogResult.Discard: _mainVm.NavigateTo(_returnTo()); break;
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

    private (string, int, decimal, decimal, double, decimal, int, int, int, int, int, int, int, int, LanguageOption)
        CurrentValues()
    {
        return (_playerName, _autoSaveInterval, _minDebt, _maxDebt, _dailyInterestRate, _idleWageAmount,
            _idleWageInterval, _popupMinInterval, _popupMaxInterval, _popupTimeout, _hydrationMinInterval,
            _captureMinInterval, _captureMaxInterval, _selectedCameraIndex, _selectedLanguage);
    }

    private void UpdateGeneralDirty()
    {
        IsGeneralDirty = CurrentValues() != _snapshot;
    }

    private void Save()
    {
        SaveAll();
        TakeSnapshot();
        IsGeneralDirty = IsChoresDirty = IsPunishmentsDirty = IsWardrobeDirty = IsRulesDirty = false;
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
        s.HydrationMinIntervalMinutes = HydrationMinInterval;
        s.BusyCaptureMinIntervalSeconds = CaptureMinInterval;
        s.BusyCaptureMaxIntervalSeconds = CaptureMaxInterval;
        s.SelectedCameraIndex = SelectedCameraIndex;
        s.Language = SelectedLanguage.Code;
        _repo.SaveGameSettings(s);

        foreach (var c in Chores)
        {
            _repo.SaveChore(c.ToModel());
        }

        foreach (var p in Punishments)
        {
            _repo.SavePunishmentTemplate(p.ToModel());
        }

        foreach (var w in WardrobeItems)
        {
            _repo.SaveClothingItem(w.ToModel());
        }

        foreach (var r in OutfitRules)
        {
            _repo.SaveOutfitRule(r.ToModel());
        }
    }
}

public record ClothingItemOption(int Id, string Name)
{
    public override string ToString()
    {
        return Name;
    }
}

public record LanguageOption(string Code, string DisplayName)
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

    public int Id { get; }
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

public partial class PunishmentEditViewModel : ViewModelBase
{
    [Reactive] private decimal _debtIncrease;
    [Reactive] private string _name = "";
    [Reactive] private int _requiredChoreCount;

    public PunishmentEditViewModel(PunishmentTemplate t, Action<int> onDelete)
    {
        Id = t.Id;
        _name = t.Name;
        _debtIncrease = t.DebtIncrease ?? 0;
        _requiredChoreCount = t.RequiredChoreCount ?? 0;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; }
    public ICommand DeleteCommand { get; }

    public PunishmentTemplate ToModel()
    {
        return new PunishmentTemplate
        {
            Id = Id, Name = Name,
            DebtIncrease = DebtIncrease > 0 ? DebtIncrease : null,
            RequiredChoreCount = RequiredChoreCount > 0 ? RequiredChoreCount : null,
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

    public int Id { get; }

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
    [Reactive] private ClothingItemOption? _item;
    [Reactive] private ClothingItemOption? _relatedItem;
    [Reactive] private OutfitRelation _relation;

    public OutfitRuleEditViewModel(OutfitRule rule, ObservableCollection<ClothingItemOption> items,
        Action<int> onDelete)
    {
        Id = rule.Id;
        AvailableItems = items;
        _item = items.FirstOrDefault(i => i.Id == rule.ItemId);
        _relatedItem = items.FirstOrDefault(i => i.Id == rule.RelatedItemId);
        _relation = rule.Relation;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public int Id { get; }

    public ObservableCollection<ClothingItemOption> AvailableItems { get; }
    public ICommand DeleteCommand { get; }

    public static OutfitRelation[] RelationOptions { get; } =
        [OutfitRelation.Requires, OutfitRelation.Incompatible];

    public OutfitRule ToModel()
    {
        return new OutfitRule
        {
            Id = Id, ItemId = Item?.Id ?? 0, RelatedItemId = RelatedItem?.Id ?? 0, Relation = Relation,
        };
    }
}
