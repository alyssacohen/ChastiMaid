using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using static ChastiMaid.Services.GameClock;

namespace ChastiMaid.ViewModels;

public class OutfitItemViewModel(ClothingItem item, bool isSelected = false) : ViewModelBase
{
    private bool _isSelected = isSelected;

    public string Name { get; } = item.Name;
    public int ItemId { get; } = item.Id;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

public class ChangeOutfitViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly double _bonusPercent;

    public ObservableCollection<OutfitItemViewModel> ProposedItems { get; } = [];
    public ObservableCollection<OutfitItemViewModel> OtherItems { get; } = [];

    public ICommand ConfirmCommand { get; }
    public ICommand BackCommand { get; }

    public ChangeOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit currentOutfit, double bonusPercent)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        _bonusPercent = bonusPercent;

        PopulateItems(currentOutfit);

        ConfirmCommand = ReactiveCommand.Create(() =>
        {
            var selectedIds = ProposedItems.Concat(OtherItems)
                .Where(i => i.IsSelected)
                .Select(i => i.ItemId)
                .ToHashSet();

            var seeds = _saveRepo.GetWardrobe().Where(w => selectedIds.Contains(w.Id)).ToList();
            var builder = new OutfitBuilder(_mainVm.ProfileRepository);
            builder.AddSeedItems(seeds);
            var newOutfit = builder.Build();

            // Lock outfit with the already-penalized bonus
            var state = _saveRepo.GetGameState();
            if (state is not null)
            {
                state.CurrentOutfit = newOutfit.ToString();
                state.OutfitLockedDate = UtcNow();
                state.OutfitBonusPercent = _bonusPercent;
                state.OutfitMultiplier = newOutfit.TotalMultiplier;
                _saveRepo.SaveGameState(state);
            }

            _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, newOutfit, _bonusPercent));
        });

        BackCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, _saveRepo, currentOutfit)));
    }

    private void PopulateItems(Outfit? outfit)
    {
        ProposedItems.Clear();
        OtherItems.Clear();

        var allItems = _saveRepo.GetWardrobe();
        var proposedIds = outfit?.Items.Select(i => i.Id).ToHashSet() ?? [];

        foreach (var item in allItems)
        {
            if (proposedIds.Contains(item.Id))
                ProposedItems.Add(new OutfitItemViewModel(item, isSelected: true));
            else
                OtherItems.Add(new OutfitItemViewModel(item));
        }
    }
}
