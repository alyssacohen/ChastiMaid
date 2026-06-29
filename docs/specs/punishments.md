# Punishment system

Punishments are used to promote proper behavior and discourage laziness or attempts to cheat.

## How to get a punishment?

Punishments are assigned when the user is late on calls when idle, when he is too late or too early
on finishing a chore, when there is a negative outcome on impossibile questions, or when he refuses to
wear a clothing item too many times.

## How to remove a punishment?

When a user is assigned a punishment it will go on a list of pending punishments except for the Fine
which takes effect immediately. When the user has time to dedicate to a punishment, he can select and
execute it.
If he manages to complete it, it is archived and removed.

## Punishment types

1. Fine/Contravvention
   This is the simplest punishment type, it just add an amount of money to the current debt extending the
   duration of the game.

2. Cornertime
   The app asks the user to stand in a corner and not move for a period of min/max time and check using the
   camera that he don't move. If the user moves, it will add some time and keep checking, if the time goes
   over the max because the user can't stand still, the punishment fails, a fine will be applied and the user
   can retry it from the start at another time.

3. Writing lines
   The user must type correctly one ore more sentences for a certain number of repetitions.
   (i.e. one sentence 30 times or a series of 5 sentences 10 times for a total of 50 lines)
   The app will show the line to write at the top and check each charachter typed by te user. In case of a
   mistype, it will force the user to restart typing that line and eventually add one or more penalty lines
   for the mistake.
   If the user notices that the task is becoming too tedious and taking up too much time, they can stop it,
   but in this case the punishment fails and a fine will be applied. The user can then try to do it again
   from the beginning at another time.

4. Forced outfit
   There will be some clothing items that are pretty unconfortable to wear.
   The idea is to create one or more complete outfits (no other clothing items will be added by the generator)
   and set them as mandatory for one or more subsequent sessions. The user will only have the option to accept
   it or close the application so that until the penalty is served it will not be possible to continue with
   the game.

5. Unwanted clothings
   The app should keep track of how many times each item has been rejected. The count can decrease by 1 if the
   item is included in a subsequent outfit and accepted, but it can never be negative. If the count reaches 5,
   a non-editable outfit is generated and locked for the next session (maybe 2 or 3 sessions?).

Maybe other types will be added.

## Punishment severity

Each infraction has a certain seriousness, so the consequent punishment must have a certain severity.
For each type of punishment I will choose a SeverityValue so when a punishment is assigned, one or more
parameters like minutes, repetitions or number of sessions are adjusted in proportion to the severity of the
punishment.
