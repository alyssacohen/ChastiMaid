# Wardrobe & Outfit System

## Overview
The wardrobe system manages clothing items, outfit generation, and the daily outfit mechanic that affects the player's earning multiplier during sessions.

## Data Model

### ClothingItem
Each item in the wardrobe has:
- **Name**: display name
- **Layers**: which layers this item can be placed on (one or more of: `undies`, `base`, `extra`, `outer`, `gears`)
- **Slots**: body slots this item occupies (e.g. `torso`, `legs`, `arms`, `crotch`, `waist`, `head`, `neck`, `feet`)
- **Multiplier**: earning multiplier contribution (1.0 = neutral, >1.0 = bonus, <1.0 = penalty)
- **Weight**: probability weight for random selection (higher = more likely to be picked)
- **Tags**: descriptive tags (e.g. `maid`, `lingerie`, `fetish`, `casual`)

### OutfitRule
Rules define relationships between items:
- **Requires**: if item A is in the outfit, at least one of its required items must also be present (e.g. girdle requires stockings)
- **Incompatible**: if item A is in the outfit, incompatible items cannot be present (e.g. diaper is incompatible with stockings)

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

An item can belong to multiple layers (e.g. stockings can go on `base` or `extra`), but is placed on only one layer in the final outfit.

## Slot System
Slots represent body areas. Two items cannot occupy the same slot on the same layer. Available slots:
- `torso`, `legs`, `arms`, `crotch`, `waist`, `head`, `neck`, `feet`

## OutfitBuilder Algorithm

### Input
- All available clothing items (from wardrobe)
- Outfit rules (requires/incompatible)
- Optional seed items (user-selected items that must be included)

### Process
1. **Resolve required rules**: if any seed item has a "requires" rule, pick one of the required items and add 
it to the picking list. Repeat until no new requirements are found.
2. **Build outfit layer by layer** (in order: undies → extra → base → outer → gears):
   - Roll probability for the layer; skip if failed
   - Place seed/required items that belong to this layer
   - Fill remaining slots with random weighted candidates (up to 10 attempts per layer)
3. **Fallback for unplaced seeds**: try to place remaining seed items on any valid layer, 
replacing non-seed items in case of conflict.

### Weighted Selection
Items are chosen randomly with probability proportional to their `Weight` value.
`Weight` vary when items are accepted or rejected.

## Session Flow: Outfit Proposal

### Daily Lock Mechanic
- The outfit is locked once per day (based on `OutfitLockedDate`)
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

## Settings UI
The Settings view provides tabs for:
- **Wardrobe**: add/edit/delete clothing items (name, layers, slots, multiplier, weight, tags)
- **Outfit Rules**: add/edit/delete requires/incompatible relationships between items
