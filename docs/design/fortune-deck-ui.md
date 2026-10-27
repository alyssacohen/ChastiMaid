# Feature Specification: Fortune Deck UI &amp; Draw Animation

## Purpose

This document specifies the **presentation layer** for the Fortune Deck draw: the visual sequence and interaction pattern shown when the player draws a card, on the "Il Ventaglio della Sorte" screen.

Game logic, probability model, and outcome resolution are specified separately in [fortune-deck.md](fortune-deck.md) and are **not** duplicated here. This document assumes the draw's outcome has already been resolved by the domain layer *before* the animation begins (see "Relationship to Draw Resolution" below).

## Relationship to Draw Resolution

The animation is purely presentational and must never influence or delay the actual outcome:

1. The player triggers a draw. The ViewModel immediately requests the resolved outcome from the domain/service layer (per the algorithm in `fortune-deck.md`).
2. Once the outcome is known, the animation sequence plays to completion.
3. The final "reveal" step displays the already-resolved outcome. The animation timeline never blocks on or waits for game logic mid-sequence — all game-logic computation happens up front, before the first animation frame.

This separation keeps the animation replaceable/tunable independently of the draw logic, and avoids any risk of the visual outcome and the actual outcome disagreeing due to timing issues.

## Cosmetic Card Count

The animation displays a fixed **cosmetic deck of a constant size** (visual card count), independent of the actual weighted deck size described in `fortune-deck.md` (which varies dynamically based on collectible card stock). The player never sees the real deck's composition or size (per the "hidden deck state" rule in `fortune-deck.md`); the visual deck exists purely to sell the physical-card-draw fantasy.

- Recommended cosmetic count: **16–24 cards** — enough to form a convincing continuous ribbon, low enough to stay performant on the Surface Go.
- The exact count is a tunable visual constant, not tied to any game-logic value.

## Animation Sequence

The sequence has four phases, played back to back with no player interaction required until the final state.

### Phase 1 — Shuffle

- The deck starts as a single stacked pile, centered on screen, no rotation.
- The deck performs **3 quick shuffle rounds**: each round briefly spreads all cards horizontally into a loose overlapping row, then recomposes them back into a stack.
- Each round (spread + recompose) should be brief and snappy — this phase reads as "riffling," not as a slow, deliberate motion.
- Target budget: ~150–200ms per half-round (spread or recompose), ~1.0–1.3s total for all 3 rounds.

### Phase 2 — Ribbon spread (arc)

- Immediately after the last shuffle round, the whole stack translates as a single unit to the position of the leftmost point of the target arc.
- Cards then peel off from the stack **one at a time, in left-to-right order**, each animating along a circular arc path to its final resting position. Each card's animation is staggered slightly behind the previous one, producing a continuous "dealt in one sweeping motion" ribbon effect rather than all cards moving simultaneously.
- The arc is a **minor arc** (noticeably less than a semicircle) — approximately **60–70° of total sweep**, rising from the left end, peaking at the center, and descending to the right end. It must **not** read as a half-circle or a flat horizontal fan.
- Cards overlap heavily along the ribbon: only a thin sliver of each card is visible except for the topmost (rightmost-drawn) card at each point, matching a classic card-spread/ribbon-shuffle look. Z-order increases left to right, so each new card lands visually on top of the previous one.
- Target budget: ~40–50ms stagger between consecutive cards, ~300–350ms travel duration per card. For a 20-card cosmetic deck this phase totals roughly 1.1–1.2s end to end (stagger accumulation + last card's travel time).

### Phase 3 — Card selection and lift

- Once the ribbon settles, the card at (or near) the arc's center is selected as the draw.
- The selected card lifts out of the ribbon toward the center of the screen, scaling up moderately (roughly 1.2–1.5×) as it approaches its resting position. The rest of the ribbon remains fully visible and fully opaque behind it — **cards must not fade or dim** during this phase.
- Target budget: ~500–600ms.

### Phase 4 — Flip and reveal

- The lifted card performs a horizontal flip: it scales down to zero on its horizontal axis (simulating turning edge-on), its content is swapped from card-back to the resolved outcome's face, then it scales back up to full width.
- The reveal content (outcome name/description, per the resolved draw result) is shown once the flip completes.
- Target budget: ~250–300ms for the flip itself.

### Overall budget

Total sequence, start to finish: **approximately 3–3.5 seconds**. This is a deliberate, ceremonious pace — not a snappy UI transition — reflecting the significance of the wager. All numeric timings above are tunable constants, not hard requirements; keep them grouped together (e.g. a single `FortuneDeckAnimationSettings` class or resource) so the whole pace can be retuned without touching animation logic.

## Avalonia Implementation Approach

### State machine in the ViewModel

Represent the sequence as an explicit state enum, driven by the ViewModel and exposed reactively (e.g. via `ObservableAsPropertyHelper<T>`), not tracked in code-behind:

```csharp
public enum FortuneDeckDrawState
{
    Idle,
    Shuffling,
    SpreadingRibbon,
    LiftingCard,
    Revealing,
    Revealed
}
```

The draw command (`ReactiveCommand` backed by an `async Task`) should:
1. Resolve the outcome from the domain layer.
2. Drive the state machine through each phase in order, `await`-ing each phase's animation before advancing.
3. Only expose the resolved outcome to the View once `Revealing` begins.

### Animating card position and rotation

- Use `RenderTransform` (translation + rotation + scale) for all per-card motion, never `Margin` or other layout-affecting properties. `RenderTransform` changes are compositor-only and avoid triggering a new layout pass — important for maintaining frame rate on the Surface Go.
- For the shuffle (Phase 1) and simple state-to-state transitions, declarative `Transitions` on a `Style` selector (keyed to a `Classes` toggle per shuffle round) are sufficient and idiomatic.
- For the ribbon spread (Phase 2), where each card needs an **individually staggered start time**, declarative `Transitions` alone are not expressive enough (they all fire on the same property change). Use `Avalonia.Animation.Animation` built in code with explicit `KeyFrames`, or stagger the start of each card's `Transitions`-based animation with a per-card `await Task.Delay(...)` inside the orchestrating method. Prefer the latter only if the delay count stays small (cosmetic deck size, ~16–24) — otherwise batch via a single `Animation` per card scheduled with a computed start offset.
- For the flip (Phase 4), which has two visually distinct halves (scale-X to zero, swap content, scale-X back to one), build the animation as two sequential `KeyFrames` animations (or a single `Animation` with a `KeyFrame` at the midpoint) so the content swap can be synchronized exactly to the moment the card is edge-on and its face is not visible to the player.

### Arc geometry

Compute each card's target position along the circular arc using the half-angle and radius as named constants:

```csharp
// theta ranges from -halfAngle to +halfAngle across the cosmetic deck
var x = Math.Sin(thetaRadians) * arcRadius;
var y = -(Math.Cos(thetaRadians) - Math.Cos(halfAngleRadians)) * arcRadius;
var rotationDegrees = thetaDegrees;
```

This keeps the arc's peak at the center (highest point) and both ends level with each other at the baseline, producing the "minor arc" shape validated in the mockup — not a full semicircle.

### Screen size

- The arc's radius and total width must scale with the available width of the draw screen (desktop vs. Surface Go). Compute `arcRadius` from a fraction of the container's actual width at layout time rather than a fixed pixel constant, so the ribbon never overflows the visible area.
- Card size (and therefore the visible "sliver" width in the ribbon) should also scale down proportionally on narrower layouts, rather than reducing the arc's angular sweep — changing the sweep angle would alter the silhouette of the gesture; changing card scale preserves it.

### Performance notes (Surface Go)

- Cap simultaneous actively-animating cards where practical; the staggered ribbon spread already keeps this naturally low (each card's individual travel animation is short relative to the stagger interval, so only a handful of cards are mid-transition at any instant).
- Keep all per-card visuals as simple flat-color shapes with a single border and a small number of child elements (matching the card-back design already defined) — avoid per-card effects like blur, drop shadow, or opacity gradients, which are comparatively expensive to composite when many are on screen at once during the ribbon phase.
- Since the deck state is never shown to the player, there is no need to keep the full 16–24 cosmetic cards backed by real view-models tied to actual deck contents — they can be lightweight, purely visual placeholder elements, generated fresh for each draw and discarded afterward.

## Interaction Rules

- The draw button/control is disabled for the full duration of the animation sequence (from the moment the draw is triggered until Phase 4 completes) to prevent overlapping draws.
- No skip/fast-forward control is specified at this stage; the sequence always plays to completion. (Revisit if playtesting shows the ~3–3.5s pace becomes tedious on repeated draws.)
