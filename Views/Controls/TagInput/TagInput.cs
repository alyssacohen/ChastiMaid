using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ChastiMaid.Views.Controls.TagInput;

public class TagInput : TemplatedControl
{
    private const string PartChipPanel = "PART_ChipPanel";
    private const string PartInputBox = "PART_InputBox";
    private const string PartSuggestionList = "PART_SuggestionList";

    // ── StyledProperties ──────────────────────────────────────────────────
    public static readonly StyledProperty<IList<string>?> TagsProperty =
        AvaloniaProperty.Register<TagInput, IList<string>?>(nameof(Tags));

    public static readonly StyledProperty<IEnumerable<string>> SuggestionsProperty =
        AvaloniaProperty.Register<TagInput, IEnumerable<string>>(
            nameof(Suggestions),
            Array.Empty<string>());

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TagInput, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string> SeparatorProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(Separator), ",");

    public static readonly StyledProperty<bool> AllowDuplicatesProperty =
        AvaloniaProperty.Register<TagInput, bool>(nameof(AllowDuplicates), false);

    public static readonly StyledProperty<bool> IsDropdownOpenProperty =
        AvaloniaProperty.Register<TagInput, bool>(nameof(IsDropdownOpen));

    public static readonly StyledProperty<IEnumerable<string>> FilteredSuggestionsProperty =
        AvaloniaProperty.Register<TagInput, IEnumerable<string>>(
            nameof(FilteredSuggestions),
            Array.Empty<string>());

    public static readonly StyledProperty<int> HighlightedIndexProperty =
        AvaloniaProperty.Register<TagInput, int>(nameof(HighlightedIndex), -1);

    private WrapPanelWithTrailingItem? _chipPanel;
    private CancellationTokenSource? _filterCts;
    private TextBox? _inputBox;
    private string _inputText = string.Empty;
    private INotifyCollectionChanged? _subscribedCollection;
    private ListBox? _suggestionList;
    private EventHandler<TextInputEventArgs>? _textInputHandler;

    // ── Public accessors ──────────────────────────────────────────────────
    public IList<string>? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public IEnumerable<string> Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Separator
    {
        get => GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    public bool AllowDuplicates
    {
        get => GetValue(AllowDuplicatesProperty);
        set => SetValue(AllowDuplicatesProperty, value);
    }

    public bool IsDropdownOpen
    {
        get => GetValue(IsDropdownOpenProperty);
        set => SetValue(IsDropdownOpenProperty, value);
    }

    public IEnumerable<string> FilteredSuggestions
    {
        get => GetValue(FilteredSuggestionsProperty);
        set => SetValue(FilteredSuggestionsProperty, value);
    }

    public int HighlightedIndex
    {
        get => GetValue(HighlightedIndexProperty);
        set => SetValue(HighlightedIndexProperty, value);
    }

    // ── Template ──────────────────────────────────────────────────────────
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Detach old handlers before resolving new parts
        if (_inputBox is not null)
        {
            _inputBox.GotFocus -= OnInputGotFocus;
            _inputBox.LostFocus -= OnInputLostFocus;
            _inputBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _inputBox.TextChanged -= OnInputTextChanged;
            if (_textInputHandler is not null)
            {
                _inputBox.RemoveHandler(TextInputEvent, _textInputHandler);
            }
        }

        if (_suggestionList is not null)
        {
            _suggestionList.PointerPressed -= OnSuggestionPressed;
        }

        // Resolve parts
        _chipPanel = e.NameScope.Find<WrapPanelWithTrailingItem>(PartChipPanel);
        _inputBox = e.NameScope.Find<TextBox>(PartInputBox);
        _suggestionList = e.NameScope.Find<ListBox>(PartSuggestionList);

        if (_chipPanel is not null && _inputBox is not null)
        {
            _chipPanel.TrailingItem = _inputBox;
            _chipPanel.InvalidateMeasure();
        }

        if (_inputBox is not null)
        {
            _inputBox.GotFocus += OnInputGotFocus;
            _inputBox.LostFocus += OnInputLostFocus;
            _inputBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            _inputBox.TextChanged += OnInputTextChanged;
            _textInputHandler = OnInputTextInput;
            _inputBox.AddHandler(TextInputEvent, _textInputHandler, RoutingStrategies.Tunnel);
        }

        if (_suggestionList is not null)
        {
            _suggestionList.PointerPressed += OnSuggestionPressed;
        }

        SubscribeToTags(Tags);
        RebuildChips();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TagsProperty)
        {
            SubscribeToTags(change.NewValue as IList<string>);
            RebuildChips();
        }
        else if (change.Property == SuggestionsProperty)
        {
            UpdateFilteredSuggestions();
        }
    }

    // ── Collection subscription ──────────────────────────────────────────
    private void SubscribeToTags(IList<string>? tags)
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnTagsCollectionChanged;
            _subscribedCollection = null;
        }

        if (tags is not INotifyCollectionChanged col)
        {
            return;
        }

        col.CollectionChanged += OnTagsCollectionChanged;
        _subscribedCollection = col;
    }

    private void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildChips);
    }

    // ── Chip building ────────────────────────────────────────────────────
    private void RebuildChips()
    {
        if (_chipPanel is null)
        {
            return;
        }

        var toRemove = _chipPanel.Children.Where(c => c != _chipPanel.TrailingItem).ToList();
        foreach (var c in toRemove)
        {
            _chipPanel.Children.Remove(c);
        }

        var tags = Tags;
        if (tags is null)
        {
            return;
        }

        for (var i = 0; i < tags.Count; i++)
        {
            _chipPanel.Children.Insert(i, BuildChipBorder(tags[i], i));
        }
    }

    private Border BuildChipBorder(string value, int index)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        chip.Classes.Add("chip");

        var label = new TextBlock { Text = value, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("chip-label");

        var xLabel = new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center };
        xLabel.Classes.Add("chip-x");

        chip.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Children = { label, xLabel },
        };

        var capturedIndex = index;
        chip.Tapped += (_, _) => RemoveChip(capturedIndex);

        return chip;
    }

    // ── Text input interception ──────────────────────────────────────────
    private void OnInputTextInput(object? sender, TextInputEventArgs e)
    {
        var sep = Separator;
        if (!string.IsNullOrEmpty(sep) && e.Text == sep)
        {
            CommitInput();
            e.Handled = true;
        }
    }

    // ── Keyboard handling ────────────────────────────────────────────────
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsDropdownOpen)
                {
                    ConfirmHighlighted();
                }
                else
                {
                    CommitInput();
                }

                e.Handled = true;
                break;

            case Key.Tab:
                if (!string.IsNullOrWhiteSpace(_inputText))
                {
                    CommitInput();
                    e.Handled = true;
                }

                break;

            case Key.Escape:
                if (_inputBox is not null)
                {
                    _inputBox.Text = string.Empty;
                }

                _inputText = string.Empty;
                CloseDropdown();
                e.Handled = true;
                break;

            case Key.Back:
                if (string.IsNullOrEmpty(_inputBox?.Text))
                {
                    var tags = Tags;
                    if (tags is { Count: > 0 })
                    {
                        RemoveChip(tags.Count - 1);
                        e.Handled = true;
                    }
                }

                break;

            case Key.Up:
                MoveHighlight(-1);
                e.Handled = true;
                break;

            case Key.Down:
                MoveHighlight(1);
                e.Handled = true;
                break;
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        _inputText = _inputBox?.Text ?? string.Empty;

        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(80, token);
                if (!token.IsCancellationRequested)
                {
                    UpdateFilteredSuggestions();
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    // ── Focus ─────────────────────────────────────────────────────────────
    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(":focus-within", true);
    }

    private void OnInputLostFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(":focus-within", false);
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(150);
            if (_inputBox is not null && !_inputBox.IsFocused)
            {
                CloseDropdown();
            }
        });
    }

    // ── Autocomplete ─────────────────────────────────────────────────────
    private void UpdateFilteredSuggestions()
    {
        var input = _inputText.Trim();
        if (string.IsNullOrEmpty(input))
        {
            FilteredSuggestions = Array.Empty<string>();
            IsDropdownOpen = false;
            return;
        }

        IEnumerable<string> tags = Tags ?? Array.Empty<string>();
        var results = Suggestions
            .Where(s => s.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                        && !tags.Contains(s, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s)
            .ToList();

        FilteredSuggestions = results;
        IsDropdownOpen = results.Count > 0;
    }

    private void OnSuggestionPressed(object? sender, PointerPressedEventArgs e)
    {
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is string text)
        {
            CommitSuggestion(text);
        }
    }

    // ── Chip operations ──────────────────────────────────────────────────
    private void CommitInput()
    {
        var value = _inputText.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var tags = Tags;
        if (tags is null)
        {
            return;
        }

        if (AllowDuplicates || !tags.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(value);
        }

        if (_inputBox is not null)
        {
            _inputBox.Text = string.Empty;
        }

        _inputText = string.Empty;
        IsDropdownOpen = false;
        HighlightedIndex = -1;
    }

    private void CommitSuggestion(string value)
    {
        var tags = Tags;
        if (tags is null)
        {
            return;
        }

        if (AllowDuplicates || !tags.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(value);
        }

        if (_inputBox is not null)
        {
            _inputBox.Text = string.Empty;
        }

        _inputText = string.Empty;
        IsDropdownOpen = false;
        HighlightedIndex = -1;
    }

    private void RemoveChip(int index)
    {
        var tags = Tags;
        if (tags is null || index < 0 || index >= tags.Count)
        {
            return;
        }

        tags.RemoveAt(index);
    }

    private void CloseDropdown()
    {
        IsDropdownOpen = false;
        HighlightedIndex = -1;
    }

    private void MoveHighlight(int delta)
    {
        var count = FilteredSuggestions.Count();
        if (count == 0)
        {
            return;
        }

        HighlightedIndex = Math.Clamp(HighlightedIndex + delta, 0, count - 1);
    }

    private void ConfirmHighlighted()
    {
        var list = FilteredSuggestions.ToList();
        if (HighlightedIndex >= 0 && HighlightedIndex < list.Count)
        {
            CommitSuggestion(list[HighlightedIndex]);
        }
        else
        {
            CommitInput();
        }
    }
}
