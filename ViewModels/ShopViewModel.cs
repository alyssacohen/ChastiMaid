using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Views.Converters;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>One catalogue row: name, description, price and how many the player currently owns.</summary>
public partial class ShopCardItemViewModel : ViewModelBase
{
    public ShopCardItemViewModel(CardId id, decimal price, Action<ShopCardItemViewModel> onBuy)
    {
        Id = id;
        Name = CardDisplay.NameOf(id);
        Description = CardDisplay.DescriptionOf(id);
        PriceDisplay = CurrencyConverter.Format(price);
        BuyCommand = ReactiveCommand.Create(() => onBuy(this), this.WhenAnyValue(x => x.CanBuy));
    }

    public CardId Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string PriceDisplay { get; }

    [Reactive] private int _ownedCount;
    [Reactive] private bool _canBuy;

    public string OwnedDisplay => string.Format(Resources.Get("ShopOwnedFormat"), OwnedCount);

    public ICommand BuyCommand { get; }
}

public partial class ShopViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;

    [Reactive] private string _currentDebtDisplay = "";

    public ShopViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var settings = saveRepo.GetGameSettings();
        foreach (var def in CardCatalog.All)
        {
            Cards.Add(new ShopCardItemViewModel(def.Id, CardCatalog.PriceOf(def.Id, settings), OnBuy));
        }

        FortuneDrawButtonLabel = string.Format(
            Resources.Get("ShopFortuneDeckDrawFormat"),
            CurrencyConverter.Format(CardCatalog.FortuneDrawPrice(settings)));

        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new DashboardViewModel(_mainVm, _saveRepo)));
        OpenFortuneDeckCommand =
            ReactiveCommand.Create(() => _mainVm.NavigateTo(new FortuneDeckViewModel(_mainVm, _saveRepo)));

        Refresh();
    }

    public ObservableCollection<ShopCardItemViewModel> Cards { get; } = [];
    public string FortuneDrawButtonLabel { get; }

    public ICommand BackCommand { get; }
    public ICommand OpenFortuneDeckCommand { get; }

    private void OnBuy(ShopCardItemViewModel card)
    {
        _saveRepo.PurchaseCard(card.Id);
        Refresh();
    }

    private void Refresh()
    {
        var state = _saveRepo.GetGameState();
        CurrentDebtDisplay = CurrencyConverter.Format(state?.CurrentDebt ?? 0);

        var owned = _saveRepo.GetInventory();
        foreach (var card in Cards)
        {
            card.OwnedCount = owned.Count(c => c.CardId == card.Id);
            card.CanBuy = _saveRepo.AvailableCopies(card.Id) > 0;
        }
    }
}
