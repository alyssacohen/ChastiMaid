using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public enum ChipState
{
    Normal,
    Selected,
    Required,
    Disabled,
}

public partial class ChipViewModel : ViewModelBase
{
    [Reactive] private ChipState _state;

    public ChipViewModel(ClothingItem item, int layer, Action<ChipViewModel> onToggle)
    {
        Item = item;
        Layer = layer;
        _state = ChipState.Normal;
        ToggleCommand = ReactiveCommand.Create(() => onToggle(this));

        this.WhenAnyValue(x => x.State).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsSelected));
            this.RaisePropertyChanged(nameof(IsRequired));
            this.RaisePropertyChanged(nameof(IsDisabled));
            this.RaisePropertyChanged(nameof(IsNormal));
        });
    }

    public ClothingItem Item { get; }
    public int Layer { get; }
    public string Name => Item.Name;
    public int ItemId => Item.Id;

    public bool IsSelected => State is ChipState.Selected;
    public bool IsRequired => State is ChipState.Required;
    public bool IsDisabled => State is ChipState.Disabled;
    public bool IsNormal => State is ChipState.Normal;

    public ICommand ToggleCommand { get; }
}

public class LayerGroupViewModel
{
    public LayerGroupViewModel(int layerOrder)
    {
        LayerOrder = layerOrder;
        var def = WardrobeLayer.FindByOrder(layerOrder);
        DisplayName = def?.DisplayName ?? layerOrder.ToString();
        Description = def?.Description ?? "";
    }

    public int LayerOrder { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ObservableCollection<ChipViewModel> Chips { get; } = [];
}

public partial class ChangeOutfitViewModel : ViewModelBase
{
    private readonly List<ClothingItem> _allItems;
    private readonly List<OutfitRule> _allRules;
    private readonly double _bonusPercent;
    private readonly Outfit _currentOutfit;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    [Reactive] private bool _canFinalize = true;
    [Reactive] private bool _hasWarning;
    [Reactive] private bool _isViolation;
    [Reactive] private int _selectedCount;

    [Reactive] private string _statusMessage = "";

    public ChangeOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit currentOutfit,
        double bonusPercent)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _currentOutfit = currentOutfit;
        _bonusPercent = bonusPercent;
        _allItems = saveRepo.GetWardrobe();
        _allRules = saveRepo.GetOutfitRules();

        BuildLayers();
        PreselectCurrentOutfit();
        ValidateRules();

        IntegrateCommand = ReactiveCommand.Create(Integrate);
        SaveOutfitCommand = ReactiveCommand.Create(SaveOutfit);
        ClearCommand = ReactiveCommand.Create(ClearSelection);
        BackCommand = ReactiveCommand.Create(() =>
            _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, _saveRepo, _currentOutfit)));
    }

    public ObservableCollection<LayerGroupViewModel> Layers { get; } = [];

    public ICommand IntegrateCommand { get; }
    public ICommand SaveOutfitCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BackCommand { get; }

    private void BuildLayers()
    {
        foreach (var layer in Outfit.LayersOrder)
        {
            var group = new LayerGroupViewModel(layer);
            foreach (var item in _allItems.Where(i => i.Layers.Contains(layer)))
            {
                group.Chips.Add(new ChipViewModel(item, layer, OnChipToggled));
            }

            if (group.Chips.Count > 0)
            {
                Layers.Add(group);
            }
        }
    }

    private void PreselectCurrentOutfit()
    {
        foreach (var item in _currentOutfit.Items)
        {
            var layer = _currentOutfit.LayerOf(item);
            var chip = AllChips().FirstOrDefault(c => c.ItemId == item.Id && c.Layer == layer);
            if (chip is not null)
            {
                chip.State = ChipState.Selected;
            }
        }
    }

    private void OnChipToggled(ChipViewModel chip)
    {
        if (chip.State is ChipState.Disabled)
        {
            return;
        }

        chip.State = chip.State is ChipState.Selected ? ChipState.Normal : ChipState.Selected;

        ValidateRules();
    }

    private void ValidateRules()
    {
        var selectedChips = AllChips().Where(c => c.State is ChipState.Selected).ToList();
        var selectedIds = selectedChips.Select(c => c.ItemId).ToHashSet();
        var violations = new List<string>();
        var infos = new List<string>();
        var canFinalize = true;

        // 1. Reset all non-selected chips to Normal
        foreach (var chip in AllChips().Where(c => c.State is not ChipState.Selected))
        {
            chip.State = ChipState.Normal;
        }

        // 2. Disable other copies of the same item (multi-layer duplicates)
        foreach (var sel in selectedChips)
        {
            foreach (var copy in AllChips()
                         .Where(c => c.ItemId == sel.ItemId && c != sel && c.State is ChipState.Normal))
            {
                copy.State = ChipState.Disabled;
            }
        }

        // 3. Disable slot conflicts on the same layer
        foreach (var sel in selectedChips)
        {
            var slots = sel.Item.Slots.ToHashSet();
            foreach (var chip in AllChips().Where(c =>
                         c != sel &&
                         c.Layer == sel.Layer &&
                         c.State is ChipState.Normal &&
                         c.Item.Slots.Any(slots.Contains)))
            {
                chip.State = ChipState.Disabled;
            }
        }

        // 4. Apply incompatibility rules (bidirectional)
        foreach (var rule in _allRules.Where(r => r.Relation == OutfitRelation.Incompatible))
        {
            var aSelected = selectedIds.Contains(rule.ItemId);
            var bSelected = selectedIds.Contains(rule.RelatedItemId);

            if (aSelected && bSelected)
            {
                var source = _allItems.FirstOrDefault(i => i.Id == rule.ItemId);
                var related = _allItems.FirstOrDefault(i => i.Id == rule.RelatedItemId);
                if (source is not null && related is not null)
                {
                    violations.Add($"{source.Name} and {related.Name} cannot be worn together.");
                }
            }
            else if (aSelected)
            {
                foreach (var chip in FindChips(rule.RelatedItemId).Where(c => c.State is ChipState.Normal))
                {
                    chip.State = ChipState.Disabled;
                }
            }
            else if (bSelected)
            {
                foreach (var chip in FindChips(rule.ItemId).Where(c => c.State is ChipState.Normal))
                {
                    chip.State = ChipState.Disabled;
                }
            }
        }

        // 5. Apply requires rules (directional: A requires B)
        var processedRequires = new HashSet<int>();
        foreach (var rule in _allRules.Where(r => r.Relation == OutfitRelation.Requires))
        {
            if (!selectedIds.Contains(rule.ItemId))
            {
                continue;
            }

            if (!processedRequires.Add(rule.ItemId))
            {
                continue;
            }

            var source = _allItems.FirstOrDefault(i => i.Id == rule.ItemId);
            if (source is null)
            {
                continue;
            }

            var requiredIds = _allRules
                .Where(r => r.ItemId == rule.ItemId && r.Relation == OutfitRelation.Requires)
                .Select(r => r.RelatedItemId)
                .ToHashSet();

            var satisfying = requiredIds
                .Select(id => _allItems.FirstOrDefault(i => i.Id == id))
                .Where(i => i is not null && selectedIds.Contains(i.Id))
                .ToList();

            if (satisfying.Count > 0)
            {
                var satisfierName = satisfying[0]!.Name;
                var otherNames = requiredIds
                    .Where(id => satisfying.All(s => s!.Id != id))
                    .Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name)
                    .Where(n => n is not null)
                    .ToList();

                var allReqNames = requiredIds
                    .Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name)
                    .Where(n => n is not null);
                var msg =
                    $"{source.Name} requires one of: {string.Join(", ", allReqNames)}. {satisfierName} is selected — requirement satisfied.";
                if (otherNames.Count > 0)
                {
                    msg +=
                        $" {string.Join(", ", otherNames)} {(otherNames.Count == 1 ? "remains" : "remain")} available.";
                }

                infos.Add(msg);
            }
            else
            {
                foreach (var reqId in requiredIds)
                {
                    foreach (var chip in FindChips(reqId).Where(c => c.State is ChipState.Normal))
                    {
                        chip.State = ChipState.Required;
                    }
                }

                var reqNames = requiredIds
                    .Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name)
                    .Where(n => n is not null);
                violations.Add($"{source.Name} requires one of: {string.Join(", ", reqNames)}.");
                canFinalize = false;
            }
        }

        SelectedCount = selectedIds.Count;
        CanFinalize = canFinalize;
        IsViolation = violations.Count > 0;
        HasWarning = violations.Count > 0 || infos.Count > 0;
        StatusMessage = string.Join(" ", violations.Concat(infos));
    }

    private void Integrate()
    {
        var seeds = GetSelectedItems();
        var seedIds = seeds.Select(s => s.Id).ToHashSet();

        // Items from the previous outfit that the user did not select as seeds are rejected
        var droppedFromPrevious = _currentOutfit.Items
            .Where(i => !seedIds.Contains(i.Id))
            .ToList();

        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var newOutfit = builder.Build();

        _saveRepo.RejectItems(droppedFromPrevious);
        _saveRepo.AcceptItems(newOutfit.Items);
        LockOutfit(newOutfit);
    }

    private void SaveOutfit()
    {
        var selectedChips = AllChips().Where(c => c.State is ChipState.Selected).ToList();
        var seeds = selectedChips.Select(c => c.Item).ToList();

        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var referenceOutfit = builder.Build();

        _saveRepo.AcceptItems(seeds);
        var autoFilled = referenceOutfit.Items.Where(i => seeds.All(s => s.Id != i.Id)).ToList();
        _saveRepo.RejectItems(autoFilled);

        var seedOutfit = PlaceSeedsOnOutfit(selectedChips);
        LockOutfit(seedOutfit);
    }

    // Builds an Outfit with only the selected chips, using each chip's Layer as the placement layer.
    private static Outfit PlaceSeedsOnOutfit(List<ChipViewModel> chips)
    {
        var outfit = new Outfit();
        foreach (var chip in chips)
        {
            var layers = chip.Item.Layers.OrderByDescending(l => l == chip.Layer);
            foreach (var layer in layers)
            {
                if (outfit.AddItemOnLayer(chip.Item, layer))
                {
                    break;
                }
            }
        }

        return outfit;
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
        {
            chip.State = ChipState.Normal;
        }

        ValidateRules();
    }

    private List<ClothingItem> GetSelectedItems()
    {
        return AllChips().Where(c => c.State is ChipState.Selected).Select(c => c.Item).ToList();
    }

    private IEnumerable<ChipViewModel> FindChips(int itemId)
    {
        return AllChips().Where(c => c.ItemId == itemId);
    }

    private IEnumerable<ChipViewModel> AllChips()
    {
        return Layers.SelectMany(l => l.Chips);
    }
}
