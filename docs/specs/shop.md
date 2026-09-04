# Feature Specification: Shop

## Purpose

The **Shop** lets the player spend virtual pounds on cards that modify permission requests or reduce pending punishments.

A purchase is always possible, regardless of the player's current balance: its price is added to the amount owed
exactly like a fine, growing the debt rather than requiring credit to be spent first. This mirrors how a Fortune
Deck draw's stake is charged (see [fortune-deck.md](fortune-deck.md)), so both spending paths behave the same way.

The Shop also provides a paid draw from the **Fortune Deck**. The deck's contents, odds, and resolution rules are specified separately in [fortune-deck.md](fortune-deck.md).

## Card Catalogue

Every card type has a **fixed number of physical copies**, shared between direct Shop purchase and Fortune Deck draws — see [fortune-deck.md](fortune-deck.md) "Collectible Cards" for the shared-stock model and the copy counts per type. A card type becomes unavailable for purchase once no copies remain in the pool; the remaining count itself is never shown to the player.

Unless stated otherwise, every price is expressed in virtual pounds and uses the configured **idle wage** as its price basis.

| Card | Effect | Price |
| --- | --- | --- |
| `COOLDOWN_BYPASS` | Clears the cooldown for a permission request. | `10 × idle wage` |
| `DURATION_BOOST` | Adds a selected number of minutes to the duration of a permission request. Available values: `+1`, `+3`, and `+5` minutes. | `10 × added minutes × idle wage` |
| `PROBABILITY_BOOST` | Adds a selected percentage-point bonus to the grant probability of a permission request. Available values: `+5%`, `+10%`, and `+20%`. | `percentage points × idle wage` |
| `GRANT_PERMISSION` | Guarantees approval of a permission request. | `110 × idle wage` |
| `SOFTEN_PUNISHMENT` | Halves the severity of one eligible pending punishment, rounding up. | `50 × idle wage` |
| `CANCEL_PUNISHMENT` | Removes one eligible pending punishment. | `120 × idle wage` |

## Permission Cards

`COOLDOWN_BYPASS`, `DURATION_BOOST`, `PROBABILITY_BOOST`, and `GRANT_PERMISSION` are played from the permission-request menu, immediately before the player submits a request.

### `COOLDOWN_BYPASS`

- Clears the cooldown for the upcoming request.
- The card is consumed even if the selected permission is not currently on cooldown.
- The card is also consumed if the player plays it and then requests a different permission type from the intended one.

### `DURATION_BOOST`

- Adds its selected number of minutes to the requested permission's duration.
- The permission can still be denied. If it is denied, the card is consumed with no effect.

### `PROBABILITY_BOOST`

- Adds its selected percentage-point value to the configured grant probability.
- The resulting probability from probability-boost cards must never exceed `95%`.
- If **Diaper Play Override** is active, the card has no effect for a bathroom permission and is consumed.

### `GRANT_PERMISSION`

- Behaves as a permission probability modifier, but sets the chance of approval to `100%`.
- It is the only card that can guarantee approval; probability boosts alone cannot exceed `95%`.
- If **Diaper Play Override** is active, the card has no effect for a bathroom permission and is consumed.

### Combining Permission Cards

- The player may play multiple cards before submitting one permission request.
- Cards of the same type stack. For example, a `+10%` and a `+5%` probability boost produce a `+15%` bonus before the `95%` cap is applied.
- Different card effects may be combined. For example, the player may combine `GRANT_PERMISSION` with `DURATION_BOOST`.
- When combining cards, the player is responsible for clearing an active cooldown. If the player uses other cards on a request that is still on cooldown, those cards can be consumed without delivering their intended benefit. A `COOLDOWN_BYPASS` may be included to prevent this.

## Punishment Cards

`SOFTEN_PUNISHMENT` and `CANCEL_PUNISHMENT` are played from the punishments menu and target a pending punishment.

Fines are not eligible targets: they are applied immediately and therefore are never pending punishments.

### `SOFTEN_PUNISHMENT`

- Halves the target punishment's severity, rounding up.
- Apply the reduction to the quantity that represents the punishment's severity. Examples:
  - `3` hours of remaining forced-outfit time become `2` hours.
  - `35` writing lines become `18` lines.
  - `5` minutes of corner time become `3` minutes.
- Multiple `SOFTEN_PUNISHMENT` cards stack multiplicatively. For example, two cards reduce the severity to one quarter of its original value, with rounding up applied at each halving.

### `CANCEL_PUNISHMENT`

- Removes the targeted pending punishment.
- For a forced-outfit punishment, it removes the forced-outfit time that has not yet been credited, subject to the forced-outfit timing rules below.

## Forced-Outfit Timing Rules

A forced-outfit punishment starts automatically in the session after it is assigned. `SOFTEN_PUNISHMENT` and `CANCEL_PUNISHMENT` may target it either in the session in which it is assigned, before it begins, or during a later forced-outfit session.

- For `SOFTEN_PUNISHMENT`, always calculate the reduction from the punishment's current required duration (multiplying it directly), not by discarding already-credited time.
- If the recalculated required duration is less than or equal to the time already credited, treat the punishment as completed.
- Neither completing nor cancelling a forced-outfit punishment changes the outfit during the current session. The forced outfit remains in effect until that session ends, including if the player closes and immediately restarts the game.
- The change takes effect at the next outfit change, which occurs when the next game session begins.

## Fortune Deck Draw

The player may pay `10 × idle wage` to draw from the **Fortune Deck** ("Il Ventaglio della Sorte"). The available rewards and their resolution are defined in [fortune-deck.md](fortune-deck.md).
