using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public enum ChipState { Normal, Selected, Required, Disabled }

public partial class ChipViewModel : ViewModelBase
{
    public ClothingItem Item { get; }
    public string Name => Item.Name;
    public int ItemId => Item.Id;

    [Reactive] private ChipState _state;

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

        // Propagate State changes to the derived bool properties so XAML bindings update
        this.WhenAnyValue(x => x.State).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsSelected));
            this.RaisePropertyChanged(nameof(IsRequired));
            this.RaisePropertyChanged(nameof(IsDisabled));
            this.RaisePropertyChanged(nameof(IsNormal));
        });
    }
}

public class LayerGroupViewModel
{
    private static readonly Dictionary<string, (string Display, string Desc)> _meta = new()
    {
        ["undies"] = ("Underwear",   "foundation · choose freely"),
        ["extra"]  = ("Extra",       "suspenders, garters · optional unless required by hosiery"),
        ["base"]   = ("Base",        "hosiery, slip · choose at most one pair"),
        ["outer"]  = ("Outerwear",   "dress, skirt, blouse"),
        ["gears"]  = ("Accessories", "shoes, jewellery · multiple allowed"),
    };

    public LayerGroupViewModel(string layerKey)
    {
        LayerKey = layerKey;
        var (display, desc) = _meta.GetValueOrDefault(layerKey, (layerKey, ""));
        DisplayName = display;
        Description = desc;
    }

    public string LayerKey { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ObservableCollection<ChipViewModel> Chips { get; } = [];
}

public partial class ChangeOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly Outfit _currentOutfit;
    private readonly double _bonusPercent;
    private readonly List<ClothingItem> _allItems;
    private readonly List<OutfitRule> _allRules;

    public ObservableCollection<LayerGroupViewModel> Layers { get; } = [];

    [Reactive] private string _statusMessage = "";
    [Reactive] private bool _hasWarning;
    [Reactive] private bool _isViolation;
    [Reactive] private bool _canFinalize = true;
    [Reactive] private int _selectedCount;

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

        // Required → Selected (satisfies the requirement); Selected → Normal; Normal → Selected
        chip.State = chip.State is ChipState.Selected ? ChipState.Normal : ChipState.Selected;

        ValidateRules();
    }

    private void ValidateRules()
    {
        var selectedIds = GetSelectedIds();
        var violations = new List<string>();
        var infos = new List<string>();
        var canFinalize = true;

        foreach (var chip in AllChips().Where(c => c.State is not ChipState.Selected))
            chip.State = ChipState.Normal;

        var processedRequires = new HashSet<int>();

        foreach (var rule in _allRules)
        {
            if (!selectedIds.Contains(rule.ItemId)) continue;

            var source = _allItems.FirstOrDefault(i => i.Id == rule.ItemId);
            var related = _allItems.FirstOrDefault(i => i.Id == rule.RelatedItemId);
            if (source is null || related is null) continue;

            if (rule.Relation == OutfitRelation.Incompatible)
            {
                var chip = FindChip(rule.RelatedItemId);
                if (chip is not null && chip.State is not ChipState.Selected)
                    chip.State = ChipState.Disabled;

                if (selectedIds.Contains(rule.RelatedItemId))
                    violations.Add($"{source.Name} and {related.Name} cannot be worn together.");
            }
            else if (rule.Relation == OutfitRelation.Requires && !processedRequires.Contains(rule.ItemId))
            {
                processedRequires.Add(rule.ItemId);

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

                    var msg = $"{source.Name} requires one of: {string.Join(", ", requiredIds.Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name).Where(n => n is not null))}. {satisfierName} is selected — requirement satisfied.";
                    if (otherNames.Count > 0)
                        msg += $" {string.Join(", ", otherNames)} {(otherNames.Count == 1 ? "remains" : "remain")} available.";
                    infos.Add(msg);
                }
                else
                {
                    foreach (var reqId in requiredIds)
                    {
                        var chip = FindChip(reqId);
                        if (chip is not null && chip.State is ChipState.Normal)
                            chip.State = ChipState.Required;
                    }

                    var reqNames = requiredIds
                        .Select(id => _allItems.FirstOrDefault(i => i.Id == id)?.Name)
                        .Where(n => n is not null);
                    violations.Add($"{source.Name} requires one of: {string.Join(", ", reqNames)}.");
                    canFinalize = false;
                }
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
        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var newOutfit = builder.Build();
        _saveRepo.AcceptItems(newOutfit.Items);
        LockOutfit(newOutfit);
    }

    private void SaveOutfit()
    {
        var seeds = GetSelectedItems();

        // Run OutfitBuilder to determine what would have been auto-filled,
        // then use those items only for weight adjustment — not for the actual outfit.
        var builder = new OutfitBuilder(_allItems, _allRules);
        builder.AddSeedItems(seeds);
        var referenceOutfit = builder.Build();

        _saveRepo.AcceptItems(seeds);
        var autoFilled = referenceOutfit.Items.Where(i => seeds.All(s => s.Id != i.Id)).ToList();
        _saveRepo.RejectItems(autoFilled);

        // Lock only the user-selected items
        var seedOutfit = PlaceSeedsOnOutfit(seeds, referenceOutfit);
        LockOutfit(seedOutfit);
    }

    // Builds an Outfit containing only the given seeds, using the reference outfit
    // for preferred layer assignment (fallback to item's first available layer).
    private static Outfit PlaceSeedsOnOutfit(List<ClothingItem> seeds, Outfit referenceOutfit)
    {
        var outfit = new Outfit();
        foreach (var item in seeds)
        {
            var preferred = referenceOutfit.LayerOf(item);
            var layers = item.Layers.OrderByDescending(l => l == preferred);
            foreach (var layer in layers)
            {
                if (outfit.AddItemOnLayer(item, layer)) break;
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
