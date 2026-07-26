using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Full-page editor for a single Forced Outfit preset, opened from Settings › Punishments. Mirrors
///     ChangeOutfitViewModel's layer/chip composition UI (reusing ChipViewModel/ChipState/LayerGroupViewModel
///     as-is) but persists a raw ClothingItemIds selection to the profile instead of building/locking a live
///     Outfit for the current session.
/// </summary>
public partial class ForcedOutfitPresetEditorViewModel : ViewModelBase
{
    private readonly List<ClothingItem> _allItems;
    private readonly List<OutfitRule> _allRules;
    private readonly MainWindowViewModel _mainVm;
    private readonly int _presetId;
    private readonly ProfileRepository _repo;
    private readonly Func<ViewModelBase> _returnTo;

    [Reactive] private bool _canFinalize = true;
    [Reactive] private bool _hasWarning;
    [Reactive] private bool _isEnabled;
    [Reactive] private bool _isFullOutfit;
    [Reactive] private bool _isViolation;
    [Reactive] private string _name;
    [Reactive] private int _selectedCount;
    [Reactive] private double _sessionsPerSeverityPoint;
    [Reactive] private string _statusMessage = "";

    public ForcedOutfitPresetEditorViewModel(MainWindowViewModel mainVm, ProfileRepository repo,
        ForcedOutfitPreset preset, Func<ViewModelBase> returnTo)
    {
        _mainVm = mainVm;
        _repo = repo;
        _returnTo = returnTo;
        _presetId = preset.Id;
        _name = preset.Name;
        _isEnabled = preset.IsEnabled;
        _isFullOutfit = preset.IsFullOutfit;
        _sessionsPerSeverityPoint = preset.SessionsPerSeverityPoint;

        _allItems = repo.GetWardrobe();
        _allRules = repo.GetOutfitRules();

        BuildLayers();
        PreselectFromPreset(preset);
        ValidateRules();

        var canSave = this.WhenAnyValue(
            x => x.CanFinalize, x => x.SelectedCount, x => x.Name,
            (canFinalize, count, name) => canFinalize && count > 0 && !string.IsNullOrWhiteSpace(name));

        SaveCommand = ReactiveCommand.Create(Save, canSave);
        ClearCommand = ReactiveCommand.Create(ClearSelection);
        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(_returnTo()));
    }

    public ObservableCollection<LayerGroupViewModel> Layers { get; } = [];

    public ICommand SaveCommand { get; }
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

    private void PreselectFromPreset(ForcedOutfitPreset preset)
    {
        foreach (var itemId in preset.ClothingItemIds)
        {
            var chip = AllChips().FirstOrDefault(c => c.ItemId == itemId);
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

    private void Save()
    {
        var updated = new ForcedOutfitPreset
        {
            Id = _presetId,
            Name = Name,
            IsEnabled = IsEnabled,
            SessionsPerSeverityPoint = SessionsPerSeverityPoint,
            IsFullOutfit = IsFullOutfit,
            ClothingItemIds = GetSelectedItems().Select(i => i.Id).Distinct().ToList(),
        };
        _repo.SaveForcedOutfitPreset(updated);
        _mainVm.NavigateTo(_returnTo());
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
