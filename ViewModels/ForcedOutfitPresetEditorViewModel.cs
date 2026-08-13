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
    private readonly OutfitChipComposer _composer;
    private readonly MainWindowViewModel _mainVm;
    private readonly int _presetId;
    private readonly ProfileRepository _repo;
    private readonly OutfitRuleValidator _ruleValidator;
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

        // Chastity devices are never hand-picked here — they're always resolved at generation
        // time (random draw or carried over from the previous session), even for full outfits.
        var deviceEligibleIds = repo.GetChastityDeviceItems().Select(c => c.ClothingItemId).ToHashSet();
        _allItems = _allItems.Where(i => !deviceEligibleIds.Contains(i.Id)).ToList();

        _composer = new OutfitChipComposer(_allItems, ValidateRules);
        _ruleValidator = new OutfitRuleValidator(_allRules);

        _composer.PreselectByItemId(preset.ClothingItemIds);
        ValidateRules();

        var canSave = this.WhenAnyValue(
            x => x.CanFinalize, x => x.SelectedCount, x => x.Name,
            (canFinalize, count, name) => canFinalize && count > 0 && !string.IsNullOrWhiteSpace(name));

        SaveCommand = ReactiveCommand.Create(Save, canSave);
        ClearCommand = ReactiveCommand.Create(_composer.ClearSelection);
        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(_returnTo()));
    }

    public ObservableCollection<LayerGroupViewModel> Layers => _composer.Layers;

    public ICommand SaveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BackCommand { get; }

    private void ValidateRules()
    {
        _composer.ApplyStructuralReset();
        var outcome = _ruleValidator.Apply(_composer.AllChips());

        SelectedCount = _composer.AllChips().Count(c => c.State is ChipState.Selected);
        CanFinalize = outcome.CanFinalize;
        IsViolation = outcome.IsViolation;
        HasWarning = outcome.HasWarning;
        StatusMessage = outcome.StatusMessage;
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
