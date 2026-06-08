using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
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

    public bool IsDirty => IsGeneralDirty || IsChoresDirty || IsPunishmentsDirty || IsWardrobeDirty || IsRulesDirty;

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
        foreach (var w in _repo.GetWardrobe())
        {
            WardrobeItems.Add(WireChild(new WardrobeEditViewModel(w, id => RemoveFrom(WardrobeItems, id, _repo.DeleteClothingItem)), () => IsWardrobeDirty = true));
            AvailableItems.Add(new ClothingItemOption(w.Id, w.Name));
        }
        foreach (var r in _repo.GetOutfitRules())
            OutfitRules.Add(WireChild(new OutfitRuleEditViewModel(r, AvailableItems, id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true));

        TakeSnapshot();

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
        WardrobeItems.CollectionChanged += (_, _) => IsWardrobeDirty = true;
        OutfitRules.CollectionChanged += (_, _) => IsRulesDirty = true;

        SaveCommand = ReactiveCommand.Create(Save);
        BackCommand = ReactiveCommand.CreateFromTask(ConfirmBack);
        AddChoreCommand = ReactiveCommand.Create(() =>
            Chores.Add(WireChild(new ChoreEditViewModel(new Chore(), id => RemoveFrom(Chores, id, _repo.DeleteChore)), () => IsChoresDirty = true)));
        AddPunishmentCommand = ReactiveCommand.Create(() =>
            Punishments.Add(WireChild(new PunishmentEditViewModel(new PunishmentTemplate(), id => RemoveFrom(Punishments, id, _repo.DeletePunishmentTemplate)), () => IsPunishmentsDirty = true)));
        AddWardrobeItemCommand = ReactiveCommand.Create(() =>
            WardrobeItems.Add(WireChild(new WardrobeEditViewModel(new ClothingItem { Multiplier = 1.0, Weight = 1.0 }, id => RemoveFrom(WardrobeItems, id, _repo.DeleteClothingItem)), () => IsWardrobeDirty = true)));
        AddOutfitRuleCommand = ReactiveCommand.Create(() =>
            OutfitRules.Add(WireChild(new OutfitRuleEditViewModel(new OutfitRule(), AvailableItems, id => RemoveFrom(OutfitRules, id, _repo.DeleteOutfitRule)), () => IsRulesDirty = true)));
    }

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
    [Reactive] private string _slots = "";
    [Reactive] private double _multiplier;
    [Reactive] private double _weight;
    [Reactive] private string _tags = "";
    [Reactive] private bool _layerUndies;
    [Reactive] private bool _layerBase;
    [Reactive] private bool _layerExtra;
    [Reactive] private bool _layerOuter;
    [Reactive] private bool _layerGears;
    public ICommand DeleteCommand { get; }

    public WardrobeEditViewModel(ClothingItem item, Action<int> onDelete)
    {
        Id = item.Id;
        _name = item.Name;
        _slots = string.Join(", ", item.Slots);
        _multiplier = item.Multiplier;
        _weight = item.Weight;
        _tags = string.Join(", ", item.Tags);
        _layerUndies = item.Layers.Contains("undies");
        _layerBase = item.Layers.Contains("base");
        _layerExtra = item.Layers.Contains("extra");
        _layerOuter = item.Layers.Contains("outer");
        _layerGears = item.Layers.Contains("gears");
        DeleteCommand = ReactiveCommand.Create(() => onDelete(Id));
    }

    public ClothingItem ToModel()
    {
        var layers = new List<string>();
        if (LayerUndies) layers.Add("undies");
        if (LayerBase) layers.Add("base");
        if (LayerExtra) layers.Add("extra");
        if (LayerOuter) layers.Add("outer");
        if (LayerGears) layers.Add("gears");
        return new()
        {
            Id = Id, Name = Name, Layers = layers,
            Slots = Slots.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            Multiplier = Multiplier, Weight = Weight,
            Tags = Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
        };
    }
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
