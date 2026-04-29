using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;

namespace ChastiMaid.ViewModels;

public class StartSessionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;
    private readonly Outfit _outfit;

    public string ProposedOutfit { get; }
    public string DebtDisplay { get; }

    public ICommand AcceptCommand { get; }
    public ICommand ChangeOutfitCommand { get; }

    public StartSessionViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo, Outfit? outfit = null)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var state = saveRepo.GetGameState();
        DebtDisplay = $"Debt: {state?.CurrentDebt:F0} credits";

        var builder = new OutfitBuilder(mainVm.ProfileRepository);
        _outfit = outfit ?? builder.Build();
        ProposedOutfit = _outfit.ToString();

        AcceptCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new ConfirmOutfitViewModel(_mainVm, _saveRepo, _outfit)));

        ChangeOutfitCommand = ReactiveCommand.Create(
            () => _mainVm.NavigateTo(new ChangeOutfitViewModel(_mainVm, _saveRepo, _outfit)));
    }
}
