using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

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

    // Elementi proposti (preselezionati, in cima)
    public ObservableCollection<OutfitItemViewModel> ProposedItems { get; } = [];
    // Resto del guardaroba
    public ObservableCollection<OutfitItemViewModel> OtherItems { get; } = [];

    public ICommand RegenerateCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand BackCommand { get; }

    public ChangeOutfitViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit? currentOutfit = null)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        PopulateItems(currentOutfit);

        RegenerateCommand = ReactiveCommand.Create(Regenerate);

        ConfirmCommand = ReactiveCommand.Create(() =>
        {
            var selectedIds = ProposedItems.Concat(OtherItems)
                .Where(i => i.IsSelected)
                .Select(i => i.ItemId)
                .ToHashSet();

            // Ricostruisce l'outfit con i seed selezionati
            var seeds = _saveRepo.GetWardrobe().Where(w => selectedIds.Contains(w.Id)).ToList();
            var builder = new OutfitBuilder(_mainVm.ProfileRepository);
            builder.AddSeedItems(seeds);
            var newOutfit = builder.Build();

            _mainVm.NavigateTo(new StartSessionViewModel(_mainVm, _saveRepo, newOutfit));
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
                OtherItems.Add(new OutfitItemViewModel(item, isSelected: false));
        }
    }

    private void Regenerate()
    {
        var selectedIds = ProposedItems.Concat(OtherItems)
            .Where(i => i.IsSelected)
            .Select(i => i.ItemId)
            .ToHashSet();

        var seeds = _saveRepo.GetWardrobe().Where(w => selectedIds.Contains(w.Id)).ToList();
        var builder = new OutfitBuilder(_mainVm.ProfileRepository);
        builder.AddSeedItems(seeds);
        var newOutfit = builder.Build();
        PopulateItems(newOutfit);
    }
}
