using Avalonia;
using Avalonia.Controls;
using ChastiMaid.Converters;
using ChastiMaid.Localization;

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

    public static readonly StyledProperty<string> RemainingLabelProperty =
        AvaloniaProperty.Register<DebtWidget, string>(nameof(RemainingLabel), "");

    public static readonly StyledProperty<string> RemainingAmountTextProperty =
        AvaloniaProperty.Register<DebtWidget, string>(nameof(RemainingAmountText), "");

    public static readonly StyledProperty<string> TotalIncurredTextProperty =
        AvaloniaProperty.Register<DebtWidget, string>(nameof(TotalIncurredText), "");

    // ── Constructor ──────────────────────────────────────────────────────────
    public DebtWidget()
    {
        InitializeComponent();
    }

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

    public string RemainingLabel
    {
        get => GetValue(RemainingLabelProperty);
        private set => SetValue(RemainingLabelProperty, value);
    }

    public string RemainingAmountText
    {
        get => GetValue(RemainingAmountTextProperty);
        private set => SetValue(RemainingAmountTextProperty, value);
    }

    public string TotalIncurredText
    {
        get => GetValue(TotalIncurredTextProperty);
        private set => SetValue(TotalIncurredTextProperty, value);
    }

    // ── Derived property refresh ─────────────────────────────────────────────
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DebtTotalProperty || change.Property == DebtSettledProperty)
        {
            DebtRemaining = DebtTotal - DebtSettled;
            DebtProgress = DebtTotal > 0
                ? Math.Clamp((double)(DebtSettled / DebtTotal), 0.0, 1.0)
                : 0.0;
            // A control has no ViewModel to receive the localizer from: static access.
            var l = Localizer.Current;
            // A negative remaining balance is accumulated credit.
            RemainingLabel = DebtRemaining < 0 ? l.T("Credit") : l.T("Remaining");
            RemainingAmountText = CurrencyConverter.Format(Math.Abs(DebtRemaining));
            TotalIncurredText = l.T("Total incurred: {0}", CurrencyConverter.Format(DebtTotal));
        }
    }
}
