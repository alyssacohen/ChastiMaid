using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Views.Controls;

public partial class OutfitDrawer : UserControl
{
    // ── IsOpen StyledProperty ───────────────────────────────────────────────
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<OutfitDrawer, bool>(nameof(IsOpen));

    // ── Outfit StyledProperty (avoids overriding DataContext from parent) ───
    public static readonly StyledProperty<OutfitViewModel?> OutfitProperty =
        AvaloniaProperty.Register<OutfitDrawer, OutfitViewModel?>(nameof(Outfit));

    // ── Layers (derived from Outfit, bindable from AXAML) ──────────────────
    public static readonly StyledProperty<IReadOnlyList<OutfitLayerViewModel>> LayersProperty =
        AvaloniaProperty.Register<OutfitDrawer, IReadOnlyList<OutfitLayerViewModel>>(
            nameof(Layers), []);

    // ── MultiplierDisplay StyledProperty ────────────────────────────────────
    public static readonly StyledProperty<string?> MultiplierDisplayProperty =
        AvaloniaProperty.Register<OutfitDrawer, string?>(nameof(MultiplierDisplay));

    // ── Constructor ──────────────────────────────────────────────────────────
    public OutfitDrawer()
    {
        InitializeComponent();
        IsVisible = false; // closed by default; OnPropertyChanged syncs when IsOpen changes
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public OutfitViewModel? Outfit
    {
        get => GetValue(OutfitProperty);
        set => SetValue(OutfitProperty, value);
    }

    public IReadOnlyList<OutfitLayerViewModel> Layers
    {
        get => GetValue(LayersProperty);
        private set => SetValue(LayersProperty, value);
    }

    public string? MultiplierDisplay
    {
        get => GetValue(MultiplierDisplayProperty);
        set => SetValue(MultiplierDisplayProperty, value);
    }

    // ── CloseRequested event ────────────────────────────────────────────────
    public event EventHandler? CloseRequested;

    // ── Property sync ────────────────────────────────────────────────────────
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            IsVisible = IsOpen;
        }

        if (change.Property == OutfitProperty)
        {
            Layers = Outfit?.Layers ?? [];
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────
    private void OnScrimClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
