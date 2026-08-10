using System.Collections.ObjectModel;
using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Owns the layer/chip grid shared by the outfit editor (<see cref="ChangeOutfitViewModel" />) and the
///     forced-outfit preset editor (<see cref="ForcedOutfitPresetEditorViewModel" />): building chips per layer,
///     selection bookkeeping, and the rule-agnostic structural checks (duplicate item across layers, same-layer
///     slot conflicts). Tag-rule-specific validation lives separately in <see cref="OutfitRuleValidator" />.
/// </summary>
public class OutfitChipComposer
{
    private readonly List<ClothingItem> _allItems;
    private readonly Action _onChipToggled;

    public OutfitChipComposer(List<ClothingItem> allItems, Action onChipToggled)
    {
        _allItems = allItems;
        _onChipToggled = onChipToggled;
        BuildLayers();
    }

    public ObservableCollection<LayerGroupViewModel> Layers { get; } = [];

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

    private void OnChipToggled(ChipViewModel chip)
    {
        if (chip.State is ChipState.Disabled)
        {
            return;
        }

        chip.State = chip.State is ChipState.Selected ? ChipState.Normal : ChipState.Selected;
        _onChipToggled();
    }

    public void ClearSelection()
    {
        foreach (var chip in AllChips())
        {
            chip.State = ChipState.Normal;
        }

        _onChipToggled();
    }

    /// <summary>Selects the first chip matching each item id (used by the forced-outfit preset, which has no layer of its own).</summary>
    public void PreselectByItemId(IEnumerable<int> itemIds)
    {
        foreach (var itemId in itemIds)
        {
            var chip = AllChips().FirstOrDefault(c => c.ItemId == itemId);
            if (chip is not null)
            {
                chip.State = ChipState.Selected;
            }
        }
    }

    /// <summary>Selects the chip matching both item id and layer (used by the manual editor to restore the exact previous placement).</summary>
    public void PreselectExact(IEnumerable<(int ItemId, int Layer)> pairs)
    {
        foreach (var (itemId, layer) in pairs)
        {
            var chip = AllChips().FirstOrDefault(c => c.ItemId == itemId && c.Layer == layer);
            if (chip is not null)
            {
                chip.State = ChipState.Selected;
            }
        }
    }

    public IEnumerable<ChipViewModel> FindChips(int itemId) => AllChips().Where(c => c.ItemId == itemId);

    public IEnumerable<ChipViewModel> AllChips() => Layers.SelectMany(l => l.Chips);

    public List<ClothingItem> GetSelectedItems() =>
        AllChips().Where(c => c.State is ChipState.Selected).Select(c => c.Item).ToList();

    /// <summary>
    ///     Normalizes non-selected chips to Normal, then disables duplicate copies of a selected item on other
    ///     layers and any chip sharing a slot with a selected item on the same layer. Must run before
    ///     <see cref="OutfitRuleValidator.Apply" />, which only touches chips still in <see cref="ChipState.Normal" />.
    /// </summary>
    public void ApplyStructuralReset()
    {
        var selectedChips = AllChips().Where(c => c.State is ChipState.Selected).ToList();

        foreach (var chip in AllChips().Where(c => c.State is not ChipState.Selected))
        {
            chip.State = ChipState.Normal;
        }

        foreach (var sel in selectedChips)
        {
            foreach (var copy in AllChips()
                         .Where(c => c.ItemId == sel.ItemId && c != sel && c.State is ChipState.Normal))
            {
                copy.State = ChipState.Disabled;
            }
        }

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
    }
}
