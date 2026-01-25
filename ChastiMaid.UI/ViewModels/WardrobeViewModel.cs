using ChastiMaid.Core.Models;
using ChastiMaid.Core.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using LiteDB;
using System.Collections.ObjectModel;
using System.Reactive;

namespace ChastiMaid.UI.ViewModels;

/// <summary>
/// Helper class for Rule UI display
/// </summary>
public class RuleDisplayItem
{
    public string Description { get; set; } = string.Empty;
    public OutfitRule OriginalRule { get; set; } = new();
}

/// <summary>
/// Helper class for CheckBox bindings
/// </summary>
public partial class SelectionItem : ViewModelBase
{
    [Reactive] private bool _isSelected;

    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// ViewModel for managing the wardrobe (clothing items and rules).
/// </summary>
public partial class WardrobeViewModel : ViewModelBase
{
    private readonly DatabaseService _dbService;
    private readonly MainViewModel? _mainVM;
    private readonly SessionManager? _sessionManager;
    private bool _isLoading;

    [Reactive] private ObservableCollection<OutfitItem> _items = new();
    [Reactive] private OutfitItem? _selectedItem;
    [Reactive] private string _selectedItemTagsText = string.Empty;

    // Rules UI
    [Reactive] private ObservableCollection<RuleDisplayItem> _currentRules = new();
    [Reactive] private OutfitItem? _newRuleTarget;
    [Reactive] private string _newRuleType = "requires";

    public ObservableCollection<SelectionItem> LayerOptions { get; } = new();
    public ObservableCollection<SelectionItem> SlotOptions { get; } = new();

    /// <summary>
    /// Dynamically filtered list for rules (cannot target self).
    /// </summary>
    public IEnumerable<OutfitItem> AvailableTargets =>
        Items.Where(i => i.Id != SelectedItem?.Id).ToList();

    public string[] RuleTypes { get; } = { "requires", "incompatible" };

    // Commands
    public ReactiveCommand<Unit, Unit> AddRuleCommand { get; }
    public ReactiveCommand<RuleDisplayItem, Unit> DeleteRuleCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveChangesCommand { get; }
    public ReactiveCommand<Unit, Unit> AddNewItemCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteItemCommand { get; }
    public ReactiveCommand<Unit, Unit> BackToDashboardCommand { get; }

    public WardrobeViewModel(DatabaseService dbService, MainViewModel? mainVM = null, SessionManager? sessionManager = null)
    {
        _dbService = dbService;
        _mainVM = mainVM;
        _sessionManager = sessionManager;

        // Initialize layer options
        foreach (var l in new[] { "undies", "base", "extra", "outer", "gears" })
            LayerOptions.Add(new SelectionItem { Name = l });

        // Initialize slot options
        foreach (var s in new[] { "head", "chest", "arms", "hands", "crotch", "legs", "feet" })
            SlotOptions.Add(new SelectionItem { Name = s });

        // Subscribe to checkbox changes to sync with the model
        foreach (var opt in LayerOptions)
            opt.WhenAnyValue(x => x.IsSelected).Subscribe(_ => SyncSelections());
        foreach (var opt in SlotOptions)
            opt.WhenAnyValue(x => x.IsSelected).Subscribe(_ => SyncSelections());

        // Subscribe to TagsText changes to sync with the model
        this.WhenAnyValue(x => x.SelectedItemTagsText)
            .Subscribe(_ => SyncTagsFromText());

        // Initialize commands
        AddRuleCommand = ReactiveCommand.Create(AddRule);
        DeleteRuleCommand = ReactiveCommand.Create<RuleDisplayItem>(DeleteRule);
        SaveChangesCommand = ReactiveCommand.Create(SaveChanges);
        AddNewItemCommand = ReactiveCommand.Create(AddNewItem);
        DeleteItemCommand = ReactiveCommand.Create(DeleteItem);
        BackToDashboardCommand = ReactiveCommand.Create(
            BackToDashboard,
            this.WhenAnyValue(x => x._mainVM, x => x._sessionManager,
                (vm, sm) => vm != null && sm != null)
        );

        // React to SelectedItem changes
        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(OnSelectedItemChanged);

        LoadItems();
    }

    /// <summary>
    /// Returns to the dashboard view.
    /// </summary>
    private void BackToDashboard()
    {
        if (_mainVM == null || _sessionManager == null) return;

        // Save changes before leaving
        SaveChanges();

        // Navigate back to dashboard
        _mainVM.NavigateTo(new DashboardViewModel(_mainVM, _dbService, _sessionManager));
    }

    /// <summary>
    /// Syncs checkbox selections with the selected item's properties.
    /// </summary>
    private void SyncSelections()
    {
        if (_isLoading || SelectedItem == null) return;

        // Update the HashSets in the model
        SelectedItem.Layers = new HashSet<string>(
            LayerOptions.Where(x => x.IsSelected).Select(x => x.Name)
        );
        SelectedItem.Slots = new HashSet<string>(
            SlotOptions.Where(x => x.IsSelected).Select(x => x.Name)
        );

    }
    /// <summary>
    /// Syncs the TagsText back to the model when the user changes it.
    /// </summary>
    private void SyncTagsFromText()
    {
        if (_isLoading || SelectedItem == null) return;

        SelectedItem.Tags = new HashSet<string>(
            SelectedItemTagsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
        );
    }

    /// <summary>
    /// Called when the selected item changes.
    /// </summary>
    private void OnSelectedItemChanged(OutfitItem? value)
    {
        _isLoading = true;

        if (value != null)
        {
            // Sync layer checkboxes
            foreach (var opt in LayerOptions)
                opt.IsSelected = value.Layers?.Contains(opt.Name) ?? false;

            // Sync slot checkboxes
            foreach (var opt in SlotOptions)
                opt.IsSelected = value.Slots?.Contains(opt.Name) ?? false;

            // Sync tags to text representation
            SelectedItemTagsText = value.Tags == null || !value.Tags.Any()
                ? string.Empty
                : string.Join(", ", value.Tags);

            LoadRulesForItem(value.Id);
        }
        else
        {
            CurrentRules.Clear();
            SelectedItemTagsText = String.Empty;
        }

        // Notify UI that the dropdown list for rules needs to refresh
        this.RaisePropertyChanged(nameof(AvailableTargets));
        _isLoading = false;
    }

    /// <summary>
    /// Loads rules associated with the specified item.
    /// </summary>
    private void LoadRulesForItem(ObjectId itemId)
    {
        CurrentRules.Clear();
        using var db = _dbService.GetDatabase();
        var rulesCol = db.GetCollection<OutfitRule>("outfit_rules");
        var rules = rulesCol.Find(r => r.ItemId == itemId);

        foreach (var r in rules)
        {
            var targetItem = Items.FirstOrDefault(i => i.Id == r.RelatedItemId);
            CurrentRules.Add(new RuleDisplayItem
            {
                Description = $"{r.Relation.ToUpper()} -> {targetItem?.Name ?? "Unknown Item"}",
                OriginalRule = r
            });
        }
    }

    /// <summary>
    /// Adds a new rule to the current item.
    /// </summary>
    private void AddRule()
    {
        if (SelectedItem == null || NewRuleTarget == null) return;

        var newRule = new OutfitRule
        {
            ItemId = SelectedItem.Id,
            Relation = NewRuleType,
            RelatedItemId = NewRuleTarget.Id
        };

        CurrentRules.Add(new RuleDisplayItem
        {
            Description = $"{NewRuleType.ToUpper()} -> {NewRuleTarget.Name}",
            OriginalRule = newRule
        });
    }

    /// <summary>
    /// Deletes the specified rule.
    /// </summary>
    private void DeleteRule(RuleDisplayItem ruleItem)
    {
        if (ruleItem != null)
        {
            CurrentRules.Remove(ruleItem);
        }
    }

    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    private void SaveChanges()
    {
        using var db = _dbService.GetDatabase();
        var itemsCol = db.GetCollection<OutfitItem>("clothing_items");
        var rulesCol = db.GetCollection<OutfitRule>("outfit_rules");

        SyncSelections();

        // Save all items
        foreach (var item in Items)
            itemsCol.Upsert(item);

        // Save rules for the selected item
        if (SelectedItem != null)
        {
            rulesCol.DeleteMany(r => r.ItemId == SelectedItem.Id);
            foreach (var ruleDisplay in CurrentRules)
                rulesCol.Insert(ruleDisplay.OriginalRule);
        }
    }

    /// <summary>
    /// Loads all items from the database.
    /// </summary>
    private void LoadItems()
    {
        using var db = _dbService.GetDatabase();
        var col = db.GetCollection<OutfitItem>("clothing_items");
        var loadedItems = col.FindAll().OrderBy(x => x.Name).ToList();
        Items = new ObservableCollection<OutfitItem>(loadedItems);

        this.RaisePropertyChanged(nameof(Items));
        this.RaisePropertyChanged(nameof(AvailableTargets));
    }

    /// <summary>
    /// Adds a new item to the wardrobe.
    /// </summary>
    private void AddNewItem()
    {
        var newItem = new OutfitItem
        {
            Name = "New Item",
            Weight = 10,
            Multiplier = 1.0
        };
        Items.Add(newItem);
        SelectedItem = newItem;
    }

    /// <summary>
    /// Deletes the currently selected item.
    /// </summary>
    private void DeleteItem()
    {
        if (SelectedItem == null) return;

        using var db = _dbService.GetDatabase();

        // Delete item
        var col = db.GetCollection<OutfitItem>("clothing_items");
        col.Delete(SelectedItem.Id);

        // Delete associated rules
        var rulesCol = db.GetCollection<OutfitRule>("outfit_rules");
        rulesCol.DeleteMany(r => r.ItemId == SelectedItem.Id || r.RelatedItemId == SelectedItem.Id);

        Items.Remove(SelectedItem);
        SelectedItem = null;
    }
}
