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
own severity value (sessions per severity point) and its own "full outfit" flag: when checked, the outfit generator
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
awaiting its turn rather than with a session countdown, since its countdown has not started yet.

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

### Popup lateness

The check-in popup shows no countdown and never closes itself on timeout — it stays open until the player
responds. If the player responds later than `PopupTimeoutSeconds`, the resulting severity is
`secondsLate × PopupLateSeverityPerSecond` (0.5 by default).

For Impossible Question and Motivational Quote — the two popups with their own success/failure outcome —
lateness is combined with the outcome instead of being punished separately:
- Impossible Question: on a negative outcome, severity is `secondsLate + random(3, 5)`. On a positive
  outcome, only the lateness severity applies.
- Motivational Quote: reading the phrase (spoken or typed) allows 3 attempts, independent of how long each
  takes. On success, only the lateness severity applies (with a dedicated line acknowledging the delay). If
  all 3 attempts fail, severity is `secondsLate + 5`.