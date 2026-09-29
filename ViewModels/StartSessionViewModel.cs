using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Converters;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public partial class StartSessionViewModel : ViewModelBase
{
    private readonly ActivePunishment? _forcedOutfitPunishment;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private double _currentBonus = 1.0;
    private double _outfitMultiplier = 1.0;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isForced;

    [Reactive] private bool _isLocked;
    private Outfit _outfit;

    private ClothingItem? _chastityDeviceItem;
    [Reactive] private string _proposedOutfit = "";
    [Reactive] private OutfitViewModel _outfitVm = new([]);
    [Reactive] private string _multiplierDisplay = "×1.00";
    [Reactive] private string _itemCountDisplay = "";
    private int _regenCount;

    public StartSessionViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit? outfit = null)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        // Daily interest: accrues only while the safe is open — grows debt, erodes credit toward zero.
        // Locked safe freezes the balance either way.
        saveRepo.ApplyDailyInterest(DateTime.UtcNow);

        var state = saveRepo.GetGameState();

        DebtDisplay = $"Debt: {CurrencyConverter.Format(state?.CurrentDebt ?? 0)}";

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
            var chastityDeviceItem = ResolveChastityDevice(state, wardrobe, saveRepo);
            _chastityDeviceItem = chastityDeviceItem;

            // A forced-outfit preset should never carry a device id of its own (the editor excludes
            // them), but strip any stale/legacy one out of already-saved data before seeding, so it
            // can never collide with the device resolved above.
            var deviceEligibleIds = saveRepo.GetChastityDeviceItems().Select(c => c.ClothingItemId).ToHashSet();

            // Priority 1: active forced-outfit punishment with sessions remaining (the oldest one pending —
            // later ones queue behind it, see SaveRepository.GetActiveForcedOutfitPunishment)
            var forcedPunishment = saveRepo.GetActiveForcedOutfitPunishment();

            if (forcedPunishment is not null)
            {
                _forcedOutfitPunishment = forcedPunishment;
                _isForced = true;

                if (forcedPunishment.ForceFullOutfit && forcedPunishment.ForcedClothingItemIds?.Count > 0)
                {
                    // Entire outfit is fixed: place the specified items, plus the resolved device
                    var forcedItems = wardrobe
                        .Where(i => forcedPunishment.ForcedClothingItemIds.Contains(i.Id) &&
                            !deviceEligibleIds.Contains(i.Id))
                        .ToList();
                    if (chastityDeviceItem is not null)
                    {
                        forcedItems.Add(chastityDeviceItem);
                    }

                    _outfit = BuildOutfitFromFixedItems(forcedItems);
                }
                else
                {
                    // Partial forced: specified items as seeds, builder fills the rest
                    var seeds = wardrobe
                        .Where(i => forcedPunishment.ForcedClothingItemIds?.Contains(i.Id) == true &&
                            !deviceEligibleIds.Contains(i.Id))
                        .ToList();
                    var builder = new OutfitBuilder(wardrobe, rules);
                    builder.AddSeedItems(seeds);
                    if (chastityDeviceItem is not null)
                    {
                        builder.AddSeedItems([chastityDeviceItem]);
                    }

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
                    if (chastityDeviceItem is not null)
                    {
                        builder.AddSeedItems([chastityDeviceItem]);
                    }

                    _outfit = outfit ?? builder.Build();
                    _proposedOutfit = _outfit.ToString();
                }
                else
                {
                    // Normal generation
                    var builder = new OutfitBuilder(wardrobe, rules);

                    if (chastityDeviceItem is not null)
                    {
                        builder.AddSeedItems([chastityDeviceItem]);
                    }

                    ClothingItem? plugAndPlaySeed = null;
                    if (state?.PendingPlugAndPlaySeedItemId is { } seedId)
                    {
                        plugAndPlaySeed = wardrobe.FirstOrDefault(i => i.Id == seedId);
                        if (plugAndPlaySeed is not null)
                        {
                            builder.AddSeedItems([plugAndPlaySeed]);
                        }
                    }

                    _outfit = outfit ?? builder.Build();
                    _proposedOutfit = _outfit.ToString();

                    // Resume the paused Plug & Play countdown if the seed was actually placed
                    if (plugAndPlaySeed is not null && _outfit.Items.Any(i => i.Id == plugAndPlaySeed.Id))
                    {
                        saveRepo.SaveActivePlugAndPlayAccessory(new ActivePlugAndPlayAccessory
                        {
                            ClothingItemId = plugAndPlaySeed.Id,
                            DonnedAt = DateTime.UtcNow,
                            RemoveAfterUtc = DateTime.UtcNow.AddMinutes(state?.PendingPlugAndPlayRemainingMinutes ?? 0)
                        });
                    }
                }
            }

            _outfitMultiplier = _outfit.TotalMultiplier;

            // The paused Plug & Play seed is consumed (placed or discarded) exactly once per
            // generation — leave it untouched if the outfit was already locked for today (no
            // generation happened on this construction) so a later same-day restart can still try.
            if (state?.PendingPlugAndPlaySeedItemId is not null)
            {
                state.PendingPlugAndPlaySeedItemId = null;
                state.PendingPlugAndPlayRemainingMinutes = null;
                saveRepo.SaveGameState(state);
            }
        }

        RefreshDisplayProperties();

        this.WhenAnyValue(x => x.IsLocked, x => x.IsForced)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CanRegenOrModify)));

        AcceptCommand = ReactiveCommand.Create(Accept);
        RegenerateCommand = ReactiveCommand.CreateFromTask(ConfirmRegenerate);
        ModifyCommand = ReactiveCommand.CreateFromTask(ConfirmModify);
        QuitCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new WelcomeViewModel(_mainVm)));

        ShowHardwareWarningIfNeeded(saveRepo);
    }

    /// <summary>
    ///     Warns the player up front about missing optional hardware/software (webcam, speech
    ///     recognizer, microphone) so it can be fixed before it silently disables idle popups
    ///     mid-session. Shown only when something is actually missing.
    /// </summary>
    private void ShowHardwareWarningIfNeeded(SaveRepository saveRepo)
    {
        var settings = saveRepo.GetGameSettings();
        var availability = HardwareAvailability.Check(
            WebcamPreviewService.EnumerateCameras(), settings.SelectedCameraIndex, _mainVm.Stt);
        if (!availability.HasAnyIssue)
        {
            return;
        }

        var lines = new List<string>();
        if (!availability.HasWebcam)
        {
            lines.Add(Resources.Get("HardwareCheckWebcamMissing"));
        }

        if (!availability.HasSpeechRecognizer)
        {
            lines.Add(Resources.Get("HardwareCheckSpeechRecognizerMissing"));
        }
        else if (!availability.HasMicrophone)
        {
            lines.Add(Resources.Get("HardwareCheckMicrophoneMissing"));
        }

        var dialog = new ConfirmDialogViewModel(
            Resources.Get("HardwareCheckTitle"),
            string.Join("\n\n", lines),
            Resources.Get("HardwareCheckAcknowledge"),
            null);
        _ = ShowConfirmAsync(dialog);
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

    /// <summary>
    ///     Picks the chastity device worn this session. It stays the same as the previous session
    ///     unless the safe was opened at the previous session's close (IsSafeLocked == false), in
    ///     which case a new one is drawn at random from the eligible items. Returns null if no
    ///     items are configured as eligible.
    /// </summary>
    private static ClothingItem? ResolveChastityDevice(GameState? state, List<ClothingItem> wardrobe, SaveRepository saveRepo)
    {
        var eligibleIds = saveRepo.GetChastityDeviceItems().Select(c => c.ClothingItemId).ToHashSet();
        var eligible = wardrobe.Where(i => eligibleIds.Contains(i.Id)).ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var keepCurrent = state?.CurrentChastityDeviceItemId is not null && state.IsSafeLocked;
        var chosen = keepCurrent
            ? wardrobe.FirstOrDefault(i => i.Id == state!.CurrentChastityDeviceItemId) ?? eligible[Random.Shared.Next(eligible.Count)]
            : eligible[Random.Shared.Next(eligible.Count)];

        if (state is not null && state.CurrentChastityDeviceItemId != chosen.Id)
        {
            state.CurrentChastityDeviceItemId = chosen.Id;
            saveRepo.SaveGameState(state);
        }

        return chosen;
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

        if (_forcedOutfitPunishment is not null)
        {
            // New continuous play stretch: reset the real-time progress baseline (see
            // SaveRepository.TickForcedOutfitProgress) so time spent with the app closed isn't credited.
            _forcedOutfitPunishment.ForcedOutfitLastTickUtc = DateTime.UtcNow;
            _forcedOutfitPunishment.ForcedOutfitRunElapsedSeconds = 0;
            _saveRepo.SaveActivePunishment(_forcedOutfitPunishment);
        }

        LockAndProceed();
    }

    private async Task ConfirmRegenerate()
    {
        const double penalty = 0.5;
        var resultBonus = _currentBonus * penalty;
        var dialog = new ConfirmDialogViewModel(
            Resources.Get("StartSessionRegenerateOutfitTitle"),
            string.Format(Resources.Get("StartSessionRegenerateMessageFormat"), resultBonus * 100),
            Resources.Get("StartSessionRegenerate"),
            Resources.Get("Cancel"));
        var confirmed = await ShowConfirmAsync(dialog);

        if (!confirmed)
        {
            return;
        }

        _saveRepo.RejectItems(_outfit.Items);
        _currentBonus = resultBonus;
        _regenCount++;

        var builder = new OutfitBuilder(_saveRepo.GetWardrobe(), _saveRepo.GetOutfitRules());
        if (_chastityDeviceItem is not null)
        {
            builder.AddSeedItems([_chastityDeviceItem]);
        }

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
        var confirmed = await ShowConfirmAsync(dialog);

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
            state.DiaperPlayActiveThisSession = _saveRepo.GetGameSettings().DiaperPlayEnabled &&
                DiaperPlayEvaluator.OutfitHasDiaperItem(state.CurrentOutfitItemIds, _saveRepo.GetDiaperPlayItems());
            _saveRepo.SaveGameState(state);
        }

        IsLocked = true;
        _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, _currentBonus));
    }
}
