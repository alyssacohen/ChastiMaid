using System.Collections.ObjectModel;
using System.Windows.Input;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Converters;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>One catalogue row: name, description, price and how many the player currently owns.</summary>
public partial class ShopCardItemViewModel : ViewModelBase
{
    private readonly ILocalizer _l;

    public ShopCardItemViewModel(CardId id, decimal price, Action<ShopCardItemViewModel> onBuy, ILocalizer l)
    {
        _l = l;
        Id = id;
        Name = CardDisplay.NameOf(id, l);
        Description = CardDisplay.DescriptionOf(id, l);
        PriceDisplay = CurrencyConverter.Format(price);
        BuyCommand = ReactiveCommand.Create(() => onBuy(this), this.WhenAnyValue(x => x.CanBuy));
    }

    public CardId Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string PriceDisplay { get; }

    [Reactive] private int _ownedCount;
    [Reactive] private bool _canBuy;

    public string OwnedDisplay => _l.T("Held: {0}", OwnedCount);

    public ICommand BuyCommand { get; }
}

public partial class ShopViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private ILocalizer L => _mainVm.Localizer;
    private readonly SaveRepository _saveRepo;

    [Reactive] private decimal _currentDebt;
    [Reactive] private string _currentDebtDisplay = "";
    [Reactive] private string _debtLabel = "";

    public ShopViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;

        var settings = saveRepo.GetGameSettings();
        foreach (var def in CardCatalog.All)
        {
            Cards.Add(new ShopCardItemViewModel(def.Id, CardCatalog.PriceOf(def.Id, settings), OnBuy, L));
        }

        FortuneDrawButtonLabel = L.T("Draw — {0}",
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
        CurrentDebt = state?.CurrentDebt ?? 0;
        CurrentDebtDisplay = CurrencyConverter.Format(Math.Abs(CurrentDebt));
        DebtLabel = CurrentDebt < 0 ? L.T("Credit available") : L.T("Amount owed");

        var owned = _saveRepo.GetInventory();
        foreach (var card in Cards)
        {
            card.OwnedCount = owned.Count(c => c.CardId == card.Id);
            card.CanBuy = _saveRepo.AvailableCopies(card.Id) > 0;
        }
    }
}
