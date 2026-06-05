using Avalonia;
using Avalonia.Controls;

namespace ChastiMaid.Views.Controls;

public partial class DebtWidget : UserControl
{
    // ── StyledProperties ────────────────────────────────────────────────────

    public static readonly StyledProperty<decimal> DebtTotalProperty =
        AvaloniaProperty.Register<DebtWidget, decimal>(nameof(DebtTotal));

    public static readonly StyledProperty<decimal> DebtSettledProperty =
        AvaloniaProperty.Register<DebtWidget, decimal>(nameof(DebtSettled));

    public static readonly StyledProperty<decimal> DebtRemainingProperty =
        AvaloniaProperty.Register<DebtWidget, decimal>(nameof(DebtRemaining));

    public static readonly StyledProperty<double> DebtProgressProperty =
        AvaloniaProperty.Register<DebtWidget, double>(nameof(DebtProgress));

    // ── Public accessors ────────────────────────────────────────────────────

    public decimal DebtTotal
    {
        get => GetValue(DebtTotalProperty);
        set => SetValue(DebtTotalProperty, value);
    }

    public decimal DebtSettled
    {
        get => GetValue(DebtSettledProperty);
        set => SetValue(DebtSettledProperty, value);
    }

    public decimal DebtRemaining
    {
        get => GetValue(DebtRemainingProperty);
        private set => SetValue(DebtRemainingProperty, value);
    }

    public double DebtProgress
    {
        get => GetValue(DebtProgressProperty);
        private set => SetValue(DebtProgressProperty, value);
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    public DebtWidget()
    {
        InitializeComponent();
    }

    // ── Derived property refresh ─────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DebtTotalProperty || change.Property == DebtSettledProperty)
        {
            DebtRemaining = DebtTotal - DebtSettled;
            DebtProgress  = DebtTotal > 0
                ? Math.Clamp((double)(DebtSettled / DebtTotal), 0.0, 1.0)
                : 0.0;
        }
    }
}
