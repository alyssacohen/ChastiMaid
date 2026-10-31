# Punishment system
Punishments are used to promote proper behavior and discourage laziness or attempts to cheat.

## How to get a punishment?
Punishments are assigned when the user is late on calls when idle, when he is too late or too early
on finishing a chore, when there is a negative outcome on impossibile questions, or when he refuses to
wear a clothing item too many times.

## How to remove a punishment?
When a user is assigned a punishment it will go on a list of pending punishments except for Fine which takes effect immediately. 
When the user has time to dedicate to a punishment, she can choose to execute it.
If he manages to complete it, it is archived and removed.

## Punishment types
1. **Fine.**
The simplest form of punishment. An additional sum is added to the current debt, extending the workload required for its repayment.

2. **Cornertime**
The app asks the user to stand in a corner and not move for a period of min/max time and check using the camera that he don't move. If the user moves, it will add some time and keep checking, if the time goes over the max because the user can't stand still, the punishment fails, a fine will be applied and the user can retry it from the start at another time.

3. **Writing lines**
The user must type correctly one ore more sentences for a certain number of repetitions.
The app will show the line to write at the top and check each charachter typed by te user. In case of a
mistype, it will force the user to restart typing that line and eventually add one or more penalty lines
for the mistake.
If the user notices that the task is becoming too tedious and taking up too much time, they can stop it,
but in this case the punishment fails and a fine will be applied. The user can then try to do it again
from the beginning at another time.

4. **Forced outfit**
There will be some clothing items that are pretty unconfortable to wear.
The user configures one or more named outfit presets in Settings, each composed the same way an outfit is proposed
at the start of a session (picking garments per layer, respecting requires/incompatible rules). Each preset has its
own severity value (hours of real, app-open playtime per severity point) and its own "full outfit" flag: when checked, the outfit generator
is skipped entirely and only the preset's exact garments are worn; when unchecked, the preset's garments are used as
seeds and the generator fills in the rest as usual.
Both a global toggle (whether Forced Outfit can be assigned at all) and a per-preset toggle (whether that specific
preset currently participates) control eligibility. When the punishment is assigned, one preset is chosen uniformly
at random among the currently enabled ones, and stays fixed for the whole duration of that punishment. The user
will only have the option to accept it or close the application so that until the penalty is served it will not be
possible to continue with the game.

If a new Forced Outfit punishment is assigned while another one is still pending, it does not replace or merge with
the existing one: both are tracked independently, but only one is ever in effect at a time. The oldest pending one
(by assignment time) is the one currently in effect; any later ones queue behind it and only start counting down
once the ones ahead of them are resolved. In the pending punishments list, a queued Forced Outfit is shown as
awaiting its turn rather than with a time countdown, since its countdown has not started yet.

The punishment is served in real time, not by session count: any time the application is open — idle or busy, it
doesn't matter — counts toward the requirement, and closing and reopening the app does not shortcut it. To stop
brief, repeated app opens from adding up toward the requirement, each continuous stretch of play has a 30-minute
grace period before its time starts being credited.

Maybe other types will be added.

## Punishment Severity
Each infraction has a certain importance level, so the resulting punishment must have proportional severity.

For each punishment type, a SeverityValue is assigned. When a punishment is applied, one or more parameters (such as duration, repetition count, or session count) are calculated proportionally to the punishment's severity level.

Example:
- Cornertime has a SeverityValue of 0.5 (representing minutes per severity point)
- Fine has a SeverityValue of 5 (representing dollars per severity point)
- The player receives a punishment with Severity = 10
- The punishment will be either a Cornertime of 10 × 0.5 = 5 minutes, or a Fine of 10 × 5 = $50

This system allows different punishment types to be balanced against each other, making them interchangeable in gameplay.

### Variable severity

The severity of an infraction is not applied as rolled: when a punishment is assigned it is first multiplied by a
coefficient between 1 and 3 that reflects how displeased the employer currently is. The constants live in
`Services/SeverityBalance.cs`; they are deliberately not settings (the contract only says that Madame "does not
disclose her methods").

**Mood** (0 = content, 1 = thoroughly displeased) is a weighted sum of four factors, each saturating at 1:

| Factor | Weight | Measure | At its maximum when |
|---|---|---|---|
| Overdue chores | 40% | mean, over the recurring chores, of `(days − frequency) / frequency`; days count from the last completion, never earlier than the start of the game | a chore is late by twice its recommended frequency |
| Refused garments | 30% | `GameState.OutfitRefusals` (see `wardrobe.md`) | 20 garments |
| Pending punishments | 20% | number of pending punishments, residual pieces included | 10 punishments |
| Debt still owed | 10% | `CurrentDebt / InitialDebt`, ramping from one third to the whole | the whole debt is still owed |

**Coefficient** = `1 + mood × 2 × U(0.5, 1)`: drawn between halfway and the ceiling the mood allows, so poor conduct is
always felt but by an unpredictable amount. With a mood of 0 — less than a third of the debt left, nothing pending,
no chore overdue, no outfit refused or modified — it is exactly 1.

| Situation | Ceiling | Drawn between |
|---|---|---|
| Start of the game, perfect conduct | ×1.20 | 1.10–1.20 |
| Mid-game: 60% owed, 2 pending, 1 chore in 5 late by twice its frequency | ×1.32 | 1.16–1.32 |
| Back after a week away (chores at 80%, 80% owed, nothing pending) | ×1.78 | 1.39–1.78 |
| Worst case | ×3.00 | 2.00–3.00 |
| Endgame, nothing to reproach | ×1.00 | 1.00 |

- The coefficient is drawn once per assignment, before the residual-severity draw below, and the result is rounded to
  the nearest point.
- It applies to every infraction (check-ins, chores, permission overruns, a failed Cornertime's escalation). It is
  **not** applied again to the fine for failing or abandoning a punishment — the stored severity already includes
  it — nor to Fortune Deck aggravation.
- Days with the app closed count: chores fall behind while the player is away.

### Residual severity

Cornertime, Writing Lines and Forced Outfit each have a configured maximum (the Fine has none). Severity that a single
punishment cannot hold is never thrown away — it is spent on further punishments until none is left, so one infraction
may produce two, three or more punishments.

- **On assignment:** the punishment is sized at its maximum and absorbs only the points that fit. The residual is
  drawn again among all enabled kinds (the same kind may come up again), and so on. The Fine absorbs whatever it is
  given, so the draw always ends. Every punishment produced carries the same reason.
- **On aggravation** (Fortune Deck): the target is brought to its maximum and the excess becomes a new pending
  punishment of the **same kind** and configuration (same sentences and options for Writing Lines, same preset for
  Forced Outfit, which queues behind the existing one).
- **Residual below the minimum:** a residual too small to reach the minimum of the kind it lands on is charged as a
  Fine instead of being raised to that minimum. (The first punishment of an assignment is still raised to the minimum,
  as before.)
- Each punishment stores only its own share of the severity, which is what the fine for failing or abandoning it is
  based on. Softening and aggravating scale that share along with the punishment.

Not affected: penalty lines added *while writing* (mistakes, idling) still stop at the Writing Lines maximum.

### Popup lateness

The check-in popup shows no countdown and never closes itself on timeout — it stays open until the player
responds. If the player responds later than `PopupTimeoutSeconds`, the resulting severity is
`secondsLate × PopupLateSeverityPerSecond` (0.5 by default), rounded to the nearest point. A delay that rounds to
0 (one second) is neither punished nor remarked on, and no infraction of severity 0 ever produces a punishment.

For Impossible Question and Motivational Quote — the two popups with their own success/failure outcome —
lateness is combined with the outcome instead of being punished separately:
- Impossible Question: on a negative outcome, severity is `secondsLate + random(3, 5)`. On a positive
  outcome, only the lateness severity applies.
- Motivational Quote: reading the phrase (spoken or typed) allows 3 attempts, independent of how long each
  takes. On success, only the lateness severity applies (with a dedicated line acknowledging the delay). If
  all 3 attempts fail, severity is `secondsLate + 5`.