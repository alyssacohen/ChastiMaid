using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public enum WardrobeSortKey
{
    Name,
    Layer,
    Slot,
    Weight,
    Multiplier,
}

public enum WardrobeEntryKind
{
    Slot,
    Tag,
}

/// <summary>
///     The master/detail Wardrobe screen of Settings (docs/specs/wardrobe-master-detail.md). It edits the
///     <see cref="SettingsViewModel" />'s own item and rule collections in memory; persisting them, and the eligibility
///     lists that follow the items, stays with Settings. Targets are the checked items, or the active item when none is
///     checked; every chip, number and delete acts on all targets at once.
/// </summary>
public partial class WardrobeViewModel : ViewModelBase
{
    private const int BaseLayer = 2; // WardrobeLayer "Base", the layer of a new item when no single layer filter is set

    private readonly Func<ClothingItem, WardrobeEditViewModel> _createItem;
    private readonly ILocalizer _l;
    private readonly HashSet<int> _layerFilter = [];
    private readonly HashSet<string> _slotFilter = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _tagFilter = new(StringComparer.OrdinalIgnoreCase);
    private int _batch;
    private bool _editingNewItem;
    private StringComparer _order = WardrobeCatalog.Order;
    private IReadOnlyList<WardrobeEditViewModel> _targets = [];
    private List<(int Index, WardrobeEditViewModel Item)>? _undoItems;

    [Reactive] private WardrobeEditViewModel? _activeItem;
    [Reactive] private IReadOnlyList<WardrobeActiveFilter> _activeFilters = [];
    [Reactive] private WardrobeEntryKind? _addEntryKind;
    [Reactive] private bool? _allVisibleChecked = false;
    [Reactive] private bool _canUndo;
    [Reactive] private ConfirmDialogViewModel? _confirmation;
    [Reactive] private string _deleteText = "";
    [Reactive] private string _deleteToolTip = "";
    [Reactive] private string _detailTitle = "";
    [Reactive] private WardrobeEditViewModel? _editingItem;
    [Reactive] private string _editText = "";
    [Reactive] private string? _entryError;
    [Reactive] private string _entryText = "";
    [Reactive] private int _filterCount;
    [Reactive] private bool _hasTargets;
    [Reactive] private bool _isMultiTarget;

    /// <summary>A Wardrobe dialog is open: Settings disables its navigation meanwhile (the dialogs only cover the tab).</summary>
    [Reactive] private bool _isModalOpen;
    [Reactive] private bool _isRulesDialogOpen;
    [Reactive] private IReadOnlyList<WardrobeRuleLine> _itemRules = [];
    [Reactive] private string? _layerError;
    [Reactive] private string _listHeaderText = "";
    [Reactive] private WardrobeEntryKind? _manageKind;
    [Reactive] private IReadOnlyList<WardrobeEntryRow> _manageRows = [];
    [Reactive] private string _namesPreview = "";
    [Reactive] private string? _notice;
    [Reactive] private string? _renameError;
    [Reactive] private string _searchText = "";
    [Reactive] private string _selectAllVisibleText = "";
    [Reactive] private IReadOnlyList<WardrobeChipViewModel> _slotChips = [];
    [Reactive] private IReadOnlyList<WardrobeChipViewModel> _slotFilterChips = [];
    [Reactive] private bool _sortAscending = true;
    [Reactive] private WardrobeSortKey _sortKey = WardrobeSortKey.Name;
    [Reactive] private string _sortLabel = "";
    [Reactive] private IReadOnlyList<WardrobeSortOption> _sortOptions = [];
    [Reactive] private IReadOnlyList<WardrobeChipViewModel> _tagChips = [];
    [Reactive] private IReadOnlyList<WardrobeChipViewModel> _tagFilterChips = [];
    [Reactive] private string _totalText = "";

    public WardrobeViewModel(
        ObservableCollection<WardrobeEditViewModel> items,
        ObservableCollection<OutfitRuleEditViewModel> rules,
        WardrobeCatalog catalog,
        Func<ClothingItem, WardrobeEditViewModel> createItem,
        ILocalizer l)
    {
        Items = items;
        Rules = rules;
        _createItem = createItem;
        _l = l;
        Slots = new ObservableCollection<string>(catalog.Slots);
        Tags = new ObservableCollection<string>(catalog.Tags);

        Weight = new WardrobeNumberFieldViewModel(() => _targets, i => i.Weight, (i, v) => i.Weight = v, l);
        Multiplier = new WardrobeNumberFieldViewModel(() => _targets, i => i.Multiplier, (i, v) => i.Multiplier = v, l);

        LayerChips = [.. WardrobeLayer.All.Select(layer => LayerChip(layer, () => ToggleLayer(layer.Order)))];
        LayerFilterChips = [.. WardrobeLayer.All.Select(layer => LayerChip(layer, () => ToggleLayerFilter(layer.Order)))];

        foreach (var item in Items)
        {
            Watch(item);
        }

        Items.CollectionChanged += OnItemsChanged;
        Rules.CollectionChanged += OnRulesChanged;
        foreach (var rule in Rules)
        {
            rule.PropertyChanged += OnRuleChanged;
        }

        this.WhenAnyValue(x => x.SearchText).Subscribe(_ => Refresh());
        this.WhenAnyValue(x => x.EditText).Subscribe(_ => RenameError = null);
        this.WhenAnyValue(x => x.EntryText).Subscribe(_ => EntryError = null);
        this.WhenAnyValue(x => x.AddEntryKind, x => x.ManageKind, x => x.IsRulesDialogOpen, x => x.Confirmation,
                (add, manage, rules, confirm) => add is not null || manage is not null || rules || confirm is not null)
            .Subscribe(open => IsModalOpen = open);

        var hasTargets = this.WhenAnyValue(x => x.HasTargets);
        ActivateCommand = ReactiveCommand.Create<WardrobeEditViewModel>(Activate);
        BeginRenameCommand = ReactiveCommand.Create<WardrobeEditViewModel>(BeginRename);
        CommitRenameCommand = ReactiveCommand.Create(() => { CommitRename(); });
        CancelRenameCommand = ReactiveCommand.Create(CancelRename);
        ToggleAllVisibleCommand = ReactiveCommand.Create(ToggleAllVisible);
        SelectAllVisibleCommand = ReactiveCommand.Create(SelectAllVisible);
        InvertSelectionCommand = ReactiveCommand.Create(InvertSelection);
        ClearSelectionCommand = ReactiveCommand.Create(ClearSelection);
        ResetFiltersCommand = ReactiveCommand.Create(ResetFilters);
        AddItemCommand = ReactiveCommand.Create(AddItem);
        DeleteTargetsCommand = ReactiveCommand.Create(DeleteTargets, hasTargets);
        UndoDeleteCommand = ReactiveCommand.Create(UndoDelete);
        DismissNoticeCommand = ReactiveCommand.Create(DismissNotice);
        OpenAddEntryCommand = ReactiveCommand.Create<WardrobeEntryKind>(OpenAddEntry);
        ConfirmAddEntryCommand = ReactiveCommand.Create(ConfirmAddEntry);
        CancelAddEntryCommand = ReactiveCommand.Create(() => { AddEntryKind = null; });
        OpenManageCommand = ReactiveCommand.Create<WardrobeEntryKind>(OpenManage);
        ManageAddCommand = ReactiveCommand.Create(ManageAdd);
        CloseManageCommand = ReactiveCommand.Create(() => { ManageKind = null; });
        OpenRulesCommand = ReactiveCommand.Create(() => { IsRulesDialogOpen = true; });
        CloseRulesCommand = ReactiveCommand.Create(() => { IsRulesDialogOpen = false; });

        RefreshSort();
        RebuildEntryChips();
    }

    public ObservableCollection<WardrobeEditViewModel> Items { get; }
    public ObservableCollection<OutfitRuleEditViewModel> Rules { get; }

    /// <summary>Every slot the user keeps, in <see cref="WardrobeCatalog.Order" />, used or not.</summary>
    public ObservableCollection<string> Slots { get; }

    /// <summary>Every tag the user keeps, in <see cref="WardrobeCatalog.Order" />, used or not.</summary>
    public ObservableCollection<string> Tags { get; }

    /// <summary>The items that pass search and filters, in the chosen order.</summary>
    public ObservableCollection<WardrobeEditViewModel> VisibleItems { get; } = [];

    public IReadOnlyList<WardrobeEditViewModel> Targets => _targets;

    public IReadOnlyList<WardrobeChipViewModel> LayerChips { get; }
    public IReadOnlyList<WardrobeChipViewModel> LayerFilterChips { get; }

    public WardrobeNumberFieldViewModel Weight { get; }
    public WardrobeNumberFieldViewModel Multiplier { get; }

    public bool ShowItemRules => _targets.Count == 1;

    public bool HasFilters => FilterCount > 0;

    public string AddEntryTitle => AddEntryKind == WardrobeEntryKind.Slot ? _l.T("Add slot") : _l.T("Add tag");
    public string ManageTitle => ManageKind == WardrobeEntryKind.Slot ? _l.T("Manage slots") : _l.T("Manage tags");

    public string ManagePlaceholder =>
        ManageKind == WardrobeEntryKind.Slot ? _l.T("New slot name") : _l.T("New tag name");

    public ICommand ActivateCommand { get; }
    public ICommand BeginRenameCommand { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand ToggleAllVisibleCommand { get; }
    public ICommand SelectAllVisibleCommand { get; }
    public ICommand InvertSelectionCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand DeleteTargetsCommand { get; }
    public ICommand UndoDeleteCommand { get; }
    public ICommand DismissNoticeCommand { get; }
    public ICommand OpenAddEntryCommand { get; }
    public ICommand ConfirmAddEntryCommand { get; }
    public ICommand CancelAddEntryCommand { get; }
    public ICommand OpenManageCommand { get; }
    public ICommand ManageAddCommand { get; }
    public ICommand CloseManageCommand { get; }
    public ICommand OpenRulesCommand { get; }
    public ICommand CloseRulesCommand { get; }

    /// <summary>Raised when a rename is refused (the name belongs to another item): the view selects the whole text.</summary>
    public event EventHandler? RenameRejected;

    // ── Selection ────────────────────────────────────────────────────────

    /// <summary>
    ///     A click on a row: it becomes the active item and the checked set is cleared. A rename in progress on another
    ///     row is committed first; if its name is refused, nothing changes and the rename goes on.
    /// </summary>
    public void Activate(WardrobeEditViewModel item)
    {
        if (EditingItem is not null && !ReferenceEquals(EditingItem, item) && !CommitRename())
        {
            return;
        }

        Batch(() =>
        {
            ActiveItem = item;
            foreach (var other in Items)
            {
                other.IsSelected = false;
            }
        });
        Weight.EndEdit();
        Multiplier.EndEdit();
    }

    /// <summary>The header checkbox: unchecks the visible items when all are checked, checks them all otherwise.</summary>
    public void ToggleAllVisible()
    {
        var check = !VisibleItems.All(i => i.IsSelected);
        Batch(() => SetChecked(VisibleItems, check));
        // The CheckBox has flipped itself; put it back on the computed state (it may not have changed).
        this.RaisePropertyChanged(nameof(AllVisibleChecked));
    }

    public void SelectAllVisible()
    {
        Batch(() => SetChecked(VisibleItems, true));
    }

    /// <summary>Inverts the visible items only; hidden checked items stay as they are.</summary>
    public void InvertSelection()
    {
        Batch(() =>
        {
            foreach (var item in VisibleItems)
            {
                item.IsSelected = !item.IsSelected;
            }
        });
    }

    /// <summary>Unchecks every item, hidden ones too, so nothing invisible stays a target.</summary>
    public void ClearSelection()
    {
        Batch(() => SetChecked(Items, false));
    }

    private static void SetChecked(IEnumerable<WardrobeEditViewModel> items, bool value)
    {
        foreach (var item in items.ToList())
        {
            item.IsSelected = value;
        }
    }

    // ── Search, filters, sort ────────────────────────────────────────────

    public void ToggleLayerFilter(int layer)
    {
        Toggle(_layerFilter, layer);
        OnFiltersChanged();
    }

    public void ToggleSlotFilter(string slot)
    {
        Toggle(_slotFilter, slot);
        OnFiltersChanged();
    }

    public void ToggleTagFilter(string tag)
    {
        Toggle(_tagFilter, tag);
        OnFiltersChanged();
    }

    public void ResetFilters()
    {
        _layerFilter.Clear();
        _slotFilter.Clear();
        _tagFilter.Clear();
        OnFiltersChanged();
    }

    /// <summary>The active key again flips the direction; another key is selected ascending.</summary>
    public void SelectSort(WardrobeSortKey key)
    {
        SortAscending = key != SortKey || !SortAscending;
        SortKey = key;
        RefreshSort();
        Refresh();
    }

    private static void Toggle<T>(HashSet<T> set, T value)
    {
        if (!set.Remove(value))
        {
            set.Add(value);
        }
    }

    private void OnFiltersChanged()
    {
        foreach (var (layer, chip) in WardrobeLayer.All.Zip(LayerFilterChips))
        {
            chip.IsChecked = _layerFilter.Contains(layer.Order);
        }

        foreach (var chip in SlotFilterChips)
        {
            chip.IsChecked = _slotFilter.Contains(chip.Label);
        }

        foreach (var chip in TagFilterChips)
        {
            chip.IsChecked = _tagFilter.Contains(chip.Label);
        }

        ActiveFilters =
        [
            .. WardrobeLayer.All.Where(l => _layerFilter.Contains(l.Order))
                .Select(l => ActiveFilter(_l.T("Layer: {0}", _l.T(l.DisplayName)), () => ToggleLayerFilter(l.Order))),
            .. Slots.Where(_slotFilter.Contains)
                .Select(s => ActiveFilter(_l.T("Slot: {0}", s), () => ToggleSlotFilter(s))),
            .. Tags.Where(_tagFilter.Contains)
                .Select(t => ActiveFilter(_l.T("Tag: {0}", t), () => ToggleTagFilter(t))),
        ];
        FilterCount = ActiveFilters.Count;
        this.RaisePropertyChanged(nameof(HasFilters));
        Refresh();
    }

    private WardrobeActiveFilter ActiveFilter(string text, Action remove) =>
        new(text, _l.T("Remove filter {0}", text), ReactiveCommand.Create(remove));

    private bool Matches(WardrobeEditViewModel item)
    {
        var query = SearchText.Trim();
        return (query.Length == 0
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.LayerDisplayNames.Any(l => l.Contains(query, StringComparison.OrdinalIgnoreCase))
                || item.Slots.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))
                || item.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
               && (_layerFilter.Count == 0 || item.Layers.Any(_layerFilter.Contains))
               && (_slotFilter.Count == 0 || item.Slots.Any(_slotFilter.Contains))
               && (_tagFilter.Count == 0 || item.Tags.Any(_tagFilter.Contains));
    }

    private int CompareForSort(WardrobeEditViewModel a, WardrobeEditViewModel b)
    {
        var byKey = SortKey switch
        {
            WardrobeSortKey.Layer => (a.Layers.Count > 0 ? a.Layers.Min() : -1)
                .CompareTo(b.Layers.Count > 0 ? b.Layers.Min() : -1),
            // Items without a slot first, then by their first slot in catalogue order.
            WardrobeSortKey.Slot => _order.Compare(a.Slots.Order(_order).FirstOrDefault(),
                b.Slots.Order(_order).FirstOrDefault()),
            WardrobeSortKey.Weight => a.Weight.CompareTo(b.Weight),
            WardrobeSortKey.Multiplier => a.Multiplier.CompareTo(b.Multiplier),
            _ => 0,
        };
        var directed = SortAscending ? byKey : -byKey;
        return directed != 0 ? directed : _order.Compare(a.Name, b.Name);
    }

    // ── Detail chips ─────────────────────────────────────────────────────

    /// <summary>A full layer chip removes the layer from every target, unless one would be left without any.</summary>
    private void ToggleLayer(int layer)
    {
        var targets = _targets;
        if (targets.Count == 0)
        {
            return;
        }

        if (targets.All(t => t.Layers.Contains(layer)))
        {
            if (targets.Any(t => t.Layers.Count == 1))
            {
                LayerError = _l.T("Every item must keep at least one layer.");
                return;
            }

            Batch(() =>
            {
                foreach (var target in targets)
                {
                    target.Layers.Remove(layer);
                }
            });
            return;
        }

        Batch(() =>
        {
            foreach (var target in targets.Where(t => !t.Layers.Contains(layer)))
            {
                var at = 0;
                while (at < target.Layers.Count && target.Layers[at] < layer)
                {
                    at++;
                }

                target.Layers.Insert(at, layer);
            }
        });
    }

    /// <summary>
    ///     A full slot/tag chip removes the value from every target (and asks whether to delete the entry when no item
    ///     uses it any more); a partial or empty one adds it to every target.
    /// </summary>
    private void ToggleEntry(WardrobeEntryKind kind, string value)
    {
        var targets = _targets;
        if (targets.Count == 0)
        {
            return;
        }

        if (targets.All(t => ContainsValue(Values(t, kind), value)))
        {
            Batch(() =>
            {
                foreach (var target in targets)
                {
                    RemoveValue(Values(target, kind), value);
                }
            });
            if (!Items.Any(i => ContainsValue(Values(i, kind), value)))
            {
                AskDeleteUnused(kind, value);
            }

            return;
        }

        Batch(() =>
        {
            foreach (var target in targets)
            {
                InsertSorted(Values(target, kind), value);
            }
        });
    }

    private void AskDeleteUnused(WardrobeEntryKind kind, string value)
    {
        var ruleCount = kind == WardrobeEntryKind.Tag ? RulesCiting(value).Count : 0;
        var message = kind == WardrobeEntryKind.Slot
            ? _l.T("No item uses the slot «{0}». Delete it?", value)
            : _l.T("No item uses the tag «{0}». Delete it?", value);
        if (ruleCount > 0)
        {
            message += " " + _l.N("The rule that cites it will be deleted too.",
                "The {0} rules that cite it will be deleted too.", ruleCount, ruleCount);
        }

        Confirm(new ConfirmDialogViewModel(
            kind == WardrobeEntryKind.Slot ? _l.T("Delete the slot?") : _l.T("Delete the tag?"),
            message, _l.T("Delete"), _l.T("Cancel")) { IsDestructive = true }, () => RemoveEntry(kind, value));
    }

    private WardrobeChipViewModel LayerChip(WardrobeLayer layer, Action toggle) => new(_l.T(layer.DisplayName), toggle);

    private void RebuildEntryChips()
    {
        SlotChips = [.. Slots.Select(s => new WardrobeChipViewModel(s, () => ToggleEntry(WardrobeEntryKind.Slot, s)))];
        TagChips = [.. Tags.Select(t => new WardrobeChipViewModel(t, () => ToggleEntry(WardrobeEntryKind.Tag, t)))];
        SlotFilterChips = [.. Slots.Select(s => new WardrobeChipViewModel(s, () => ToggleSlotFilter(s)))];
        TagFilterChips = [.. Tags.Select(t => new WardrobeChipViewModel(t, () => ToggleTagFilter(t)))];
        OnFiltersChanged();
    }

    // ── Slot and tag lists ───────────────────────────────────────────────

    /// <summary>
    ///     Adds a slot/tag to the list, and to every target when <paramref name="applyToTargets" />. Returns the error to
    ///     show when the name is empty or already in the list (ignoring case), null when it was added.
    /// </summary>
    public string? AddEntry(WardrobeEntryKind kind, string name, bool applyToTargets)
    {
        var value = name.Trim();
        if (value.Length == 0)
        {
            return _l.T("Enter a name.");
        }

        var list = Catalog(kind);
        if (ContainsValue(list, value))
        {
            return kind == WardrobeEntryKind.Slot
                ? _l.T("A slot with this name already exists.")
                : _l.T("A tag with this name already exists.");
        }

        InsertSorted(list, value);
        RebuildEntryChips();
        if (applyToTargets)
        {
            Batch(() =>
            {
                foreach (var target in _targets)
                {
                    InsertSorted(Values(target, kind), value);
                }
            });
        }

        return null;
    }

    /// <summary>Removes a slot/tag from the list, every item and the filters; a tag also takes the rules citing it.</summary>
    public void RemoveEntry(WardrobeEntryKind kind, string value)
    {
        Batch(() =>
        {
            RemoveValue(Catalog(kind), value);
            foreach (var item in Items)
            {
                RemoveValue(Values(item, kind), value);
            }

            (kind == WardrobeEntryKind.Slot ? _slotFilter : _tagFilter).Remove(value);
            if (kind == WardrobeEntryKind.Tag)
            {
                foreach (var rule in RulesCiting(value))
                {
                    Rules.Remove(rule);
                }
            }

            RebuildEntryChips();
        });
        ShowNotice(kind == WardrobeEntryKind.Slot ? _l.T("Slot «{0}» deleted", value) : _l.T("Tag «{0}» deleted", value));
        RefreshManageRows();
    }

    public void OpenAddEntry(WardrobeEntryKind kind)
    {
        EntryText = "";
        EntryError = null;
        AddEntryKind = kind;
        this.RaisePropertyChanged(nameof(AddEntryTitle));
    }

    public void ConfirmAddEntry()
    {
        if (AddEntryKind is not { } kind)
        {
            return;
        }

        EntryError = AddEntry(kind, EntryText, true);
        if (EntryError is null)
        {
            AddEntryKind = null;
        }
    }

    public void OpenManage(WardrobeEntryKind kind)
    {
        EntryText = "";
        EntryError = null;
        ManageKind = kind;
        this.RaisePropertyChanged(nameof(ManageTitle));
        this.RaisePropertyChanged(nameof(ManagePlaceholder));
        RefreshManageRows();
    }

    /// <summary>The add field of the Manage dialog only creates the entry; it is not applied to any item.</summary>
    public void ManageAdd()
    {
        if (ManageKind is not { } kind)
        {
            return;
        }

        EntryError = AddEntry(kind, EntryText, false);
        if (EntryError is null)
        {
            EntryText = "";
            RefreshManageRows();
        }
    }

    /// <summary>Delete from the Manage dialog: always confirmed, even for an entry nothing uses.</summary>
    public void AskDeleteEntry(WardrobeEntryKind kind, string value)
    {
        var items = _l.N("{0} item", "{0} items", ItemsUsing(kind, value), ItemsUsing(kind, value));
        var ruleCount = RulesCiting(value).Count;
        var message = kind == WardrobeEntryKind.Slot
            // translators: {1} is a count of items, e.g. "3 items"
            ? _l.T("The slot «{0}» is used by {1}. Deleting it removes it from all items. Continue?", value, items)
            // translators: {1} is a count of items ("3 items"), {2} a count of rules ("1 rule")
            : _l.T("The tag «{0}» is used by {1} and {2}. Deleting it removes it from all items and deletes the rules that cite it. Continue?",
                value, items, _l.N("{0} rule", "{0} rules", ruleCount, ruleCount));
        Confirm(new ConfirmDialogViewModel(
            kind == WardrobeEntryKind.Slot ? _l.T("Delete the slot?") : _l.T("Delete the tag?"),
            message, _l.T("Delete"), _l.T("Cancel")) { IsDestructive = true }, () => RemoveEntry(kind, value));
    }

    private void RefreshManageRows()
    {
        if (ManageKind is not { } kind)
        {
            if (ManageRows.Count > 0)
            {
                ManageRows = [];
            }

            return;
        }

        ManageRows =
        [
            .. Catalog(kind).Select(value =>
            {
                var items = _l.N("{0} item", "{0} items", ItemsUsing(kind, value), ItemsUsing(kind, value));
                var rules = RulesCiting(value).Count;
                return new WardrobeEntryRow(value,
                    kind == WardrobeEntryKind.Slot ? items : items + " · " + _l.N("{0} rule", "{0} rules", rules, rules),
                    kind == WardrobeEntryKind.Slot ? _l.T("Delete slot {0}", value) : _l.T("Delete tag {0}", value),
                    ReactiveCommand.Create(() => AskDeleteEntry(kind, value)));
            }),
        ];
    }

    private int ItemsUsing(WardrobeEntryKind kind, string value) =>
        Items.Count(i => ContainsValue(Values(i, kind), value));

    private List<OutfitRuleEditViewModel> RulesCiting(string tag) =>
    [
        .. Rules.Where(r => OutfitRuleEngine.TagsEqual(r.SourceTag, tag) || OutfitRuleEngine.TagsEqual(r.RelatedTag, tag)),
    ];

    private ObservableCollection<string> Catalog(WardrobeEntryKind kind) => kind == WardrobeEntryKind.Slot ? Slots : Tags;

    private static ObservableCollection<string> Values(WardrobeEditViewModel item, WardrobeEntryKind kind) =>
        kind == WardrobeEntryKind.Slot ? item.Slots : item.Tags;

    private static bool ContainsValue(IEnumerable<string> list, string value) =>
        list.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static void RemoveValue(IList<string> list, string value)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
            {
                list.RemoveAt(i);
            }
        }
    }

    private static void InsertSorted(IList<string> list, string value)
    {
        if (ContainsValue(list, value))
        {
            return;
        }

        var at = 0;
        while (at < list.Count && WardrobeCatalog.Order.Compare(list[at], value) < 0)
        {
            at++;
        }

        list.Insert(at, value);
    }

    // ── New item, rename ─────────────────────────────────────────────────

    /// <summary>Adds an item on the single filtered layer (Base otherwise), makes it visible and active, and renames it.</summary>
    public void AddItem()
    {
        if (EditingItem is not null && !CommitRename())
        {
            return;
        }

        var layer = _layerFilter.Count == 1 ? _layerFilter.Single() : BaseLayer;
        var item = _createItem(new ClothingItem { Name = _l.T("New item"), Layers = [layer] });
        _slotFilter.Clear();
        _tagFilter.Clear();
        _layerFilter.Clear();
        SearchText = "";
        Items.Add(item);
        OnFiltersChanged();
        Activate(item);
        BeginRename(item);
        _editingNewItem = true;
    }

    public void BeginRename(WardrobeEditViewModel item)
    {
        if (!ReferenceEquals(ActiveItem, item))
        {
            Activate(item);
        }

        if (!ReferenceEquals(ActiveItem, item))
        {
            return; // another row's rename was refused and goes on
        }

        _editingNewItem = false;
        EditingItem = item;
        item.IsEditing = true;
        EditText = item.Name;
        RenameError = null;
    }

    /// <summary>
    ///     Applies the typed name. An empty name keeps the old one; a name another item already has (ignoring case) is
    ///     refused: editing goes on, <see cref="RenameRejected" /> is raised and false returned.
    /// </summary>
    public bool CommitRename()
    {
        if (EditingItem is not { } item)
        {
            return true;
        }

        var name = EditText.Trim();
        if (!_editingNewItem && (name.Length == 0 || name == item.Name))
        {
            EndRename();
            return true;
        }

        if (name.Length == 0)
        {
            name = item.Name;
        }

        if (Items.Any(i => !ReferenceEquals(i, item) && string.Equals(i.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            RenameError = _l.T("An item with this name already exists.");
            RenameRejected?.Invoke(this, EventArgs.Empty);
            return false;
        }

        item.Name = name;
        EndRename();
        return true;
    }

    /// <summary>Escape: keeps the old name; for an item just created, the creation is undone.</summary>
    public void CancelRename()
    {
        if (EditingItem is { } item && _editingNewItem)
        {
            Items.Remove(item);
        }

        EndRename();
    }

    private void EndRename()
    {
        if (EditingItem is not null)
        {
            EditingItem.IsEditing = false;
        }

        EditingItem = null;
        _editingNewItem = false;
        RenameError = null;
    }

    // ── Delete, undo, notices ────────────────────────────────────────────

    /// <summary>Removes every target at once (no confirmation: the notice offers Undo).</summary>
    public void DeleteTargets()
    {
        var gone = _targets.Select(t => (Index: Items.IndexOf(t), Item: t)).OrderBy(g => g.Index).ToList();
        if (gone.Count == 0)
        {
            return;
        }

        if (EditingItem is not null && gone.Any(g => ReferenceEquals(g.Item, EditingItem)))
        {
            EndRename();
        }

        Batch(() =>
        {
            foreach (var (_, item) in gone)
            {
                item.IsSelected = false;
                Items.Remove(item);
            }

            if (ActiveItem is not null && !Items.Contains(ActiveItem))
            {
                ActiveItem = null;
            }
        });
        ShowNotice(_l.N("{0} item deleted", "{0} items deleted", gone.Count, gone.Count));
        _undoItems = gone;
        CanUndo = true;
    }

    /// <summary>Puts the items of the last delete back where they were.</summary>
    public void UndoDelete()
    {
        if (_undoItems is not { } undo)
        {
            return;
        }

        Batch(() =>
        {
            foreach (var (index, item) in undo)
            {
                Items.Insert(Math.Min(index, Items.Count), item);
            }
        });
        DismissNotice();
    }

    public void DismissNotice()
    {
        Notice = null;
        CanUndo = false;
        _undoItems = null;
    }

    private void ShowNotice(string text)
    {
        DismissNotice();
        Notice = text;
    }

    private void Confirm(ConfirmDialogViewModel dialog, Action onConfirm)
    {
        Confirmation = dialog;
        // Runs on the thread that answers the dialog (the UI thread), right when it is answered.
        dialog.Result.ContinueWith(t =>
        {
            Confirmation = null;
            if (t.Result)
            {
                onConfirm();
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    // ── Saving ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Called right before Settings persists: slots and tags become lower case everywhere (items, lists, rules),
    ///     duplicates merge, and values and layers are put in their canonical order. Returns the catalogue to store.
    /// </summary>
    public WardrobeCatalog NormalizeForSave()
    {
        WardrobeCatalog catalog = null!;
        Batch(() =>
        {
            foreach (var item in Items)
            {
                Replace(item.Slots, Lower(item.Slots));
                Replace(item.Tags, Lower(item.Tags));
                if (!item.Layers.SequenceEqual(item.Layers.Order()))
                {
                    var sorted = item.Layers.Order().ToList();
                    item.Layers.Clear();
                    sorted.ForEach(item.Layers.Add);
                }
            }

            foreach (var rule in Rules)
            {
                rule.SourceTag = rule.SourceTag.Trim().ToLowerInvariant();
                rule.RelatedTag = rule.RelatedTag.Trim().ToLowerInvariant();
            }

            catalog = WardrobeCatalog.Merge(new WardrobeCatalog { Slots = Lower(Slots), Tags = Lower(Tags) },
                Items.Select(i => i.ToModel()), Rules.Select(r => r.ToModel()));
            Replace(Slots, catalog.Slots);
            Replace(Tags, catalog.Tags);
            RebuildEntryChips();
        });
        return catalog;
    }

    private static List<string> Lower(IEnumerable<string> values) =>
        WardrobeCatalog.Normalize(values.Select(v => v.ToLowerInvariant()));

    private static void Replace(ObservableCollection<string> target, List<string> values)
    {
        if (target.SequenceEqual(values))
        {
            return;
        }

        target.Clear();
        values.ForEach(target.Add);
    }

    // ── Change tracking and refresh ──────────────────────────────────────

    private void Watch(WardrobeEditViewModel item)
    {
        item.PropertyChanged += OnItemPropertyChanged;
        item.Layers.CollectionChanged += OnItemValuesChanged;
        item.Slots.CollectionChanged += OnItemSlotsChanged;
        item.Tags.CollectionChanged += OnItemTagsChanged;
    }

    private void Unwatch(WardrobeEditViewModel item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        item.Layers.CollectionChanged -= OnItemValuesChanged;
        item.Slots.CollectionChanged -= OnItemSlotsChanged;
        item.Tags.CollectionChanged -= OnItemTagsChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.OldItems?.OfType<WardrobeEditViewModel>() ?? [])
        {
            Unwatch(item);
            item.IsActive = false;
        }

        foreach (var item in e.NewItems?.OfType<WardrobeEditViewModel>() ?? [])
        {
            Watch(item);
            AdoptValues(Slots, item.Slots);
            AdoptValues(Tags, item.Tags);
        }

        Refresh();
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var rule in e.OldItems?.OfType<OutfitRuleEditViewModel>() ?? [])
        {
            rule.PropertyChanged -= OnRuleChanged;
        }

        foreach (var rule in e.NewItems?.OfType<OutfitRuleEditViewModel>() ?? [])
        {
            rule.PropertyChanged += OnRuleChanged;
        }

        Refresh();
    }

    private void OnRuleChanged(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WardrobeEditViewModel.IsSelected) or nameof(WardrobeEditViewModel.Name)
            or nameof(WardrobeEditViewModel.Weight) or nameof(WardrobeEditViewModel.Multiplier))
        {
            Refresh();
        }
    }

    private void OnItemValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Refresh();
    }

    private void OnItemSlotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AdoptValues(Slots, e.NewItems?.OfType<string>() ?? []);
        Refresh();
    }

    private void OnItemTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AdoptValues(Tags, e.NewItems?.OfType<string>() ?? []);
        Refresh();
    }

    /// <summary>Keeps the lists a superset of what the items use, whoever added the value.</summary>
    private void AdoptValues(ObservableCollection<string> catalog, IEnumerable<string> values)
    {
        var added = false;
        foreach (var value in values.Select(v => v.Trim()).Where(v => v.Length > 0 && !ContainsValue(catalog, v)).ToList())
        {
            InsertSorted(catalog, value);
            added = true;
        }

        if (added)
        {
            RebuildEntryChips();
        }
    }

    /// <summary>Runs <paramref name="action" /> with refreshes suspended, then refreshes once.</summary>
    private void Batch(Action action)
    {
        _batch++;
        try
        {
            action();
        }
        finally
        {
            _batch--;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_batch > 0)
        {
            return;
        }

        _order = WardrobeCatalog.Order;
        Sync(VisibleItems, [.. Items.Where(Matches).Order(Comparer<WardrobeEditViewModel>.Create(CompareForSort))]);

        var visibleChecked = VisibleItems.Count(i => i.IsSelected);
        AllVisibleChecked = visibleChecked == 0 ? false : visibleChecked == VisibleItems.Count ? true : null;
        var checkedCount = Items.Count(i => i.IsSelected);
        var visible = _l.N("{0} item", "{0} items", VisibleItems.Count, VisibleItems.Count);
        ListHeaderText = checkedCount == 0
            ? visible
            // translators: after the item count of the Wardrobe list, e.g. "14 items · 3 selected"
            : visible + " · " + _l.N("{0} selected", "{0} selected", checkedCount, checkedCount);
        TotalText = _l.N("{0} wardrobe item", "{0} wardrobe items", Items.Count, Items.Count);
        SelectAllVisibleText = _l.T("Select all visible ({0})", VisibleItems.Count);
        RefreshDetail();
        RefreshManageRows();
    }

    private void RefreshSort()
    {
        (WardrobeSortKey Key, string Label)[] keys =
        [
            (WardrobeSortKey.Name, _l.T("Name")),
            // translators: sort key of the Wardrobe list (by the item's first layer)
            (WardrobeSortKey.Layer, _l.T("Layer")),
            (WardrobeSortKey.Slot, _l.T("Slot")),
            (WardrobeSortKey.Weight, _l.T("Weight")),
            (WardrobeSortKey.Multiplier, _l.T("Multiplier")),
        ];
        var arrow = SortAscending ? "↑" : "↓";
        SortOptions = [.. keys.Select(k => new WardrobeSortOption(k.Label, k.Key == SortKey, k.Key == SortKey ? arrow : "",
            ReactiveCommand.Create(() => SelectSort(k.Key))))];
        // translators: tooltip of the sort button; {0} is the sort key (Name, Layer…), {1} an arrow, ↑ or ↓
        SortLabel = _l.T("Sort: {0} {1}", keys.First(k => k.Key == SortKey).Label, arrow);
    }

    private void RefreshDetail()
    {
        if (ActiveItem is not null && !Items.Contains(ActiveItem))
        {
            ActiveItem = null;
        }

        var checkedItems = Items.Where(i => i.IsSelected).ToList();
        IReadOnlyList<WardrobeEditViewModel> targets = checkedItems.Count > 0 ? checkedItems
            : ActiveItem is { } active ? [active] : [];
        var targetsChanged = !targets.SequenceEqual(_targets);
        _targets = targets;
        if (targetsChanged)
        {
            LayerError = null;
        }

        foreach (var item in Items)
        {
            item.IsActive = checkedItems.Count == 0 && ReferenceEquals(item, ActiveItem);
        }

        HasTargets = targets.Count > 0;
        IsMultiTarget = targets.Count > 1;
        DetailTitle = targets.Count switch
        {
            0 => "",
            1 => targets[0].Name,
            var n => _l.N("{0} item selected", "{0} items selected", n, n),
        };
        NamesPreview = targets.Count <= 1
            ? ""
            : string.Join(", ", targets.Take(3).Select(t => t.Name))
              // translators: after the first three item names, e.g. "Apron, Cap, Dress and 4 more"
              + (targets.Count > 3 ? " " + _l.N("and {0} more", "and {0} more", targets.Count - 3, targets.Count - 3) : "");
        DeleteText = targets.Count > 1
            ? _l.N("Delete {0} item", "Delete {0} items", targets.Count, targets.Count)
            : _l.T("Delete item");
        DeleteToolTip = targets.Count > 1
            ? _l.N("Delete the {0} selected item", "Delete the {0} selected items", targets.Count, targets.Count)
            : _l.T("Delete the selected item");

        foreach (var (layer, chip) in WardrobeLayer.All.Zip(LayerChips))
        {
            chip.SetCount(targets.Count(t => t.Layers.Contains(layer.Order)), targets.Count);
        }

        foreach (var chip in SlotChips)
        {
            chip.SetCount(targets.Count(t => ContainsValue(t.Slots, chip.Label)), targets.Count);
        }

        foreach (var chip in TagChips)
        {
            chip.SetCount(targets.Count(t => ContainsValue(t.Tags, chip.Label)), targets.Count);
        }

        Weight.Refresh(targetsChanged);
        Multiplier.Refresh(targetsChanged);
        ItemRules = targets.Count == 1 ? RulesFor(targets[0]) : [];
        this.RaisePropertyChanged(nameof(Targets));
        this.RaisePropertyChanged(nameof(ShowItemRules));
    }

    /// <summary>
    ///     The rules that involve the item's tags. Requires rules are grouped by source tag (several of them mean "one
    ///     of") and listed again, as "required by", on the items carrying the required tag; an Incompatible rule is
    ///     listed from whichever of its tags the item has, since it works both ways.
    /// </summary>
    private List<WardrobeRuleLine> RulesFor(WardrobeEditViewModel item)
    {
        var rules = Rules.Select(r => r.ToModel()).ToList();
        var requires = rules.Where(r => r.Relation == OutfitRelation.Requires).ToList();
        bool Has(string tag) => item.Tags.Any(t => OutfitRuleEngine.TagsEqual(t, tag));
        List<string> Distinct(IEnumerable<string> tags) => [.. tags.Distinct(StringComparer.OrdinalIgnoreCase)];

        var lines = new List<WardrobeRuleLine>();
        foreach (var group in requires.Where(r => Has(r.SourceTag)).GroupBy(r => r.SourceTag, StringComparer.OrdinalIgnoreCase))
        {
            var related = Distinct(group.Select(r => r.RelatedTag));
            lines.Add(related.Count == 1
                // translators: an outfit rule; {0} and {1} are tags, shown as chips
                ? RuleLine(_l.T("{0} requires {1}"), [group.Key], related)
                // translators: an outfit rule; {0} is a tag, {1} a list of tags, all shown as chips
                : RuleLine(_l.T("{0} requires one of: {1}"), [group.Key], related));
        }

        foreach (var group in requires.Where(r => Has(r.RelatedTag)).GroupBy(r => r.RelatedTag, StringComparer.OrdinalIgnoreCase))
        {
            // translators: an outfit rule seen from the required tag {0}; {1} lists the tags that require it (chips)
            lines.Add(RuleLine(_l.T("{0} is required by {1}"), [group.Key], Distinct(group.Select(r => r.SourceTag))));
        }

        foreach (var rule in rules.Where(r => r.Relation == OutfitRelation.Incompatible))
        {
            var (own, other) = Has(rule.SourceTag) ? (rule.SourceTag, rule.RelatedTag)
                : Has(rule.RelatedTag) ? (rule.RelatedTag, rule.SourceTag)
                : (null, null);
            if (own is not null)
            {
                // translators: an outfit rule; {0} and {1} are tags that cannot be worn together, shown as chips
                lines.Add(RuleLine(_l.T("{0} excludes {1}"), [own], [other!]));
            }
        }

        return lines;
    }

    /// <summary>
    ///     Fills a translated rule sentence: <paramref name="tags" />[i] replaces {i}, as one chip per tag in
    ///     <see cref="WardrobeRuleLine.Parts" /> and as a comma-separated list in <see cref="WardrobeRuleLine.Text" />.
    /// </summary>
    private static WardrobeRuleLine RuleLine(string template, params IReadOnlyList<string>[] tags)
    {
        var parts = new List<WardrobeRulePart>();
        var at = 0;
        foreach (Match match in Placeholder().Matches(template))
        {
            AddWords(template[at..match.Index]);
            parts.AddRange(tags[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]
                .Select(tag => new WardrobeRulePart(tag, true)));
            at = match.Index + match.Length;
        }

        AddWords(template[at..]);
        var text = string.Format(CultureInfo.InvariantCulture, template,
            tags.Select(t => (object)string.Join(", ", t)).ToArray());
        return new WardrobeRuleLine(text, parts);

        void AddWords(string words)
        {
            if (words.Trim() is { Length: > 0 } trimmed)
            {
                parts.Add(new WardrobeRulePart(trimmed, false));
            }
        }
    }

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Placeholder();

    /// <summary>Makes <paramref name="target" /> match <paramref name="source" /> with moves, so the list keeps its rows.</summary>
    private static void Sync<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!source.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < source.Count; i++)
        {
            var at = target.IndexOf(source[i]);
            if (at < 0)
            {
                target.Insert(i, source[i]);
            }
            else if (at != i)
            {
                target.Move(at, i);
            }
        }
    }
}
