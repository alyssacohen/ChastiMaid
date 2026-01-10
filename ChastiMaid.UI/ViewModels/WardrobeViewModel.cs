using ChastiMaid.Core.Models;
using ChastiMaid.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
public class SelectionItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public partial class WardrobeViewModel : ViewModelBase
{
    private readonly DatabaseService _dbService;
    private bool _isLoading;

    [ObservableProperty] private ObservableCollection<OutfitItem> _items = new();
    [ObservableProperty] private OutfitItem? _selectedItem;

    // Rules UI
    [ObservableProperty] private ObservableCollection<RuleDisplayItem> _currentRules = new();

    [ObservableProperty] private OutfitItem? _newRuleTarget;
    [ObservableProperty] private string _newRuleType = "requires";

    public ObservableCollection<SelectionItem> LayerOptions { get; } = new();
    public ObservableCollection<SelectionItem> SlotOptions { get; } = new();

    // Dynamically filtered list for rules (cannot target self)
    public IEnumerable<OutfitItem> AvailableTargets =>
        Items.Where(i => i.Id != SelectedItem?.Id).ToList();

    public string[] RuleTypes { get; } = { "requires", "incompatible" };

    public WardrobeViewModel(DatabaseService dbService)
    {
        _dbService = dbService;

        // Initialize Options
        foreach (var l in new[] { "undies", "base", "extra", "outer", "gears" })
            LayerOptions.Add(new SelectionItem { Name = l });

        foreach (var s in new[] { "head", "chest", "arms", "hands", "crotch", "legs", "feet" })
            SlotOptions.Add(new SelectionItem { Name = s });

        // Subscribe to CheckBox changes to keep the Model in sync
        foreach (var opt in LayerOptions) opt.PropertyChanged += (s, e) => SyncSelections();
        foreach (var opt in SlotOptions) opt.PropertyChanged += (s, e) => SyncSelections();

        LoadItems();
    }

    private void SyncSelections()
    {
        if (_isLoading || SelectedItem == null) return;

        // Update the HashSets in the Model
        SelectedItem.Layers = new HashSet<string>(LayerOptions.Where(x => x.IsSelected).Select(x => x.Name));
        SelectedItem.Slots = new HashSet<string>(SlotOptions.Where(x => x.IsSelected).Select(x => x.Name));
    }

    partial void OnSelectedItemChanged(OutfitItem? value)
    {
        _isLoading = true;

        if (value != null)
        {
            // Check if loaded null collections
            value.Layers ??= new();
            value.Slots ??= new();
            value.Tags ??= new();

            // Sync Layers checkboxes
            foreach (var opt in LayerOptions)
                opt.IsSelected = value.Layers?.Contains(opt.Name) ?? false;

            // Sync Slots checkboxes
            foreach (var opt in SlotOptions)
                opt.IsSelected = value.Slots?.Contains(opt.Name) ?? false;

            LoadRulesForItem(value.Id);
        }
        else
        {
            CurrentRules.Clear();
        }

        // Notify UI that the dropdown list for rules needs to refresh
        OnPropertyChanged(nameof(AvailableTargets));
        _isLoading = false;
    }

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

    [RelayCommand]
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

    [RelayCommand]
    private void DeleteRule(RuleDisplayItem ruleItem)
    {
        if (ruleItem != null)
        {
            CurrentRules.Remove(ruleItem);
        }
    }

    [RelayCommand]
    private void SaveChanges()
    {
        using var db = _dbService.GetDatabase();
        var itemsCol = db.GetCollection<OutfitItem>("clothing_items");
        var rulesCol = db.GetCollection<OutfitRule>("outfit_rules");

        SyncSelections();

        foreach (var item in Items)
            itemsCol.Upsert(item);

        if (SelectedItem != null)
        {
            // Update rules for the current item
            rulesCol.DeleteMany(r => r.ItemId == SelectedItem.Id);
            foreach (var ruleDisplay in CurrentRules)
                rulesCol.Insert(ruleDisplay.OriginalRule);
        }
    }

    private void LoadItems()
    {
        using var db = _dbService.GetDatabase();
        var col = db.GetCollection<OutfitItem>("clothing_items");
        var loadedItems = col.FindAll().OrderBy(x => x.Name).ToList();
        Items = new ObservableCollection<OutfitItem>(loadedItems);

        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(AvailableTargets));
    }

    [RelayCommand]
    private void AddNewItem()
    {
        var newItem = new OutfitItem { Name = "New Item", Weight = 10, Multiplier = 1.0 };
        Items.Add(newItem);
        SelectedItem = newItem;
    }

    [RelayCommand]
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
