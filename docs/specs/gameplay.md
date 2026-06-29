# ChastiMaid - Gameplay

## Games and Sessions

The game is structured in "Games" and "Sessions".
A game start with an initial virtual debt generated at random within a range and end when the debt is extinct
and there are no pending punishments.

When the app is started, the user can choose if she wants to start a new game or continue an existent one.
The period of time spent from when the player is ready until the app is closed is a session.

During this time, the user can choose to complete chores and punishments to earn virtual credits to be
able to beat the game.
To motivate the user, she has to put a reward item in a safe with a combination lock, the picture of the
combination is stored inside the app (encrypted), and when she wants to have the item back she has to
complete the game, or ask at the end of a session to get the combination.
In this case, the app will decrypt the picture and show it to the user, but daily interest will be added to the debt.

## Application flow

1. Launch the app
2. Choose what to do:
    1. Continue game
    2. Load game
    3. New game
    4. Settings
    5. Exit

2.1 Continue game
Active if there is a started but not finished game. It continue the last played session.
If selected, the app load last played session data, calculates remaining debt + interests, generates and proposes an
outfit for the session (it change every day but not within the same day).
The user can accept or change it, then when the user is ready, the session starts.

2.2 New game
Shows the current game settings (debt range and daily interest rate).
The initial debt is randomly generated between the configured minimum and maximum.
The user can start the game with these values, or go to Settings to adjust them first.
When confirmed, the session starts like in 2.1, with proposed outfit etc.

2.3 Load Game
Show a list of up to 5 saved games to load and continue.
Here the user can delete unwanted saves with warning and confirm request, because the progresses and the encrypted
picture with the safe combination could be lost forever.

2.4 Settings
Here the user can change the game settings:
- Clothing items with rules
- Chores list with name, recommended frequency, value and completion time
- Game balance: min/max initial debt and daily interest rate
- Other settings (to be added)

2.5 Exit
Exit the app

## Session flow

To ensure full focus during the session, the user must deposit a distracting object (e.g., smartphone or game
controller) into a physical safe.
If there is not already an object locked in the physical safe, the user can deposit the object now.

### Safe lock deposit

* Instructions
  The app instructs the user to place the object inside the safe.
  The user must photograph the safe's combination without looking at it.
* System Behavior
  The photo is encrypted and stored in the session file.
  The combination will only be revealed at the end of the game or session (if the user chooses to unlock the safe).
  Once confirmed, the app transitions to the Dashboard View, in Idle Mode.

### Dashboard view, idle mode

The app will show a dashboard with the following elements:

* Header
    - Remaining debt
* Left Column – Task List
  Each task is displayed as a collapsible card:
    - Task name
    - Task value (virtual currency)
    - Start Task button
    - When expanded:
    - Last completion time (color‑coded: green/yellow/red)
    - Suggested frequency
    - Time since last completion
* Right Column – Outfit Display
  A simplified placeholder showing:
    - Layers of the uniform
    - Garments currently worn
    - Multiplier applied
* Footer
    - End session button

    * Status bar
        - Current pay rate (including outfit multiplier)
        - Time until next idle wage payout

In this mode, the user is "available", the app checks if she is there by opening every 3 to 10 minutes (at random)
a popup with a bell sound asking to do one of the following things:

1. To pose for the camera (will take a picture)
2. To record a video of a curtsy
3. To read a motivational quote (will use text-to-speech to verify that is read aloud)
4. To keel in front of the camera for a random time between 1 and 5 minutes while taking pictures at random intervals
5. To answer an "impossible question"

If the user don't confirm the popup in time (15 seconds from the ringing bell), the app will assign a punishment.
Every 30 minutes in "idle mode", without missing a popup, the app will reduce the debt by a small amount.

When in "idle mode", the user can choose to start a chore from the list to earn some credits.
A popup will ask the user to position the camera to frame the working area and then confirm, a timer will then start to
track how long the chore takes.

### Busy mode (chore)

The app will show a page with the following elements:

* Header
    - Chore name
* Subheader
    - Chore description
    - Chore value (virtual currency)
* Center of page
    - Time elapsed (a big timer)
* Footer
    - End chore button

In this mode, the user is "busy", the app checks if she is working by silently taking pictures or short videos (3 to 10
seconds) at random intervals between a min and a max configured in the settings.
When the chore is finished, the user can click the button and the value will be subtracted from the debt.

### Busy mode (cornertime)
