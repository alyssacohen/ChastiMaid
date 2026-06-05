using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChastiMaid.Views.Controls;

public partial class OutfitDrawer : UserControl
{
    // ── IsOpen StyledProperty ───────────────────────────────────────────────

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<OutfitDrawer, bool>(nameof(IsOpen));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    // ── CloseRequested event ────────────────────────────────────────────────

    public event EventHandler? CloseRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public OutfitDrawer()
    {
        InitializeComponent();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnScrimClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    // Keep IsVisible in sync with IsOpen
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            IsVisible = IsOpen;
    }
}
