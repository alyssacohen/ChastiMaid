using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public enum ChipState { Normal, Selected, Required, Disabled }

public class ChipViewModel : ViewModelBase
{
    private ChipState _state;

    public ClothingItem Item { get; }
    public string Name => Item.Name;
    public int ItemId => Item.Id;

    public ChipState State
    {
        get => _state;
        set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public bool IsSelected => State is ChipState.Selected;
    public bool IsRequired => State is ChipState.Required;
    public bool IsDisabled => State is ChipState.Disabled;
    public bool IsNormal => State is ChipState.Normal;

    public ICommand ToggleCommand { get; }

    public ChipViewModel(ClothingItem item, Action<ChipViewModel> onToggle)
    {
        Item = item;
        _state = ChipState.Normal;
        ToggleCommand = ReactiveCommand.Create(() => onToggle(this));
    }
}

public class LayerGroupViewModel(string layerName)
{
    public string LayerName { get; } = layerName;
    public ObservableCollection<ChipViewModel> Chips { get; } = [];
}

public class ChangeOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly Outfit _currentOutfit;
    private readonly double _bonusPercent;
    private readonly List<ClothingItem> _allItems;
    private readonly List<OutfitRule> _allRules;

    public ObservableCollection<LayerGroupViewModel> Layers { get; } = [];

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => this.RaiseAndSetIfChanged(ref _statusMessage, value); }

    private bool _hasWarning;
    public bool HasWarning { get => _hasWarning; set => this.RaiseAndSetIfChanged(ref _hasWarning, value); }

    private bool _canFinalize = true;
    public bool CanFinalize { get => _canFinalize; set => this.RaiseAndSetIfChanged(ref _canFinalize, value); }

    private int _selectedCount;
    public int SelectedCount { get => _selectedCount; set => this.RaiseAndSetIfChanged(ref _selectedCount, value); }

    public ICommand IntegrateCommand { get; }
    public ICommand SaveOutfitCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BackCommand { get; }

    public ChangeOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit currentOutfit, double bonusPercent)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _currentOutfit = currentOutfit;
        _bonusPercent = bonusPercent;
        _allItems = saveRepo.GetWardrobe();
        _allRules = saveRepo.GetOutfitRules();

        BuildLayers();
        ValidateRules();

        IntegrateCommand = ReactiveCommand.Create(Integrate);
        SaveOutfitCommand = ReactiveCommand.Create(SaveOutfit);
        ClearCommand = ReactiveCommand.Create(ClearSelection);
        BackCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, _saveRepo, _currentOutfit)));
    }

    private void BuildLayers()
    {
        foreach (var layer in Outfit.LayersOrder)
        {
            var group = new LayerGroupViewModel(layer);
            var items = _allItems.Where(i => i.Layers.Contains(layer)).ToList();
            foreach (var item in items)
            {
                // Avoid duplicating chips if item already placed in another layer group
                if (Layers.SelectMany(l => l.Chips).Any(c => c.ItemId == item.Id)) continue;
                group.Chips.Add(new ChipViewModel(item, OnChipToggled));
            }
            if (group.Chips.Count > 0)
                Layers.Add(group);
        }
    }

    private void OnChipToggled(ChipViewModel chip)
    {
        if (chip.State is ChipState.Disabled) return;

        chip.State = chip.State is ChipState.Selected or ChipState.Required
            ? ChipState.Normal
            : ChipState.Selected;

        ValidateRules();
    }

    private void ValidateRules()
    {
        var selectedIds = GetSelectedIds();
        var messages = new List<string>();
        var canFinalize = true;

        // Reset all non-selected chips to Normal first
        foreach (var chip in AllChips().Where(c => c.State is not ChipState.Selected))
            chip.State = ChipState.Normal;

        foreach (var rule in _allRules)
        {
            if (!selectedIds.Contains(rule.ItemId)) continue;

            var source = _allItems.First(i => i.Id == rule.ItemId);
            var related = _allItems.FirstOrDefault(i => i.Id == rule.RelatedItemId);
            if (related is null) continue;

            if (rule.Relation == OutfitRelation.Incompatible)
            {
                // Disable incompatible items
                var chip = FindChip(rule.RelatedItemId);
                if (chip is not null && chip.State is not ChipState.Selected)
                    chip.State = ChipState.Disabled;

                if (selectedIds.Contains(rule.RelatedItemId))
                    messages.Add($"{source.Name} is incompatible with {related.Name}.");
            }
            else if (rule.Relation == OutfitRelation.Requires)
            {
                // Check if any required item is selected
                var requiredIds = _allRules
                    .Where(r => r.ItemId == rule.ItemId && r.Relation == OutfitRelation.Requires)
                    .Select(r => r.RelatedItemId)
                    .ToHashSet();

                if (!requiredIds.Any(selectedIds.Contains))
                {
                    // Mark required items
                    foreach (var reqId in requiredIds)
                    {
                        var chip = FindChip(reqId);
                        if (chip is not null && chip.State is ChipState.Normal)
                            chip.State = ChipState.Required;
                    }

                    var reqNames = requiredIds
                        .Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name)
                        .Where(n => n is not null);
                    messages.Add($"{source.Name} requires one of: {string.Join(", ", reqNames)}.");
                    canFinalize = false;
                }
            }
        }

        SelectedCount = selectedIds.Count;
        CanFinalize = canFinalize;
        HasWarning = messages.Count > 0;
        StatusMessage = string.Join("\n", messages);
    }

    private void Integrate()
    {
        var seeds = GetSelectedItems();
        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var newOutfit = builder.Build();

        // Accepted = items in final outfit, rejected = items NOT in final outfit from wardrobe
        _saveRepo.AcceptItems(newOutfit.Items);

        LockOutfit(newOutfit);
    }

    private void SaveOutfit()
    {
        var seeds = GetSelectedItems();
        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var newOutfit = builder.Build();

        // Accept only the seed items; reject items added by generator but not selected by user
        _saveRepo.AcceptItems(seeds);
        var rejected = newOutfit.Items.Where(i => !seeds.Any(s => s.Id == i.Id)).ToList();
        _saveRepo.RejectItems(rejected);

        LockOutfit(newOutfit);
    }

    private void LockOutfit(Outfit outfit)
    {
        var state = _saveRepo.GetGameState();
        if (state is not null)
        {
            state.CurrentOutfit = outfit.ToString();
            state.OutfitLockedDate = UtcNow();
            state.OutfitBonusPercent = _bonusPercent;
            state.OutfitMultiplier = outfit.TotalMultiplier;
            _saveRepo.SaveGameState(state);
        }

        _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, outfit, _bonusPercent));
    }

    private void ClearSelection()
    {
        foreach (var chip in AllChips())
            chip.State = ChipState.Normal;
        ValidateRules();
    }

    private HashSet<int> GetSelectedIds() =>
        AllChips().Where(c => c.State is ChipState.Selected).Select(c => c.ItemId).ToHashSet();

    private List<ClothingItem> GetSelectedItems() =>
        AllChips().Where(c => c.State is ChipState.Selected).Select(c => c.Item).ToList();

    private ChipViewModel? FindChip(int itemId) =>
        AllChips().FirstOrDefault(c => c.ItemId == itemId);

    private IEnumerable<ChipViewModel> AllChips() =>
        Layers.SelectMany(l => l.Chips);
}
