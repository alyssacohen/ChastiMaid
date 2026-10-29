# Wardrobe & Outfit System

## Overview

The wardrobe system manages clothing items, outfit generation, and the daily outfit mechanic that affects the player's
earning multiplier during sessions.

## Data Model

### ClothingItem

Each item in the wardrobe has:

- **Name**: display name
- **Layers**: which layers this item can be placed on (one or more of: `underwear`, `extra`, `base`, `outerwear`, `accessories`)
- **Slots**: body slots this item occupies (e.g. `torso`, `legs`, `arms`, `crotch`, `waist`, `head`, `neck`, `feet`)
- **Multiplier**: earning multiplier contribution (1.0 = neutral, >1.0 = bonus, <1.0 = penalty)
- **Weight**: probability weight for random selection (higher = more likely to be picked)
- **Tags**: descriptive tags useful to apply filters (e.g. `maid`, `lingerie`, `classic`, `casual`)

### OutfitRule

Rules define relationships between item **tags** (`ClothingItem.Tags`), not specific items — a single rule covers
every item carrying either tag, so e.g. one rule on tags `garter-belt`/`stockings-attachable` covers every garter
belt and every attachable-stocking item without a row per pair. `SourceTag`/`RelatedTag` matching is case-insensitive
and trimmed:

- **Requires**: if any item carrying `SourceTag` is in the outfit, at least one item carrying `RelatedTag` must also
  be present (e.g. tag `garter-belt` requires tag `stockings-attachable`)
- **Incompatible**: if any item carrying `SourceTag` is in the outfit, no item carrying `RelatedTag` can be present,
  and vice versa (bidirectional)

To target a single specific item, give it a unique tag and reference that tag in the rule.

Rules are stored in the LiteDB collection `outfit_rules_v2` (`ProfileRepository`/`SaveRepository`), renamed from the
old item-to-item `outfit_rules` collection when rules moved to tags. The old `outfit_rules` collection is left behind
unread in any pre-existing `data/profile.db`/save-slot database — harmless dead data, not cleaned up automatically.

### Outfit

A built outfit tracks:

- Items placed on specific layers
- Slot occupancy per layer (prevents two items from using the same slot on the same layer)
- Total multiplier (product of all item multipliers)

## Layer System

Layers are ordered and represent dressing order:

1. **underwear** — underwear layer (probability: 100%)
2. **extra** — additional layer (probability: 70%)
3. **base** — base clothing layer (probability: 100%)
4. **outerwear** — outermost clothing, shoes (probability: 100%)
5. **accessories** — accessories, headwear (probability: 50%)

Each layer has a probability of being filled. If a layer is skipped (random roll), no items are placed on it.

An item can belong to multiple layers (e.g. stockings can go on `base` or `extra`), but is placed on only one layer in
the final outfit.

## Slot System

Slots represent body areas. Two items cannot occupy the same slot on the same layer. Slots and tags are
**user-defined** (e.g. `torso`, `legs`, `arms`, `waist`, `feet`; the seeds use their own per-language list): Settings ›
Wardrobe adds and deletes them, and keeps them in a catalogue (`WardrobeCatalog`, profile collection
`wardrobe_catalog`, optional `wardrobeCatalog` section of the contract) even while no item uses an entry. The
catalogue is always completed with every value the items (and, for tags, the rules) use. Names are compared ignoring
case and lower-cased on save; deleting a tag also deletes the rules that cite it.

## OutfitBuilder Algorithm

### Input

- All available clothing items (from wardrobe)
- Outfit rules (requires/incompatible)
- Optional seed items (user-selected items that must be included)

### Process

1. **Resolve required rules**: if any seed item has a "requires" rule, pick one of the required items and add
   it to the picking list. Repeat until no new requirements are found.
2. **Build outfit layer by layer** (in order: underwear → extra → base → outerwear → accessories):
    - Roll probability for the layer; skip if failed
    - Place seed/required items that belong to this layer
    - Fill remaining slots with random weighted candidates (up to 10 attempts per layer)
3. **Fallback for unplaced seeds**: try to place remaining seed items on any valid layer,
   replacing non-seed items in case of conflict.

### Weighted Selection
Items are chosen randomly with probability proportional to their `Weight` value.
`Weight` vary when items are accepted or rejected.

An item with `Weight` 0 is never a random pick: it only enters an outfit as a seed (forced outfit, Plug & Play,
chastity device, items kept while modifying), or when a seed's "requires" rule has no other match. Accepting or
rejecting it changes neither its `Weight` nor its rejection count, so it stays at 0.

To keep the accept/reject drift from collapsing the whole wardrobe to the floor or growing it
unbounded over a long game, every `RescaleEveryNRejections` (10) rejected outfits, all item `Weight`
values are rescaled so their total returns to what it was at new-game time — preserving each item's
relative proportion (the spread the player set up in Settings) without a hard per-item cap.

### Unwanted clothings (already implemented)
The app keep track of how many times each item has been rejected. The count can decrease by 1 if the
item is included in a subsequent outfit and accepted, but it can never be negative. If the count reaches 5,
a non-editable outfit is generated and locked for the next session (already implemented, to be balanced).

### Refusals and the employer's mood
Separately from the per-item count above, the save keeps a single tally of garments the player **explicitly** refused
(`GameState.OutfitRefusals`), which feeds the variable severity of punishments (see `punishments.md`):

- regenerating a proposed outfit adds every garment of the refused outfit (the chastity device excepted);
- modifying an outfit adds the proposed garments that were taken off — at least 1, even if garments were only added;
- the tally is capped at 20, and accepting a freshly proposed outfit untouched halves it (at most once a day, and
  not for an outfit the player had no say in, and not on a day that already recorded a refusal:
  `GameState.OutfitRefusalDate` remembers it across relaunches and back navigation).


## Session Flow: Outfit Proposal

### Daily Lock Mechanic

- The outfit is locked once per day (based on `OutfitLockedDate`). A day is the player's local calendar
  day, starting at local midnight (`GameDay.IsSameDay`); the same rule decides whether accepting an outfit
  forgives earlier refusals.
- If the player returns on the same day, the previously locked outfit is shown (no changes allowed)
- On a new day, a fresh outfit is generated

### Player Choices

When a new outfit is proposed, the player can:

1. **Accept** (bonus: 100%)
    - Outfit is locked immediately
    - Full bonus multiplier preserved
    - `Weight` of each accepted clothing item is decreased by 0.1

2. **Regenerate** (bonus: ×0.5 each time, max 2 regenerations)
    - A completely new random outfit is generated
    - `Weight` of rejected clothing items is increased by 0.3
    - Bonus is halved each time (100% → 50% → 25%)
    - After 2 regenerations, the outfit is auto-locked

3. **Modify** (bonus: ×0.2)
    - Player manually selects items from the wardrobe
        - Incompatible items (if any) must be greyed out on the fly
        - Required items (if any) must be indicated, the user should not be able to finalize the outfit
          in this case
    - The player can choose to set chosen items as the complete outfit (Save outfit) or ask the app to integrate it.
    - Selected items become "seeds" for the builder (even if the user chooses to save the outfit)
    - Builder fills remaining slots around the seeds
        - If the user had chosen to save the outfit, these garments selected by the generator, but not included,
          are considered as rejected
    - `Weight` of rejected clothing items is increased by 0.3
    - Bonus drops to 20% of current value
    - Outfit is locked immediately after confirmation

4. **Quit**
   In some cases, the outfit cannot be regenerated or modified (already locked or forced by a punishment).
   If the user can't comply with the request in any way, he just can return to the welcome screen

   The app keeps track of how many times each item has been rejected. The count decrease by 1 if the item is
   included in a subsequent outfit and accepted, but it can never drop below 0.
   If the count of one or more items reaches 5, a non-editable outfit with that items as the seed is generated
   and locked for the next session (see punishments).

### Effective Multiplier

The final earning multiplier applied to wages and chore credits is:

```
EffectiveMultiplier = OutfitMultiplier × OutfitBonusPercent
```

Where:

- `OutfitMultiplier` = product of all item multipliers in the outfit
- `OutfitBonusPercent` = penalty from regenerating/modifying (1.0 if accepted directly)

## Persistence

- **Profile DB**: master wardrobe and rules (editable in Settings)
- **Save DB**: snapshot of wardrobe and rules at game creation time (used during gameplay)
- **GameState**: stores `CurrentOutfit` (string), `OutfitLockedDate`, `OutfitBonusPercent`, `OutfitMultiplier`

