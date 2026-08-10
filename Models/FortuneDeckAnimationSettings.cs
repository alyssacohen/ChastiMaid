namespace ChastiMaid.Models;

/// <summary>
///     Tunable timing/geometry constants for the Fortune Deck draw animation. Grouped here per
///     docs/design/fortune-deck-ui.md so the whole sequence's pace can be retuned in one place without touching
///     animation logic. Purely visual — never tied to real deck contents or game logic.
/// </summary>
public static class FortuneDeckAnimationSettings
{
    /// <summary>Visual card count of the fanned spread. Not tied to the actual weighted deck size.</summary>
    public const int CosmeticCardCount = 20;

    /// <summary>Cosmetic card size in pixels — standard 2:3 (width:height) playing-card proportions.</summary>
    public const double CardWidth = 80;

    public const double CardHeight = 120;

    /// <summary>Per-card offset (both axes) used everywhere cards sit stacked in a single pile, so the pile
    /// reads as many layered cards rather than one flat rectangle.</summary>
    public const double ThicknessStepPx = 1.2;

    /// <summary>Resting Y position of the shuffle pile — below the draw area's center, matching the design
    /// reference — used as both the shuffle anchor and the lift/reveal target.</summary>
    public const double ShufflePileOffsetY = 140;

    // --- Phase 1: Shuffle ---
    public const int ShuffleRounds = 3;
    public const int ShuffleHalfRoundMs = 175;

    /// <summary>Cards that peel off to the side and tuck back underneath each round — half the deck, so the two
    /// packets read as comparably sized stacks, matching the reference sketch (not one big pile + a sliver).</summary>
    public const int ShufflePacketSize = CosmeticCardCount / 2;

    public const double ShufflePacketGapPx = 90;
    public const double ShuffleSlantDegrees = 4;

    // --- Phase 2: Ribbon spread (arc) ---
    public const int RibbonStaggerMs = 45;
    public const int RibbonCardTravelMs = 320;

    /// <summary>Total arc sweep, in degrees. A minor arc — well under a semicircle.</summary>
    public const double ArcSweepDegrees = 65;

    /// <summary>Arc radius as a fraction of the draw screen's available width.</summary>
    public const double ArcRadiusFraction = 0.42;

    // --- Phase 3: Selection and lift ---
    public const int LiftMs = 550;

    /// <summary>Base 80×120 card scaled up to ~172×258 — enough room for a real title + description
    /// rendered inside the revealed card face, not just a cosmetic pop (bumped from the original 1.35).</summary>
    public const double LiftScale = 2.15;

    // --- Phase 4: Flip and reveal ---
    public const int FlipHalfMs = 140;

    /// <summary>Must match the Width/Height <c>DoubleTransition</c> duration on the card panel in
    /// FortuneDeckView.axaml (<c>0:0:0.32</c>) — how long RunFlipAsync waits for the post-flip regrow to
    /// visually finish before the title/description are allowed to fade in.</summary>
    public const int CardResizeMs = 320;

    /// <summary>Must match the Opacity <c>DoubleTransition</c> duration on the title/description panel in
    /// FortuneDeckView.axaml (<c>0:0:0.22</c>).</summary>
    public const int TextFadeInMs = 220;
}
