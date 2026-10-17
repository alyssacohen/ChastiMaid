using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     A layer, slot or tag chip of the Wardrobe screen. <see cref="IsChecked" /> is true when every target has the
///     value, null (partial) when only some do, false when none does; filter chips are only ever true or false.
///     The ViewModel decides what a click does, so the view binds IsChecked one way and calls
///     <see cref="ToggleCommand" />.
/// </summary>
public partial class WardrobeChipViewModel : ViewModelBase
{
    private readonly Action _toggle;
    [Reactive] private string _countText = "";
    [Reactive] private bool? _isChecked = false;

    public WardrobeChipViewModel(string label, Action toggle)
    {
        Label = label;
        _toggle = toggle;
        ToggleCommand = ReactiveCommand.Create(Toggle);
    }

    public string Label { get; }

    public ICommand ToggleCommand { get; }

    public void Toggle()
    {
        _toggle();
    }

    /// <summary>Sets the state from how many of <paramref name="total" /> targets have the value.</summary>
    public void SetCount(int count, int total)
    {
        IsChecked = count == 0 ? false : count == total ? true : null;
        CountText = IsChecked is null ? $"{count}/{total}" : "";
    }
}

/// <summary>A removable chip of the active-filters bar.</summary>
public record WardrobeActiveFilter(string Text, string RemoveName, ICommand RemoveCommand);

/// <summary>An entry of the sort menu; <see cref="Mark" /> is the direction arrow of the active key.</summary>
public record WardrobeSortOption(string Label, bool IsActive, string Mark, ICommand SelectCommand);

/// <summary>A row of the Manage slots / Manage tags dialog.</summary>
public record WardrobeEntryRow(string Name, string Usage, string DeleteName, ICommand DeleteCommand);
