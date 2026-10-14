using ChastiMaid.Localization;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

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
    [Reactive] private bool _isLocked;

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
    public LayerGroupViewModel(int layerOrder, ILocalizer l)
    {
        LayerOrder = layerOrder;
        var def = WardrobeLayer.FindByOrder(layerOrder);
        DisplayName = def is null ? layerOrder.ToString() : l.T(def.DisplayName);
        Description = def is null ? "" : l.T(def.Description);
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
    private readonly OutfitChipComposer _composer;
    private readonly Outfit _currentOutfit;
    private readonly MainWindowViewModel _mainVm;
    private readonly OutfitRuleValidator _ruleValidator;
    private readonly SaveRepository _saveRepo;
    [Reactive] private bool _canFinalize = true;
    [Reactive] private bool _hasWarning;
    [Reactive] private bool _isViolation;
    [Reactive] private int _selectedCount;
    [Reactive] private string _selectedCountCaption = "";

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

        // The chastity device already worn today (if any) stays locked in place; every other
        // device-eligible item is excluded — device swapping isn't a user action.
        var deviceEligibleIds = saveRepo.GetChastityDeviceItems().Select(c => c.ClothingItemId).ToHashSet();
        var currentDeviceItem = _currentOutfit.Items.FirstOrDefault(i => deviceEligibleIds.Contains(i.Id));
        _allItems = _allItems.Where(i => !deviceEligibleIds.Contains(i.Id) || i == currentDeviceItem).ToList();

        _composer = new OutfitChipComposer(_allItems, ValidateRules, mainVm.Localizer);
        _ruleValidator = new OutfitRuleValidator(_allRules, mainVm.Localizer);

        _composer.PreselectExact(_currentOutfit.Items
            .Select(i => (Id: i.Id, Layer: _currentOutfit.LayerOf(i)))
            .Where(p => p.Layer.HasValue)
            .Select(p => (p.Id, p.Layer!.Value)));

        if (currentDeviceItem is not null)
        {
            var deviceLayer = _currentOutfit.LayerOf(currentDeviceItem);
            if (deviceLayer.HasValue)
            {
                _composer.LockItem(currentDeviceItem.Id, deviceLayer.Value);
            }
        }

        ValidateRules();

        IntegrateCommand = ReactiveCommand.Create(Integrate);
        SaveOutfitCommand = ReactiveCommand.Create(SaveOutfit);
        ClearCommand = ReactiveCommand.Create(_composer.ClearSelection);
        BackCommand = ReactiveCommand.Create(() =>
            _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, _saveRepo, _currentOutfit)));
    }

    public ObservableCollection<LayerGroupViewModel> Layers => _composer.Layers;

    public ICommand IntegrateCommand { get; }
    public ICommand SaveOutfitCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BackCommand { get; }

    private void ValidateRules()
    {
        _composer.ApplyStructuralReset();
        var outcome = _ruleValidator.Apply(_composer.AllChips());

        SelectedCount = _composer.AllChips().Count(c => c.State is ChipState.Selected);
        // translators: shown after a large number, e.g. "3  items selected"
        SelectedCountCaption = _mainVm.Localizer.N("item selected", "items selected", SelectedCount);
        CanFinalize = outcome.CanFinalize;
        IsViolation = outcome.IsViolation;
        HasWarning = outcome.HasWarning;
        StatusMessage = outcome.StatusMessage;
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
        _saveRepo.RecordOutfitRefusal(Math.Max(1, droppedFromPrevious.Count)); // modifying at all counts for one
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

        // The explicit refusals are the proposed garments the player took off; modifying at all counts for one.
        var droppedFromPrevious = _currentOutfit.Items.Count(i => seeds.All(s => s.Id != i.Id));
        _saveRepo.RecordOutfitRefusal(Math.Max(1, droppedFromPrevious));

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
            state.CurrentOutfitItemIds = outfit.Items.Select(i => i.Id).ToList();
            state.OutfitLockedDate = DateTime.UtcNow;
            state.OutfitBonusPercent = _bonusPercent;
            state.OutfitMultiplier = outfit.TotalMultiplier;
            state.DiaperPlayActiveThisSession = _saveRepo.GetGameSettings().DiaperPlayEnabled &&
                DiaperPlayEvaluator.OutfitHasDiaperItem(state.CurrentOutfitItemIds, _saveRepo.GetDiaperPlayItems());
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
