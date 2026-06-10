using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Controls;
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
    private readonly MainWindowViewModel _mainVm;
    private readonly ProfileRepository _repo;
    private readonly Func<ViewModelBase> _returnTo;

    // Snapshot for General tab dirty detection
    private (string, int, decimal, decimal, double, decimal, int, int, int, int, int, int) _snapshot;

    // --- General tab fields ---
    [Reactive] private string _playerName = "";
    [Reactive] private int _autoSaveInterval;
    [Reactive] private decimal _minDebt;
    [Reactive] private decimal _maxDebt;
    [Reactive] private double _dailyInterestRate;
    [Reactive] private decimal _idleWageAmount;
    [Reactive] private int _idleWageInterval;
    [Reactive] private int _popupMinInterval;
    [Reactive] private int _popupMaxInterval;
    [Reactive] private int _popupTimeout;
    [Reactive] private int _captureMinInterval;
    [Reactive] private int _captureMaxInterval;

    // --- Collections ---
    public ObservableCollection<ChoreEditViewModel> Chores { get; } = [];
    public ObservableCollection<PunishmentEditViewModel> Punishments { get; } = [];
    public ObservableCollection<WardrobeEditViewModel> WardrobeItems { get; } = [];
    public ObservableCollection<OutfitRuleEditViewModel> OutfitRules { get; } = [];
    public ObservableCollection<ClothingItemOption> AvailableItems { get; } = [];

    // --- Per-tab dirty flags ---
    [Reactive] private bool _isGeneralDirty;
    [Reactive] private bool _isChoresDirty;
    [Reactive] private bool _isPunishmentsDirty;
    [Reactive] private bool _isWardrobeDirty;
    [Reactive] private bool _isRulesDirty;

    // --- Wardrobe filter & selection ---
    [Reactive] private string _wardrobeNameFilter = string.Empty;
    [Reactive] private string? _wardrobeLayerFilter;
    [Reactive] private int _wardrobeSelectedCount;
    private ReadOnlyObservableCollection<WardrobeEditViewModel> _filteredWardrobeItems = null!;

    // --- New-item row state (used by Phase 2 UserControl) ---
    [Reactive] private string _newItemName = string.Empty;
    [Reactive] private bool _newItemLayerUndies;
    [Reactive] private bool _newItemLayerBase;
    [Reactive] private bool _newItemLayerExtra;
    [Reactive] private bool _newItemLayerOuter;
    [Reactive] private bool _newItemLayerGears;
    [Reactive] private string _newItemSlots = string.Empty;

    public bool IsDirty => IsGeneralDirty || IsChoresDirty || IsPunishmentsDirty || IsWardrobeDirty || IsRulesDirty;
    public ReadOnlyObservableCollection<WardrobeEditViewModel> FilteredWardrobeItems => _filteredWardrobeItems;

    // Layer filter active-state helpers (bound to ToggleButton.IsChecked in WardrobeToolbar)
    public bool LayerFilterAll => WardrobeLayerFilter is null;
    public bool LayerFilterUndies => WardrobeLayerFilter == "undies";
    public bool LayerFilterBase => WardrobeLayerFilter == "base";
    public bool LayerFilterExtra => WardrobeLayerFilter == "extra";
    public bool LayerFilterOuter => WardrobeLayerFilter == "outer";
    public bool LayerFilterGears => WardrobeLayerFilter == "gears";

    // --- Tab selection with interception ---
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { if (_selectedTabIndex != value) _ = TryChangeTab(value); }
    }

    // --- Dialog ---
    [Reactive] private ViewModelBase? _activeDialog;

    // --- Commands ---
    public ICommand SaveCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand AddChoreCommand { get; }
    public ICommand AddPunishmentCommand { get; }
    public ICommand AddWardrobeItemCommand { get; }
    public ICommand AddOutfitRuleCommand { get; }
    public ICommand DeleteSelectedWardrobeItemsCommand { get; }
    public ICommand SetLayerFilterCommand { get; }

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
        _captureMinInterval = s.BusyCaptureMinIntervalSeconds;
        _captureMaxInterval = s.BusyCaptureMaxIntervalSeconds;

        foreach (var c in _repo.GetChores())
            Chores.Add(WireChild(new ChoreEditViewModel(c, id => RemoveFrom(Chores, id, _repo.DeleteChore)), () => IsChoresDirty = true));
        foreach (var p in _repo.GetPunishmentTemplates())
            Punishments.Add(WireChild(new PunishmentEditViewModel(p, id => RemoveFrom(Punishments, id, _repo.DeletePunishmentTemplate)), () => IsPunishmentsDirty = true));
        var allWardrobe = _repo.GetWardrobe().ToList();
        var initTags   = allWardrobe.SelectMany(i => i.Tags).Distinct().OrderBy(s => s).ToList();
        var initLayers = allWardrobe.SelectMany(i => i.Layers).Distinct().OrderBy(s => s).ToList();
        var initSlots  = allWardrobe.SelectMany(i => i.Slots).Distinct().OrderBy(s => s).ToList();
        foreach (var w in allWardrobe)
        {
            var vm = new WardrobeEditViewModel(w, id => RemoveWardrobeItem(id), initTags, initLayers, initSlots);
            WireWardrobeItem(vm);
            WardrobeItems.Add(vm);
            AvailableItems.Add(new ClothingItemOption(w.Id, w.Name));
        }
        foreach (var r in _repo.GetOutfitRules())
            OutfitRules.Add(WireChild(new OutfitRuleEditViewModel(r, AvailableItems, id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true));

        UpdateRowParities();

        // Filtered wardrobe view — reactive to filter controls
        WardrobeItems.ToObservableChangeSet()
            .Filter(this.WhenAnyValue(
                x => x.WardrobeNameFilter,
                x => x.WardrobeLayerFilter,
                (name, layer) => (Func<WardrobeEditViewModel, bool>)(item => MatchesWardrobeFilter(item, name, layer))))
            .Bind(out _filteredWardrobeItems)
            .Subscribe();

        TakeSnapshot();

        // Notify layer-filter bool helpers when the filter string changes
        this.WhenAnyValue(x => x.WardrobeLayerFilter)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(LayerFilterAll));
                this.RaisePropertyChanged(nameof(LayerFilterUndies));
                this.RaisePropertyChanged(nameof(LayerFilterBase));
                this.RaisePropertyChanged(nameof(LayerFilterExtra));
                this.RaisePropertyChanged(nameof(LayerFilterOuter));
                this.RaisePropertyChanged(nameof(LayerFilterGears));
            });

        Observable.Merge(
            this.WhenAnyValue(
                x => x.PlayerName, x => x.AutoSaveInterval, x => x.MinDebt,
                x => x.MaxDebt, x => x.DailyInterestRate, x => x.IdleWageAmount,
                (_, _, _, _, _, _) => System.Reactive.Unit.Default),
            this.WhenAnyValue(
                x => x.IdleWageInterval, x => x.PopupMinInterval, x => x.PopupMaxInterval,
                x => x.PopupTimeout, x => x.CaptureMinInterval, x => x.CaptureMaxInterval,
                (_, _, _, _, _, _) => System.Reactive.Unit.Default))
            .Subscribe(_ => UpdateGeneralDirty());

        Chores.CollectionChanged += (_, _) => IsChoresDirty = true;
        Punishments.CollectionChanged += (_, _) => IsPunishmentsDirty = true;
        WardrobeItems.CollectionChanged += (_, _) => { IsWardrobeDirty = true; UpdateRowParities(); UpdateSelectedCount(); };
        OutfitRules.CollectionChanged += (_, _) => IsRulesDirty = true;

        SaveCommand = ReactiveCommand.Create(Save);
        BackCommand = ReactiveCommand.CreateFromTask(ConfirmBack);
        AddChoreCommand = ReactiveCommand.Create(() =>
            Chores.Add(WireChild(new ChoreEditViewModel(new Chore(), id => RemoveFrom(Chores, id, _repo.DeleteChore)), () => IsChoresDirty = true)));
        AddPunishmentCommand = ReactiveCommand.Create(() =>
            Punishments.Add(WireChild(new PunishmentEditViewModel(new PunishmentTemplate(), id => RemoveFrom(Punishments, id, _repo.DeletePunishmentTemplate)), () => IsPunishmentsDirty = true)));
        var canAddWardrobe = this.WhenAnyValue(
            x => x.NewItemName,
            x => x.NewItemLayerUndies, x => x.NewItemLayerBase, x => x.NewItemLayerExtra,
            x => x.NewItemLayerOuter, x => x.NewItemLayerGears,
            x => x.NewItemSlots,
            (name, u, b, e, o, g, slots) =>
                !string.IsNullOrWhiteSpace(name) && (u || b || e || o || g) && !string.IsNullOrWhiteSpace(slots));
        AddWardrobeItemCommand = ReactiveCommand.Create(AddWardrobeItemFromNewRow, canAddWardrobe);
        SetLayerFilterCommand = ReactiveCommand.Create<string?>(layer => WardrobeLayerFilter = layer);
        AddOutfitRuleCommand = ReactiveCommand.Create(() =>
            OutfitRules.Add(WireChild(new OutfitRuleEditViewModel(new OutfitRule(), AvailableItems, id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true)));
        DeleteSelectedWardrobeItemsCommand = ReactiveCommand.Create(
            DeleteSelectedWardrobeItems,
            this.WhenAnyValue(x => x.WardrobeSelectedCount, n => n > 0));
    }

    // --- Wardrobe helpers ---

    private IEnumerable<string> AllUsedTags   => WardrobeItems.SelectMany(w => w.TagsChipInput.Values).Distinct().OrderBy(s => s);
    private IEnumerable<string> AllUsedLayers => WardrobeItems.SelectMany(w => w.LayersChipInput.Values).Distinct().OrderBy(s => s);
    private IEnumerable<string> AllUsedSlots  => WardrobeItems.SelectMany(w => w.SlotsChipInput.Values).Distinct().OrderBy(s => s);

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
        vm.LayersChipInput.Values.CollectionChanged += (_, _) => IsWardrobeDirty = true;
        vm.SlotsChipInput.Values.CollectionChanged  += (_, _) => IsWardrobeDirty = true;
        vm.TagsChipInput.Values.CollectionChanged   += (_, _) => IsWardrobeDirty = true;
    }

    private void RemoveWardrobeItem(int id)
    {
        var item = WardrobeItems.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        WardrobeItems.Remove(item);
        if (id > 0) _repo.DeleteClothingItem(id);
    }

    private void UpdateRowParities()
    {
        for (int i = 0; i < WardrobeItems.Count; i++)
            WardrobeItems[i].IsEvenRow = i % 2 == 0;
    }

    private void UpdateSelectedCount() =>
        WardrobeSelectedCount = WardrobeItems.Count(x => x.IsSelected);

    private void AddWardrobeItemFromNewRow()
    {
        var layers = new List<string>();
        if (NewItemLayerUndies) layers.Add("undies");
        if (NewItemLayerBase) layers.Add("base");
        if (NewItemLayerExtra) layers.Add("extra");
        if (NewItemLayerOuter) layers.Add("outer");
        if (NewItemLayerGears) layers.Add("gears");

        var item = new ClothingItem
        {
            Name = NewItemName,
            Layers = layers,
            Slots = NewItemSlots.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            Multiplier = 1.0,
            Weight = 1.0
        };

        var vm = new WardrobeEditViewModel(item, id => RemoveWardrobeItem(id), AllUsedTags, AllUsedLayers, AllUsedSlots);
        WireWardrobeItem(vm);
        WardrobeItems.Add(vm);

        // Clear new-item fields
        NewItemName = string.Empty;
        NewItemLayerUndies = NewItemLayerBase = NewItemLayerExtra = NewItemLayerOuter = NewItemLayerGears = false;
        NewItemSlots = string.Empty;

        IsWardrobeDirty = true;
    }

    private void DeleteSelectedWardrobeItems()
    {
        var toDelete = WardrobeItems.Where(x => x.IsSelected).ToList();
        foreach (var item in toDelete)
        {
            WardrobeItems.Remove(item);
            if (item.Id > 0) _repo.DeleteClothingItem(item.Id);
        }
    }

    private static bool MatchesWardrobeFilter(WardrobeEditViewModel item, string nameFilter, string? layerFilter) =>
        (string.IsNullOrEmpty(nameFilter) || item.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
        && (layerFilter is null || item.ActiveLayers.Any(l => l.Equals(layerFilter, StringComparison.OrdinalIgnoreCase)));

    // --- Generic helpers ---
    private static T WireChild<T>(T vm, Action markDirty) where T : INotifyPropertyChanged
    {
        vm.PropertyChanged += (_, _) => markDirty();
        return vm;
    }

    private static void RemoveFrom<T>(ObservableCollection<T> collection, int id, Action<int> deleteFromRepo) where T : class
    {
        var item = collection.FirstOrDefault(i => ((dynamic)i).Id == id);
        if (item is null) return;
        collection.Remove(item);
        if (id > 0) deleteFromRepo(id);
    }

    // --- Tab change interception ---
    private async Task TryChangeTab(int newIndex)
    {
        if (!IsDirty) { SetTab(newIndex); return; }

        switch (await ShowSaveDialog("You have unsaved changes in this tab."))
        {
            case SaveDialogResult.Save: Save(); SetTab(newIndex); break;
            case SaveDialogResult.Discard: _mainVm.NavigateTo(new SettingsViewModel(_mainVm, _returnTo)); break;
            case SaveDialogResult.Cancel: this.RaisePropertyChanged(nameof(SelectedTabIndex)); break;
        }
    }

    private void SetTab(int index) { _selectedTabIndex = index; this.RaisePropertyChanged(nameof(SelectedTabIndex)); }

    private async Task ConfirmBack()
    {
        if (!IsDirty) { _mainVm.NavigateTo(_returnTo()); return; }

        switch (await ShowSaveDialog("You have unsaved changes. What would you like to do?"))
        {
            case SaveDialogResult.Save: Save(); _mainVm.NavigateTo(_returnTo()); break;
            case SaveDialogResult.Discard: _mainVm.NavigateTo(_returnTo()); break;
            case SaveDialogResult.Cancel: break;
        }
    }

    private async Task<SaveDialogResult> ShowSaveDialog(string message)
    {
        var dialog = new SaveDialogViewModel("Unsaved Changes", message);
        ActiveDialog = dialog;
        var result = await dialog.Result;
        ActiveDialog = null;
        return result;
    }

    // --- Snapshot & dirty (tuple comparison) ---
    private void TakeSnapshot() => _snapshot = CurrentValues();

    private (string, int, decimal, decimal, double, decimal, int, int, int, int, int, int) CurrentValues() =>
        (_playerName, _autoSaveInterval, _minDebt, _maxDebt, _dailyInterestRate, _idleWageAmount,
         _idleWageInterval, _popupMinInterval, _popupMaxInterval, _popupTimeout, _captureMinInterval, _captureMaxInterval);

    private void UpdateGeneralDirty() => IsGeneralDirty = CurrentValues() != _snapshot;

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
        s.BusyCaptureMinIntervalSeconds = CaptureMinInterval;
        s.BusyCaptureMaxIntervalSeconds = CaptureMaxInterval;
        _repo.SaveGameSettings(s);

        foreach (var c in Chores) _repo.SaveChore(c.ToModel());
        foreach (var p in Punishments) _repo.SavePunishmentTemplate(p.ToModel());
        foreach (var w in WardrobeItems) _repo.SaveClothingItem(w.ToModel());
        foreach (var r in OutfitRules) _repo.SaveOutfitRule(r.ToModel());
    }
}

public record ClothingItemOption(int Id, string Name)
{
    public override string ToString() => Name;
}

public partial class ChoreEditViewModel : ViewModelBase
{
    public int Id { get; }
    [Reactive] private string _name = "";
    [Reactive] private string _description = "";
    [Reactive] private decimal _value;
    [Reactive] private int _frequencyDays;
    public ICommand DeleteCommand { get; }

    public ChoreEditViewModel(Chore chore, Action<int> onDelete)
    {
        Id = chore.Id;
        _name = chore.Name;
        _description = chore.Description;
        _value = chore.Value;
        _frequencyDays = chore.FrequencyDays;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public Chore ToModel() => new() { Id = Id, Name = Name, Description = Description, Value = Value, FrequencyDays = FrequencyDays };
}

public partial class PunishmentEditViewModel : ViewModelBase
{
    public int Id { get; }
    [Reactive] private string _name = "";
    [Reactive] private decimal _debtIncrease;
    [Reactive] private int _requiredChoreCount;
    public ICommand DeleteCommand { get; }

    public PunishmentEditViewModel(PunishmentTemplate t, Action<int> onDelete)
    {
        Id = t.Id;
        _name = t.Name;
        _debtIncrease = t.DebtIncrease ?? 0;
        _requiredChoreCount = t.RequiredChoreCount ?? 0;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public PunishmentTemplate ToModel() => new()
    {
        Id = Id, Name = Name,
        DebtIncrease = DebtIncrease > 0 ? DebtIncrease : null,
        RequiredChoreCount = RequiredChoreCount > 0 ? RequiredChoreCount : null
    };
}

public partial class WardrobeEditViewModel : ViewModelBase
{
    public int Id { get; }
    [Reactive] private string _name = "";
    [Reactive] private double _multiplier;
    [Reactive] private double _weight;
    [Reactive] private bool _isSelected;
    [Reactive] private bool _isExpanded;
    [Reactive] private bool _isEvenRow;

    private readonly ObservableCollection<string> _layersCollection;
    private readonly ObservableCollection<string> _slotsCollection;
    private readonly ObservableCollection<string> _tagsCollection;

    public ChipInputViewModel LayersChipInput { get; }
    public ChipInputViewModel SlotsChipInput  { get; }
    public ChipInputViewModel TagsChipInput   { get; }

    // Passthrough for filter logic and read-only chip display in the table row
    public IReadOnlyList<string> ActiveLayers => _layersCollection;
    public IReadOnlyList<string> SlotList     => _slotsCollection;

    public ICommand ToggleExpandedCommand { get; }
    public ICommand DeleteCommand { get; }

    public WardrobeEditViewModel(
        ClothingItem item,
        Action<int> onDelete,
        IEnumerable<string>? tagSuggestions   = null,
        IEnumerable<string>? layerSuggestions = null,
        IEnumerable<string>? slotSuggestions  = null)
    {
        Id          = item.Id;
        _name       = item.Name;
        _multiplier = item.Multiplier;
        _weight     = item.Weight;

        _layersCollection = new(item.Layers);
        _slotsCollection  = new(item.Slots);
        _tagsCollection   = new(item.Tags);

        LayersChipInput = new(_layersCollection) { Placeholder = "aggiungi layer...", Suggestions = layerSuggestions ?? [] };
        SlotsChipInput  = new(_slotsCollection)  { Placeholder = "aggiungi slot...",  Suggestions = slotSuggestions  ?? [] };
        TagsChipInput   = new(_tagsCollection)   { Placeholder = "aggiungi tag...",   Suggestions = tagSuggestions   ?? [] };

        DeleteCommand         = ReactiveCommand.Create(() => onDelete(Id));
        ToggleExpandedCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });
    }

    public ClothingItem ToModel() => new()
    {
        Id         = Id,
        Name       = Name,
        Layers     = [.. _layersCollection],
        Slots      = [.. _slotsCollection],
        Tags       = [.. _tagsCollection],
        Multiplier = Multiplier,
        Weight     = Weight
    };
}

public partial class OutfitRuleEditViewModel : ViewModelBase
{
    public int Id { get; }
    [Reactive] private ClothingItemOption? _item;
    [Reactive] private ClothingItemOption? _relatedItem;
    [Reactive] private OutfitRelation _relation;

    public ObservableCollection<ClothingItemOption> AvailableItems { get; }
    public ICommand DeleteCommand { get; }
    public static OutfitRelation[] RelationOptions { get; } = [OutfitRelation.Requires, OutfitRelation.Incompatible];

    public OutfitRuleEditViewModel(OutfitRule rule, ObservableCollection<ClothingItemOption> items, Action<int> onDelete)
    {
        Id = rule.Id;
        AvailableItems = items;
        _item = items.FirstOrDefault(i => i.Id == rule.ItemId);
        _relatedItem = items.FirstOrDefault(i => i.Id == rule.RelatedItemId);
        _relation = rule.Relation;
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public OutfitRule ToModel() => new()
    {
        Id = Id, ItemId = Item?.Id ?? 0, RelatedItemId = RelatedItem?.Id ?? 0, Relation = Relation
    };
}
