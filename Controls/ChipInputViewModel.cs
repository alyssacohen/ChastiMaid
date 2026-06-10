using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.Controls;

/// <summary>
/// ViewModel for ChipInputControl. Manages the list of selected values,
/// the current input text, autocomplete suggestions, and dropdown state.
/// All fields are free-form: any non-duplicate value is accepted as a chip.
/// </summary>
public partial class ChipInputViewModel : ReactiveObject
{
    // ── Public bindable properties ───────────────────────────────────────

    /// <summary>
    /// The confirmed values displayed as chips.
    /// Two-way bound to the parent ViewModel via a passed-in ObservableCollection.
    /// </summary>
    public ObservableCollection<string> Values { get; }

    /// <summary>
    /// Vocabulary for autocomplete suggestions (all values already used across
    /// the wardrobe for this field type, fetched dynamically from the parent ViewModel).
    /// </summary>
    [Reactive] private IEnumerable<string> _suggestions = [];

    /// <summary>
    /// Placeholder text shown when the control is empty and unfocused.
    /// </summary>
    [Reactive] private string _placeholder = "add...";

    /// <summary>
    /// Current text in the inline TextBox.
    /// </summary>
    [Reactive] private string _inputText = string.Empty;

    /// <summary>
    /// Filtered suggestions matching the current InputText (prefix, case-insensitive).
    /// Excludes values already present in Values.
    /// </summary>
    private IEnumerable<string> _filteredSuggestions = [];
    public IEnumerable<string> FilteredSuggestions
    {
        get => _filteredSuggestions;
        private set => this.RaiseAndSetIfChanged(ref _filteredSuggestions, value);
    }

    /// <summary>
    /// Whether the autocomplete dropdown is currently visible.
    /// </summary>
    [Reactive] private bool _isDropdownOpen;

    /// <summary>
    /// Index of the highlighted row in the dropdown (-1 = none).
    /// </summary>
    [Reactive] private int _highlightedIndex = -1;

    /// <summary>
    /// Index of the chip currently selected for deletion via Backspace (-1 = none).
    /// The first Backspace on an empty input highlights the last chip;
    /// a second Backspace removes it.
    /// </summary>
    [Reactive] private int _pendingDeleteIndex = -1;

    // ── Constructor ──────────────────────────────────────────────────────

    public ChipInputViewModel(ObservableCollection<string> values)
    {
        Values = values;

        // Recompute filtered suggestions whenever InputText or Suggestions change
        this.WhenAnyValue(x => x.InputText, x => x.Suggestions)
            .Throttle(TimeSpan.FromMilliseconds(80))
            .Subscribe(_ => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateFilteredSuggestions));

        // Open/close dropdown based on filtered results and input length
        this.WhenAnyValue(x => x.FilteredSuggestions, x => x.InputText)
            .Subscribe(_ =>
            {
                IsDropdownOpen = InputText.Length > 0 && FilteredSuggestions.Any();
            });
    }

    // ── Public methods ───────────────────────────────────────────────────

    /// <summary>
    /// Commits the current InputText as a new chip if non-empty and not duplicate.
    /// Resets InputText and dropdown state.
    /// </summary>
    public void CommitInput()
    {
        var value = InputText.Trim();
        if (string.IsNullOrEmpty(value)) return;
        if (!Values.Contains(value, StringComparer.OrdinalIgnoreCase))
            Values.Add(value);
        InputText = string.Empty;
        IsDropdownOpen = false;
        HighlightedIndex = -1;
        PendingDeleteIndex = -1;
    }

    /// <summary>
    /// Commits a specific value from the autocomplete dropdown.
    /// </summary>
    public void CommitSuggestion(string value)
    {
        if (!Values.Contains(value, StringComparer.OrdinalIgnoreCase))
            Values.Add(value);
        InputText = string.Empty;
        IsDropdownOpen = false;
        HighlightedIndex = -1;
    }

    /// <summary>
    /// Removes the chip at the given index.
    /// </summary>
    public void RemoveChip(int index)
    {
        if (index >= 0 && index < Values.Count)
            Values.RemoveAt(index);
        PendingDeleteIndex = -1;
    }

    /// <summary>
    /// Closes the dropdown and resets navigation state.
    /// </summary>
    public void CloseDropdown()
    {
        IsDropdownOpen = false;
        HighlightedIndex = -1;
    }

    /// <summary>
    /// Moves the highlighted index up or down within the dropdown list.
    /// </summary>
    public void MoveHighlight(int delta)
    {
        var count = FilteredSuggestions.Count();
        if (count == 0) return;
        HighlightedIndex = Math.Clamp(HighlightedIndex + delta, 0, count - 1);
    }

    /// <summary>
    /// Confirms the currently highlighted dropdown row.
    /// Falls back to CommitInput if nothing is highlighted.
    /// </summary>
    public void ConfirmHighlighted()
    {
        var list = FilteredSuggestions.ToList();
        if (HighlightedIndex >= 0 && HighlightedIndex < list.Count)
            CommitSuggestion(list[HighlightedIndex]);
        else
            CommitInput();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private void UpdateFilteredSuggestions()
    {
        var input = InputText.Trim();

        if (string.IsNullOrEmpty(input))
        {
            FilteredSuggestions = [];
            return;
        }

        FilteredSuggestions = Suggestions
            .Where(s => s.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                     && !Values.Contains(s, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s)
            .ToList();
    }
}
