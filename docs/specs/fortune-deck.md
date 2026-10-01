# Feature Specification: Fortune Deck ("Il Ventaglio della Sorte")

## Purpose

The **Fortune Deck** is a paid draw minigame accessible from the Shop. The player pays `10 × idle wage` (added to the outstanding debt, exactly like any other expense) to draw a single card from a simulated physical deck and immediately resolve its effect.

Visually, the draw is presented as choosing one card from a fanned spread of face-down cards with a Victorian-styled back design; the chosen card is then revealed.

## Relationship to the Shop

This document introduces a **shared, limited stock pool** for the six collectible card types, used by both the Shop and the Fortune Deck. `shop.md`'s Card Catalogue reflects this: a card type becomes unavailable for direct purchase once no copies remain in the pool (see "Collectible Cards" below).

## Deck Model

The Fortune Deck is not a simple infinite weighted-random pick. It is modeled as a **simulated physical deck** composed of two structurally different kinds of entries:

1. **Collectible cards** — the six card types from the Shop's Card Catalogue (`COOLDOWN_BYPASS`, `DURATION_BOOST`, `PROBABILITY_BOOST`, `GRANT_PERMISSION`, `SOFTEN_PUNISHMENT`, `CANCEL_PUNISHMENT`). These have a **limited number of physical copies** shared with the Shop.
2. **Immediate effects** — resolved on the spot, with no lasting inventory item. These are always available and are effectively reshuffled back into the deck immediately after being drawn (their draw probability never changes over time).

### Collectible Cards — Shared Limited Stock

- Each collectible card type has a fixed total number of copies (see table below).
- A copy leaves the pool when it is either **purchased directly from the Shop** or **won via a Fortune Deck draw**. In both cases it is added to the player's inventory.
- A copy **returns to the pool** when the player **plays/consumes** that card (per the consumption rules already defined in `shop.md`, e.g. combining it with a permission request or targeting a punishment).
- If all copies of a type are currently out of the pool (held in the player's inventory), that type becomes **unavailable**: it cannot be purchased in the Shop and cannot be drawn from the Fortune Deck, until at least one copy returns to the pool.
- The deck's collectible composition at any point in time is always: *(fixed base copies) − (copies currently in the player's inventory)*.
- The player may draw a collectible card even if they already hold copies of that same type in their inventory (as long as at least one copy remains in the pool).

### Immediate Effects — Unlimited, Always Available

- Immediate effects resolve instantly when drawn: their outcome is applied right away, with nothing added to the player's inventory.
- Because they have no "consumption" event, they never leave the deck. Their draw probability is constant regardless of draw history.
- Immediate effects are grouped into: a direct cash win, suspended positive buffs, and negative effects (see below).

## Deck Persistence

- The deck persists for the entire duration of a playthrough. Its current state (which collectible copies are currently out of the pool) is derived directly from the player's inventory, so no separate persisted state is required beyond the inventory itself.
- Starting a **new game** resets the deck to full composition (inventory is emptied).
- The state of the deck (how many copies of each type remain) is **not shown** to the player. The Fortune Deck's contents should feel uncertain, not countable.

## Suspended Buffs and Debuffs (Immediate Positive/Negative Effects)

Some immediate effects are "immediate" variants of a Shop card, but with a **randomly chosen target** instead of a player-chosen one:

- `DURATION_BOOST`, `PROBABILITY_BOOST`, and `GRANT_PERMISSION` have suspended-buff variants; `SOFTEN_PUNISHMENT` and `CANCEL_PUNISHMENT` have suspended-buff variants targeting a pending punishment.
- `COOLDOWN_BYPASS` has **no** immediate/suspended variant — it is excluded from the Fortune Deck entirely, since a bypass only makes sense against a cooldown that already exists at the moment of use.
- The negative mirror of `PROBABILITY_BOOST` and `DURATION_BOOST` (debuffs) follow the same suspended-target mechanic.
- The negative mirror of `SOFTEN_PUNISHMENT` is the **Punishment Aggravation** effect (see below).

### Resolution mechanic for permission-targeting buffs/debuffs

- When drawn, a permission-targeting buff or debuff (duration or probability, positive or negative) randomly selects one permission type as its target **at draw time**, without revealing this choice to the player.
- It then remains **suspended indefinitely** until the player submits a request for that specific permission type, at which point it is automatically applied and consumed. There is no expiry.
- Multiple suspended buffs/debuffs may coexist simultaneously, including multiple targeting the same permission type.
- When a request is submitted for a permission type with multiple suspended modifiers, they combine **algebraically** using the same stacking rule as manually-played cards (e.g. a suspended `+10%` and a suspended `−5%` on the same permission type net to `+5%`).
- Resulting probability is clamped to `[0%, 95%]` for boosts per existing rules; a net **debuff** may reduce probability down to a floor of **0%** (the request is denied, but the player may retry after the cooldown, since the debuff has already been consumed).
- Resulting duration from a net debuff has a floor of **0 minutes**, and only applies if the permission was granted (the player's break ends immediately, handled by the existing permission timing system).
- `GRANT_PERMISSION` suspended variant: when its target permission type is next requested, approval is guaranteed (100%), following the same rule as the manually-played card. If **Diaper Play Override** is active and the suspended target is the bathroom permission, the effect has no valid application and the draw is treated as a whiff (see "Missing Target" below) — resolved at the moment the effect would have applied, not at draw time.

### Resolution mechanic for punishment-targeting buffs/debuffs

- When drawn, a punishment-targeting buff or debuff (soften, cancel, or aggravate) immediately selects one eligible pending punishment at random and applies its effect on the spot — there is no suspension involved for these, since eligible punishments are evaluated at draw time.
- **Missing target:** if there is no eligible pending punishment at the moment of the draw, the draw is lost — treated exactly as a whiff, no effect applied, no refund.

## Punishment Aggravation (negative mirror of `SOFTEN_PUNISHMENT`)

- Multiplies the target punishment's severity by **1.5×**, rounding up.
- Multiple aggravations on the same punishment stack multiplicatively (mirroring `SOFTEN_PUNISHMENT`'s stacking rule), with rounding up applied at each step.
- The target itself never exceeds its kind's maximum: the excess becomes a new pending punishment of the **same kind** (or a fine, when it is too small to reach that kind's minimum). See "Residual severity" in `punishments.md`.

- Targets a randomly-selected eligible pending punishment, following the same eligibility rules as `SOFTEN_PUNISHMENT` (fines are never eligible, as they are applied immediately and are never pending).
- Missing target: draw is lost (whiff), as described above.

A maximum exists per punishment kind under **Settings → Popup and Punishments**: `CornertimeMaxRequiredSeconds`, `WritingLinesMaxLines` and `ForcedOutfitMaxHours`. Aggravation — and ordinary assignment via `AssignPunishment` — never sizes a single punishment above these caps; what does not fit is spent on further punishments rather than discarded.

## Card Pool

### Collectible Cards (limited shared stock)

| Card | Shop Price | Copies in Deck |
| --- | --- | --- |
| `CANCEL_PUNISHMENT` | `120 × idle wage` | 2 |
| `GRANT_PERMISSION` | `110 × idle wage` | 2 |
| `SOFTEN_PUNISHMENT` | `50 × idle wage` | 3 |
| `DURATION_BOOST` (+5 min) | `50 × idle wage` | 4 |
| `PROBABILITY_BOOST` (+20%) | `20 × idle wage` | 4 |
| `DURATION_BOOST` (+3 min) | `30 × idle wage` | 5 |
| `PROBABILITY_BOOST` (+10%) | `10 × idle wage` | 5 |
| `COOLDOWN_BYPASS` | `10 × idle wage` | 6 |
| `DURATION_BOOST` (+1 min) | `10 × idle wage` | 6 |
| `PROBABILITY_BOOST` (+5%) | `5 × idle wage` | 6 |
| **Total collectible copies** | | **43** |

Copy counts were derived from Shop price as an inverse-rarity proxy (`copies ∝ 1/√price`), then manually flattened slightly while preserving relative ordering.

### Immediate Effects — Weighted Pool

The relative weight of collectible cards vs. immediate effects, and of positive vs. negative outcomes overall, is achieved by treating each physical collectible copy as **10 weight units** in a combined weighted pool. This keeps the *ratio* of physical copies intact while giving enough resolution to balance the immediate-effect groups without fractional copies.

| Category | Weight | Share of deck |
| --- | --- | --- |
| Collectible cards (43 copies × 10) | 430 | 30.1% |
| Immediate positive effects | 286 | 20.0% |
| Immediate negative effects | 715 | 50.0% |
| **Total** | **1431** | 100% |

#### Immediate Positive Effects (286 total)

**Direct cash win** (multiplier of the draw stake, weight 72)

| Outcome | Weight |
| --- | --- |
| 2× stake | 45 |
| 5× stake | 18 |
| 10× stake | 9 |

**Suspended `PROBABILITY_BOOST`** (weight 71)

| Outcome | Weight |
| --- | --- |
| +5% | 41 |
| +10% | 20 |
| +20% | 10 |

**Suspended `DURATION_BOOST`** (weight 71)

| Outcome | Weight |
| --- | --- |
| +1 min | 47 |
| +3 min | 15 |
| +5 min | 9 |

**Single-tier suspended buffs** (weight 72, proxy `1/Shop price`)

| Outcome | Weight |
| --- | --- |
| `SOFTEN_PUNISHMENT` (immediate, random eligible target) | 38 |
| `GRANT_PERMISSION` (suspended) | 18 |
| `CANCEL_PUNISHMENT` (immediate, random eligible target) | 16 |

#### Immediate Negative Effects (715 total)

| Group | Weight | Share of negatives |
| --- | --- | --- |
| Whiff (no effect) | 215 | 30% |
| Punishment Aggravation | 107 | 15% |
| Additional money loss | 131 | 18.3% |
| Suspended probability debuff | 131 | 18.3% |
| Suspended duration debuff | 131 | 18.3% |

**Additional money loss** (multiplier of the draw stake)

| Outcome | Weight |
| --- | --- |
| −1× stake | 87 |
| −2× stake | 44 |

**Suspended probability debuff**

| Outcome | Weight |
| --- | --- |
| −5% | 75 |
| −10% | 37 |
| −20% | 19 |

**Suspended duration debuff**

| Outcome | Weight |
| --- | --- |
| −1 min | 86 |
| −3 min | 28 |
| −5 min | 17 |

**Punishment Aggravation** — single outcome, weight 107 (see mechanic above).

**Whiff** — single outcome, weight 215. No effect is applied; the stake is simply lost.

## Draw Resolution Algorithm (summary)

1. Compute the current weighted pool: all immediate-effect entries (constant weights, as above) plus one entry per **currently available** collectible copy (base copies minus copies in the player's inventory), each weighted at 10 units. A collectible type with zero available copies contributes no entries.
2. Perform a single weighted-random pick across the combined pool.
3. Resolve the outcome:
   - **Collectible card** → add one copy to the player's inventory (identical to a Shop purchase).
   - **Immediate positive/negative effect with a permission target** → randomly select a permission type, create a suspended modifier.
   - **Immediate positive/negative effect with a punishment target** → randomly select an eligible pending punishment and apply the effect immediately; if none is eligible, resolve as a whiff instead.
   - **Cash win/loss** → adjust the player's balance immediately.
   - **Whiff** → no effect.
