using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ChastiMaid.Controls;

/// <summary>
/// A composite input control combining a WrapPanel of removable chips
/// with an inline TextBox. All values are free-form: any non-duplicate
/// value is accepted. Supports autocomplete suggestions via ChipInputViewModel.
/// </summary>
public partial class ChipInputControl : UserControl
{
    private ChipInputViewModel? _vm;

    public ChipInputControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext wiring ───────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _vm = DataContext as ChipInputViewModel;
        if (_vm is null) return;

        _vm.Values.CollectionChanged += (_, _) => RebuildChips();
        RebuildChips();

        // Apply drop shadow to dropdown
        DropdownPanel.BoxShadow = new BoxShadows(
            new BoxShadow
            {
                OffsetX = 0, OffsetY = 4,
                Blur = 12, Spread = 0,
                Color = Color.FromArgb(26, 44, 33, 24) // #1A2C2118
            });

        InputBox.GotFocus  += (_, _) => SetFocusState(true);
        InputBox.LostFocus += (_, _) => SetFocusState(false);
        InputBox.KeyDown   += OnInputKeyDown;

        SuggestionList.PointerPressed += OnSuggestionPressed;
    }

    // ── Chip rendering ───────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds all chip borders in the WrapPanel, preserving the InputBox
    /// as the last child.
    /// </summary>
    private void RebuildChips()
    {
        if (_vm is null) return;

        // Remove all children except InputBox (always last)
        while (ChipWrapPanel.Children.Count > 1)
            ChipWrapPanel.Children.RemoveAt(0);

        for (int i = 0; i < _vm.Values.Count; i++)
        {
            var chip = BuildChipBorder(_vm.Values[i], i, _vm.PendingDeleteIndex == i);
            ChipWrapPanel.Children.Insert(i, chip);
        }
    }

    private Border BuildChipBorder(string value, int index, bool isPending)
    {
        var bg = isPending
            ? (IBrush)Application.Current!.Resources["BrushSurfaceMuted"]!
            : (IBrush)Application.Current!.Resources["BrushChipDefault"]!;

        var chip = new Border
        {
            Background    = bg,
            CornerRadius  = new CornerRadius(4),
            Padding       = new Thickness(8, 0),
            Height        = 30,
            Margin        = new Thickness(0, 2, 4, 2),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Classes           = { "chip-label" },
            Text              = value,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var removeBtn = new Button
        {
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(6, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize        = 13,
            Content         = "×",
            Foreground      = (IBrush)Application.Current!.Resources["TextMuted"]!
        };

        var capturedIndex = index;
        removeBtn.Click += (_, _) =>
        {
            _vm?.RemoveChip(capturedIndex);
            RebuildChips();
        };

        chip.Child = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing     = 4,
            Children    = { label, removeBtn }
        };

        return chip;
    }

    // ── Keyboard handling ────────────────────────────────────────────────

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null) return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.OemComma:
                _vm.CommitInput();
                e.Handled = true;
                break;

            case Key.Enter:
                if (_vm.IsDropdownOpen)
                    _vm.ConfirmHighlighted();
                else
                    _vm.CommitInput();
                e.Handled = true;
                break;

            case Key.Tab:
                if (!string.IsNullOrWhiteSpace(_vm.InputText))
                {
                    _vm.CommitInput();
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                _vm.InputText = string.Empty;
                _vm.CloseDropdown();
                e.Handled = true;
                break;

            case Key.Back:
                if (string.IsNullOrEmpty(_vm.InputText) && _vm.Values.Count > 0)
                {
                    int last = _vm.Values.Count - 1;
                    if (_vm.PendingDeleteIndex == last)
                        _vm.RemoveChip(last);
                    else
                        _vm.PendingDeleteIndex = last;

                    RebuildChips();
                    e.Handled = true;
                }
                break;

            case Key.Up:
                _vm.MoveHighlight(-1);
                e.Handled = true;
                break;

            case Key.Down:
                _vm.MoveHighlight(1);
                e.Handled = true;
                break;
        }
    }

    // ── Dropdown interaction ─────────────────────────────────────────────

    private void OnSuggestionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null) return;
        var row  = (e.Source as Visual)?.FindAncestorOfType<Border>();
        var text = row?.FindDescendantOfType<TextBlock>()?.Text;
        if (text is not null)
        {
            _vm.CommitSuggestion(text);
            RebuildChips();
        }
    }

    // ── Visual state ─────────────────────────────────────────────────────

    private void SetFocusState(bool focused)
    {
        ContainerBorder.BorderBrush = focused
            ? (IBrush)Application.Current!.Resources["BrushButtonPrimary"]!
            : (IBrush)Application.Current!.Resources["BrushBorderSubtle"]!;
        ContainerBorder.BorderThickness = new Thickness(0, 0, 0, focused ? 2 : 1);
    }
}
