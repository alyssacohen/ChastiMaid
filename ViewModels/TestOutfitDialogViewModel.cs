using ChastiMaid.Localization;
using System.Windows.Input;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Popup shown by Settings › Rules "Test rules" button: builds an outfit from the wardrobe/rules
///     currently edited in Settings (including unsaved changes) and lets the user regenerate it.
///     Pure preview — never touches item Weight/RejectionCount.
/// </summary>
public partial class TestOutfitDialogViewModel : ViewModelBase
{
    private readonly List<ClothingItem> _items;
    private readonly List<OutfitRule> _rules;

    [Reactive] private OutfitViewModel _outfitVm;

    private readonly ILocalizer _l;

    public TestOutfitDialogViewModel(List<ClothingItem> items, List<OutfitRule> rules, ILocalizer l)
    {
        _l = l;
        _items = items;
        _rules = rules;
        _outfitVm = BuildOutfitVm();
        RegenerateCommand = ReactiveCommand.Create(() => OutfitVm = BuildOutfitVm());
    }

    public ICommand RegenerateCommand { get; }

    private OutfitViewModel BuildOutfitVm()
    {
        var outfit = new OutfitBuilder(_items, _rules).Build();
        return OutfitViewModel.FromString(outfit.ToString(), _l);
    }
}
