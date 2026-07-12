using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class StartSessionViewModel : ViewModelBase
{
    private readonly ActivePunishment? _forcedOutfitPunishment;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    [Reactive] private ConfirmDialogViewModel? _activeDialog;
    private double _currentBonus = 1.0;
    private double _outfitMultiplier = 1.0;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isForced;

    [Reactive] private bool _isLocked;
    private Outfit _outfit;

    [Reactive] private string _proposedOutfit = "";
    [Reactive] private OutfitViewModel _outfitVm = new([]);
    [Reactive] private string _multiplierDisplay = "×1.00";
    [Reactive] private string _itemCountDisplay = "";
    private int _regenCount;

    public StartSessionViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit? outfit = null)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();

        // Daily interest: accrues only if safe is open (combination known)
        if (state is not null && !state.IsSafeLocked && state.CurrentDebt > 0)
        {
            var days = (int)(DateTime.UtcNow - state.LastSessionAt).TotalDays;
            if (days > 0)
            {
                var rate = (decimal)state.DailyInterestRate;
                for (var i = 0; i < days; i++)
                {
                    state.CurrentDebt += Math.Round(state.CurrentDebt * rate, 0);
                }

                saveRepo.SaveGameState(state);
            }
        }

        DebtDisplay = $"Debt: ${state?.CurrentDebt:F2}";

        if (state?.OutfitLockedDate?.Date == DateTime.UtcNow.Date)
        {
            _outfit = new Outfit();
            _proposedOutfit = state.CurrentOutfit ?? "";
            _currentBonus = state.OutfitBonusPercent;
            _outfitMultiplier = state.OutfitMultiplier;
            _isLocked = true;
        }
        else
        {
            var wardrobe = saveRepo.GetWardrobe();
            var rules = saveRepo.GetOutfitRules();

            // Priority 1: active forced-outfit punishment with sessions remaining
            var forcedPunishment = saveRepo.GetActivePunishments()
                .FirstOrDefault(p => (p.RemainingOutfitForce?.Sessions ?? 0) > 0);

            if (forcedPunishment is not null)
            {
                _forcedOutfitPunishment = forcedPunishment;
                _isForced = true;

                if (forcedPunishment.ForceFullOutfit && forcedPunishment.ForcedClothingItemIds?.Count > 0)
                {
                    // Entire outfit is fixed: place only the specified items
                    var forcedItems = wardrobe
                        .Where(i => forcedPunishment.ForcedClothingItemIds.Contains(i.Id))
                        .ToList();
                    _outfit = BuildOutfitFromFixedItems(forcedItems);
                }
                else
                {
                    // Partial forced: specified items as seeds, builder fills the rest
                    var seeds = wardrobe
                        .Where(i => forcedPunishment.ForcedClothingItemIds?.Contains(i.Id) == true)
                        .ToList();
                    var builder = new OutfitBuilder(wardrobe, rules);
                    builder.AddSeedItems(seeds);
                    _outfit = outfit ?? builder.Build();
                }

                _proposedOutfit = _outfit.ToString();
            }
            else
            {
                // Priority 2: item rejection threshold — generate non-editable outfit
                var overThreshold = wardrobe.Where(i => i.RejectionCount >= WardrobeBalance.RejectionForceThreshold)
                    .ToList();
                if (overThreshold.Count > 0)
                {
                    _isForced = true;
                    var builder = new OutfitBuilder(wardrobe, rules);
                    builder.AddSeedItems(overThreshold);
                    _outfit = outfit ?? builder.Build();
                    _proposedOutfit = _outfit.ToString();
                }
                else
                {
                    // Normal generation
                    var builder = new OutfitBuilder(wardrobe, rules);
                    _outfit = outfit ?? builder.Build();
                    _proposedOutfit = _outfit.ToString();
                }
            }

            _outfitMultiplier = _outfit.TotalMultiplier;
        }

        RefreshDisplayProperties();

        this.WhenAnyValue(x => x.IsLocked, x => x.IsForced)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CanRegenOrModify)));

        AcceptCommand = ReactiveCommand.Create(Accept);
        RegenerateCommand = ReactiveCommand.CreateFromTask(ConfirmRegenerate);
        ModifyCommand = ReactiveCommand.CreateFromTask(ConfirmModify);
        QuitCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));
    }

    public string DebtDisplay { get; }

    /// <summary>True while the outfit can be regenerated or manually modified.</summary>
    public bool CanRegenOrModify => !IsLocked && !IsForced;

    public ICommand AcceptCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand ModifyCommand { get; }
    public ICommand QuitCommand { get; }

    private void RefreshDisplayProperties()
    {
        OutfitVm = OutfitViewModel.FromString(_proposedOutfit);
        var effective = _currentBonus * _outfitMultiplier;
        MultiplierDisplay = $"×{effective:F2}";
        var totalItems = OutfitVm.Layers.Sum(l => l.Items.Count);
        var layersCovered = OutfitVm.Layers.Count(l => l.HasItems);
        ItemCountDisplay = $"{totalItems} items · {layersCovered} layers covered";
    }

    /// <summary>Builds an Outfit from a fixed list of items without the builder.</summary>
    private static Outfit BuildOutfitFromFixedItems(List<ClothingItem> items)
    {
        var outfit = new Outfit();
        foreach (var item in items)
        {
            foreach (var layer in Outfit.LayersOrder.Where(l => item.Layers.Contains(l)))
            {
                if (outfit.AddItemOnLayer(item, layer))
                {
                    break;
                }
            }
        }

        return outfit;
    }

    private void Accept()
    {
        _saveRepo.AcceptItems(_outfit.Items);

        if (_forcedOutfitPunishment?.RemainingOutfitForce is { } remaining)
        {
            remaining.Sessions = (remaining.Sessions ?? 0) - 1;
            if ((remaining.Sessions ?? 0) <= 0)
            {
                _forcedOutfitPunishment.IsResolved = true;
            }

            _saveRepo.SaveActivePunishment(_forcedOutfitPunishment);
        }

        LockAndProceed();
    }

    private async Task ConfirmRegenerate()
    {
        var penalty = _regenCount == 0 ? 0.5 : 0.5;
        var resultBonus = _currentBonus * penalty;
        var dialog = new ConfirmDialogViewModel(
            Resources.Get("StartSessionRegenerateOutfitTitle"),
            string.Format(Resources.Get("StartSessionRegenerateMessageFormat"), resultBonus * 100),
            Resources.Get("StartSessionRegenerate"),
            Resources.Get("Cancel"));
        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

        if (!confirmed)
        {
            return;
        }

        _saveRepo.RejectItems(_outfit.Items);
        _currentBonus = resultBonus;
        _regenCount++;

        var builder = new OutfitBuilder(_saveRepo.GetWardrobe(), _saveRepo.GetOutfitRules());
        _outfit = builder.Build();
        _outfitMultiplier = _outfit.TotalMultiplier;
        ProposedOutfit = _outfit.ToString();
        RefreshDisplayProperties();

        // After 2 regenerations, lock automatically
        if (_regenCount >= 2)
        {
            LockAndProceed();
        }
    }

    private async Task ConfirmModify()
    {
        var resultBonus = _currentBonus * 0.2;
        var dialog = new ConfirmDialogViewModel(
            Resources.Get("StartSessionModifyOutfitTitle"),
            string.Format(Resources.Get("StartSessionModifyMessageFormat"), resultBonus * 100),
            Resources.Get("StartSessionModify"),
            Resources.Get("Cancel"));
        ActiveDialog = dialog;
        var confirmed = await dialog.Result;
        ActiveDialog = null;

        if (!confirmed)
        {
            return;
        }

        _currentBonus = resultBonus;
        _mainVm.NavigateTo(new ChangeOutfitViewModel(_mainVm, _saveRepo, _outfit, _currentBonus));
    }

    private void LockAndProceed()
    {
        var state = _saveRepo.GetGameState();
        if (state is not null && !_isLocked)
        {
            state.CurrentOutfit = _outfit.ToString();
            state.CurrentOutfitItemIds = _outfit.Items.Select(i => i.Id).ToList();
            state.OutfitLockedDate = DateTime.UtcNow;
            state.OutfitBonusPercent = _currentBonus;
            state.OutfitMultiplier = _outfit.TotalMultiplier;
            _saveRepo.SaveGameState(state);
        }

        IsLocked = true;
        _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, _currentBonus));
    }
}
