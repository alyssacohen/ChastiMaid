using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Transformation;
using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

public enum FortuneDeckDrawState
{
    Idle,
    Shuffling,
    SpreadingRibbon,
    LiftingCard,
    Revealing,
    Revealed,
}

/// <summary>
///     A single cosmetic card in the fanned spread — purely visual, generated fresh per draw and discarded
///     afterward (see docs/design/fortune-deck-ui.md "Performance notes"). Position/rotation/scale are combined
///     into a single bound <see cref="Transform" />, animated by a `TransformOperationsTransition` on the view's
///     `RenderTransform` — the supported Avalonia idiom for smoothly transitioning a combined transform, unlike a
///     `TransformGroup` of separately-bound sub-transforms (whose nested Transitions do not reliably fire).
/// </summary>
public partial class FortuneDeckCardViewModel : ViewModelBase
{
    private double _rotationDegrees;
    private double _widthScale = 1.0;
    private double _heightScale = 1.0;
    private double _x;
    private double _y;

    [Reactive] private TransformOperations? _transform;
    [Reactive] private int _zIndex;
    [Reactive] private bool _isRevealed;
    [Reactive] private string? _title;
    [Reactive] private string? _description;

    // Separate from IsRevealed so the title/description can stay invisible while the front face's chrome is
    // already growing to full size — faded in only once CardWidth/CardHeight have settled, avoiding visible
    // text reflow mid-resize.
    [Reactive] private double _textOpacity;

    // Actual layout size — grown via Width/Height (with its own Transitions in XAML), not a RenderTransform
    // scale. A render-scale would blow up the CONTENT too (title/description text, at 2× the reveal size,
    // rendering wildly oversized) since RenderTransform scales the whole visual subtree, not just the frame.
    [Reactive] private double _cardWidth = FortuneDeckAnimationSettings.CardWidth;
    [Reactive] private double _cardHeight = FortuneDeckAnimationSettings.CardHeight;

    /// <summary>
    ///     x/y are the desired position of the CARD'S CENTER, not its top-left corner — SetPose applies the
    ///     half-width/half-height correction internally so every caller can think purely in terms of "where the
    ///     card's center should sit" (e.g. arc positions, which are naturally centered around 0).
    ///     scaleX/scaleY are size multipliers against the base 80×120 (e.g. LiftScale) — they drive
    ///     <see cref="CardWidth" />/<see cref="CardHeight" />, not a visual stretch of already-laid-out content.
    /// </summary>
    public void SetPose(double? x = null, double? y = null, double? rotationDegrees = null,
        double? scaleX = null, double? scaleY = null)
    {
        _x = x ?? _x;
        _y = y ?? _y;
        _rotationDegrees = rotationDegrees ?? _rotationDegrees;
        _widthScale = scaleX ?? _widthScale;
        _heightScale = scaleY ?? _heightScale;

        CardWidth = FortuneDeckAnimationSettings.CardWidth * _widthScale;
        CardHeight = FortuneDeckAnimationSettings.CardHeight * _heightScale;

        // Built as a single explicit Matrix instead of a parsed CSS-style transform-function string — the
        // string form (translate/translateX+Y/rotate composed via Avalonia's own RenderTransformOrigin
        // pre/post-translation) produced a visually wrong result (confirmed the stored x/y/rotation values
        // are correct — see FortuneDeckView.axaml's RenderTransformOrigin="0,0", which disables Avalonia's
        // implicit centering so this matrix is the ONLY thing positioning the card, with no ambiguity about
        // composition order or which point rotation pivots around). Only translate+rotate now — sizing is
        // Width/Height, not a matrix scale.
        var cx = CardWidth / 2;
        var cy = CardHeight / 2;
        var radians = _rotationDegrees * Math.PI / 180.0;
        var matrix = Matrix.CreateTranslation(-cx, -cy)
                     * Matrix.CreateRotation(radians)
                     * Matrix.CreateTranslation(_x, _y);

        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendMatrix(matrix);
        Transform = builder.Build();
    }
}

public partial class FortuneDeckViewModel : ViewModelBase
{
    private readonly FortuneDeck _deck;
    private readonly MainWindowViewModel _mainVm;
    private readonly SaveRepository _saveRepo;

    private double _containerWidth = 800;
    private double _containerHeight = 400;

    [Reactive] private FortuneDeckDrawState _state = FortuneDeckDrawState.Idle;
    [Reactive] private string _currentDebtDisplay = "";

    public FortuneDeckViewModel(MainWindowViewModel mainVm, SaveRepository saveRepo)
    {
        _mainVm = mainVm;
        _saveRepo = saveRepo;
        var settings = saveRepo.GetGameSettings();
        _deck = new FortuneDeck(saveRepo, settings);
        var stake = Views.Converters.CurrencyConverter.Format(CardCatalog.FortuneDrawPrice(settings));
        DrawButtonLabel = string.Format(Resources.Get("ShopFortuneDeckDrawFormat"), stake);
        RefreshDebtDisplay();

        var canDraw = this.WhenAnyValue(x => x.State,
            s => s is FortuneDeckDrawState.Idle or FortuneDeckDrawState.Revealed);
        DrawCommand = ReactiveCommand.CreateFromTask(RunDrawAsync, canDraw);
        BackCommand = ReactiveCommand.Create(() => _mainVm.NavigateTo(new ShopViewModel(_mainVm, _saveRepo)), canDraw);

        ResetCosmeticDeck();
    }

    public ObservableCollection<FortuneDeckCardViewModel> CosmeticCards { get; } = [];
    public string DrawButtonLabel { get; }

    public ICommand DrawCommand { get; }
    public ICommand BackCommand { get; }

    private void RefreshDebtDisplay()
    {
        var state = _saveRepo.GetGameState();
        CurrentDebtDisplay = Views.Converters.CurrencyConverter.Format(state?.CurrentDebt ?? 0);
    }

    /// <summary>
    ///     Called by the view's code-behind whenever the draw area's measured size changes, so the arc scales
    ///     with the actual available space. While idle, also re-anchors the resting pile — otherwise it would
    ///     stay pinned to wherever the container happened to be sized at construction time (before the window's
    ///     first real layout pass), visibly off-center on anything but that exact original size.
    /// </summary>
    public void SetContainerSize(double width, double height)
    {
        if (width > 0)
        {
            _containerWidth = width;
        }

        if (height > 0)
        {
            _containerHeight = height;
        }

        if (State == FortuneDeckDrawState.Idle)
        {
            ResetCosmeticDeck();
        }
    }

    private double ArcRadius => _containerWidth * FortuneDeckAnimationSettings.ArcRadiusFraction;

    // The draw area's Canvas is unbounded (children aren't clipped to it), positioned at the top-left of the
    // Grid cell it fills — so every card pose is expressed relative to this anchor (the cell's actual center),
    // not relative to (0,0), which is why every SetPose call below routes through SetCardPose.
    private double AnchorX => _containerWidth / 2;
    private double AnchorY => _containerHeight / 2;

    private void SetCardPose(FortuneDeckCardViewModel card, double? x = null, double? y = null,
        double? rotationDegrees = null, double? scaleX = null, double? scaleY = null)
    {
        card.SetPose(x is { } vx ? AnchorX + vx : null, y is { } vy ? AnchorY + vy : null,
            rotationDegrees, scaleX, scaleY);
    }

    /// <summary>Per-card (dx, dy) stagger so a pile of cards sharing one logical slot reads as layered, not flat.</summary>
    private static (double X, double Y) StackOffset(int indexInStack)
    {
        var d = indexInStack * FortuneDeckAnimationSettings.ThicknessStepPx;
        return (d, d);
    }

    private void ResetCosmeticDeck()
    {
        CosmeticCards.Clear();
        for (var i = 0; i < FortuneDeckAnimationSettings.CosmeticCardCount; i++)
        {
            var card = new FortuneDeckCardViewModel { ZIndex = i };
            var stack = StackOffset(i);
            SetCardPose(card, stack.X, FortuneDeckAnimationSettings.ShufflePileOffsetY + stack.Y, 0, 1, 1);
            CosmeticCards.Add(card);
        }
    }

    private async Task RunDrawAsync()
    {
        // Resolve the outcome up front — the animation never blocks on or waits for game logic mid-sequence.
        var result = _deck.Draw();
        RefreshDebtDisplay();

        ResetCosmeticDeck();

        State = FortuneDeckDrawState.Shuffling;
        await RunShuffleAsync();

        State = FortuneDeckDrawState.SpreadingRibbon;
        await RunRibbonSpreadAsync();

        State = FortuneDeckDrawState.LiftingCard;
        var selected = CosmeticCards[CosmeticCards.Count / 2];
        await RunLiftAsync(selected);

        State = FortuneDeckDrawState.Revealing;
        await RunFlipAsync(selected, result);

        State = FortuneDeckDrawState.Revealed;
    }

    /// <summary>
    ///     Simulates a bridge shuffle: a small packet (the last <see cref="FortuneDeckAnimationSettings.ShufflePacketSize" />
    ///     cards) repeatedly peels off to the side, then tucks back underneath the main pile — rather than a
    ///     physically exact riffle of every individual card, which would cost far more code for a purely cosmetic
    ///     flourish. See docs/design/fortune-deck-ui.md Phase 1 and the reference sketch (two side-by-side packets,
    ///     one arcing under the other).
    /// </summary>
    private async Task RunShuffleAsync()
    {
        var packetStart = CosmeticCards.Count - FortuneDeckAnimationSettings.ShufflePacketSize;
        var pileY = FortuneDeckAnimationSettings.ShufflePileOffsetY;

        // Main pile stays put, stacked with a thickness stagger, for the whole shuffle.
        for (var i = 0; i < packetStart; i++)
        {
            var stack = StackOffset(i);
            var card = CosmeticCards[i];
            SetCardPose(card, stack.X, pileY + stack.Y, 0);
            card.ZIndex = i;
        }

        for (var round = 0; round < FortuneDeckAnimationSettings.ShuffleRounds; round++)
        {
            for (var offset = 0; offset < FortuneDeckAnimationSettings.ShufflePacketSize; offset++)
            {
                var i = packetStart + offset;
                var stack = StackOffset(offset);
                var card = CosmeticCards[i];
                SetCardPose(card, FortuneDeckAnimationSettings.ShufflePacketGapPx + stack.X, pileY + stack.Y,
                    FortuneDeckAnimationSettings.ShuffleSlantDegrees);
                card.ZIndex = packetStart + 100 + offset; // lifted above the main pile
            }

            await Task.Delay(FortuneDeckAnimationSettings.ShuffleHalfRoundMs);

            for (var offset = 0; offset < FortuneDeckAnimationSettings.ShufflePacketSize; offset++)
            {
                var i = packetStart + offset;
                var stack = StackOffset(offset);
                var card = CosmeticCards[i];
                SetCardPose(card, stack.X, pileY + stack.Y, 0);
                card.ZIndex = offset; // tucked back underneath the main pile
            }

            await Task.Delay(FortuneDeckAnimationSettings.ShuffleHalfRoundMs);
        }

        // Restore normal stacking order for the ribbon-spread phase.
        for (var i = 0; i < CosmeticCards.Count; i++)
        {
            CosmeticCards[i].ZIndex = i;
        }
    }

    private async Task RunRibbonSpreadAsync()
    {
        var count = CosmeticCards.Count;
        var halfAngle = FortuneDeckAnimationSettings.ArcSweepDegrees / 2.0;

        // The whole stack translates as a unit to the arc's leftmost point before cards start peeling off.
        var leftmost = ArcPosition(-halfAngle);
        foreach (var card in CosmeticCards)
        {
            SetCardPose(card, leftmost.X, leftmost.Y, -halfAngle);
        }

        await Task.Delay(FortuneDeckAnimationSettings.RibbonCardTravelMs / 2);

        for (var i = 0; i < count; i++)
        {
            var theta = -halfAngle + FortuneDeckAnimationSettings.ArcSweepDegrees * i / (count - 1);
            var pos = ArcPosition(theta);
            var card = CosmeticCards[i];
            SetCardPose(card, pos.X, pos.Y, theta);
            card.ZIndex = i;
            await Task.Delay(FortuneDeckAnimationSettings.RibbonStaggerMs);
        }

        await Task.Delay(FortuneDeckAnimationSettings.RibbonCardTravelMs);
    }

    /// <summary>
    ///     A minor arc, rising from the left end, peaking at the center, descending to the right — never a
    ///     semicircle. See docs/design/fortune-deck-ui.md "Arc geometry".
    /// </summary>
    private (double X, double Y) ArcPosition(double thetaDegrees)
    {
        var theta = thetaDegrees * Math.PI / 180.0;
        var halfAngle = FortuneDeckAnimationSettings.ArcSweepDegrees / 2.0 * Math.PI / 180.0;
        var x = Math.Sin(theta) * ArcRadius;
        var y = -(Math.Cos(theta) - Math.Cos(halfAngle)) * ArcRadius;
        return (x, y);
    }

    private async Task RunLiftAsync(FortuneDeckCardViewModel selected)
    {
        selected.ZIndex = CosmeticCards.Count + 1;
        // Returns to the same spot the shuffle pile occupied — not up toward the arc's peak — per the design
        // reference (the flip/reveal happens back down where the deck started).
        SetCardPose(selected, 0, FortuneDeckAnimationSettings.ShufflePileOffsetY, 0,
            FortuneDeckAnimationSettings.LiftScale, FortuneDeckAnimationSettings.LiftScale);
        await Task.Delay(FortuneDeckAnimationSettings.LiftMs);
    }

    private async Task RunFlipAsync(FortuneDeckCardViewModel selected, FortuneDrawResult result)
    {
        // Pivots on a horizontal axis (top-to-bottom hinge): height collapses to zero while width holds.
        selected.SetPose(scaleY: 0);
        await Task.Delay(FortuneDeckAnimationSettings.FlipHalfMs);

        selected.IsRevealed = true;
        (selected.Title, selected.Description) = DescribeOutcome(result);
        selected.SetPose(scaleY: FortuneDeckAnimationSettings.LiftScale);

        // Text stays invisible (TextOpacity still 0) until the regrow has visually settled, then fades in —
        // otherwise the title/description would reflow visibly while CardWidth/CardHeight are still animating.
        await Task.Delay(FortuneDeckAnimationSettings.CardResizeMs);
        selected.TextOpacity = 1;
        await Task.Delay(FortuneDeckAnimationSettings.TextFadeInMs);
    }

    /// <summary>Formats a value with an explicit leading sign (e.g. "+5", "-10") — the compact card titles
    /// always show the sign so a glance tells buff from debuff without reading the description.</summary>
    private static string Signed(int value)
    {
        return (value >= 0 ? "+" : "") + value;
    }

    /// <summary>Signed currency amount for cash-outcome titles (e.g. "+£4.00", "-£2.00") — explicit about the
    /// actual sum at a glance, rather than the previous ambiguous "2×" stake-multiplier notation.</summary>
    private static string SignedCurrencyTitle(decimal amount)
    {
        return (amount >= 0 ? "+" : "-") + Views.Converters.CurrencyConverter.Format(Math.Abs(amount));
    }

    private (string Title, string Description) DescribeOutcome(FortuneDrawResult result)
    {
        var stake = CardCatalog.FortuneDrawPrice(_saveRepo.GetGameSettings());
        return result.Kind switch
        {
            FortuneOutcomeKind.Collectible => (
                CardDisplay.NameOf(result.Card!.Value),
                CardDisplay.DescriptionOf(result.Card!.Value)),
            FortuneOutcomeKind.CashWin => (
                SignedCurrencyTitle(stake * result.Value),
                Resources.Get("FortuneOutcomeCashWinDesc")),
            FortuneOutcomeKind.CashLoss => (
                SignedCurrencyTitle(stake * result.Value),
                Resources.Get("FortuneOutcomeCashLossDesc")),
            FortuneOutcomeKind.SuspendedProbability => (
                string.Format(Resources.Get("FortuneOutcomePercentTitleFormat"), Signed(result.Value)),
                Resources.Get(result.Value >= 0 ? "FortuneOutcomeProbabilityBuffDesc" : "FortuneOutcomeProbabilityDebuffDesc")),
            FortuneOutcomeKind.SuspendedDuration => (
                string.Format(Resources.Get("FortuneOutcomeMinutesTitleFormat"), Signed(result.Value)),
                Resources.Get(result.Value >= 0 ? "FortuneOutcomeDurationBuffDesc" : "FortuneOutcomeDurationDebuffDesc")),
            FortuneOutcomeKind.SuspendedGrant => (
                Resources.Get("FortuneOutcomeGrantTitle"),
                Resources.Get("FortuneOutcomeGrantDesc")),
            FortuneOutcomeKind.SoftenRandom => (
                Resources.Get("FortuneOutcomeSoftenTitle"), Resources.Get("FortuneOutcomeSoftenDesc")),
            FortuneOutcomeKind.CancelRandom => (
                Resources.Get("FortuneOutcomeCancelTitle"), Resources.Get("FortuneOutcomeCancelDesc")),
            FortuneOutcomeKind.AggravateRandom => (
                Resources.Get("FortuneOutcomeAggravateTitle"), Resources.Get("FortuneOutcomeAggravateDesc")),
            FortuneOutcomeKind.Whiff => (Resources.Get("FortuneOutcomeWhiffTitle"), Resources.Get("FortuneOutcomeWhiffDesc")),
            _ => (Resources.Get("FortuneOutcomeWhiffTitle"), Resources.Get("FortuneOutcomeWhiffDesc")),
        };
    }
}
