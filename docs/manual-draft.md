# ChastiMaid — User Manual (draft)

> Factual draft compiled from the source code at commit `d6db8b6`. Tone, structure and the Italian
> version are to be refined separately. Items whose behaviour could not be confirmed, or where code and
> specs disagree, are collected in [Uncertain / needs my decision](#uncertain--needs-my-decision) and are
> **not** described in the main sections.

---

## 1. What the app is

ChastiMaid is a full-screen Windows desktop game. The player takes the role of a maid working off a
randomly generated virtual debt owed to an employer ("Madame"). Chores, attendance check-ins and
punishments move the debt up or down. A game is finished when the debt reaches zero or less and no
punishment is pending.

All amounts are shown in pounds (£). The interface is available in English and Italian.

---

## 2. System requirements

Only what can be verified from the project:

| Item | Requirement | Source |
|---|---|---|
| Operating system | Windows 10 version 1809 (build 17763) or later; Windows 11 | `SupportedOSPlatformVersion` 10.0.17763.0, target `net10.0-windows10.0.26100.0` |
| Architecture | x64 (the speech engine ships only a `win-x64` native runtime) | `org.k2fsa.sherpa.onnx.runtime.win-x64` |
| .NET | .NET 10 SDK to build. The published app in `Bin/` is framework-dependent, so it most likely needs the .NET 10 Desktop Runtime (see uncertain items) | `publish.ps1` |
| Display | Landscape only. Designed for 1200×800 logical pixels and larger | `App.axaml.cs`, commit `d0ec141` |
| Webcam | Optional. Without it, camera-based check-ins are skipped (§9) | `HardwareAvailability` |
| Microphone + speech models | Optional. Without them, read-aloud quotes are typed instead (§9) | `HardwareAvailability`, `SherpaOnnxSttService` |
| Disk space | Speech models: Whisper `base` is about 200 MB (script description); captured photos/videos accumulate per save | `scripts/Download-SpeechModels.ps1` |

Target form factors (from project notes, not test results): Windows 11 desktop with mouse and keyboard;
Surface Go (Windows 10, tablet mode, 1800×1200 at 150% scaling, two cameras).

---

## 3. Installing and starting

### 3.1 Release layout

`publish.ps1` produces this folder (`dist/Root/`):

```
ChastiMaid.exe   launcher (self-contained, single file)
Bin/             the application itself
Content/         seed contracts (en, it) and speech models
```

Starting `ChastiMaid.exe` launches `Bin/ChastiMaid.exe` and tells it to store data next to the launcher,
so user data ends up in `data/` beside `ChastiMaid.exe` (§11). There is no installer: the folder can be
copied anywhere the user can write.

### 3.2 Speech models

Speech models are not included in the source repository. `scripts/Download-SpeechModels.ps1` downloads them
from the k2-fsa/sherpa-onnx GitHub releases into `Content/Speech/`:

- Speech recognition: Whisper `base` (multilingual).
- Voice: Piper `it_IT-paola-medium` (Italian) and `en_US-amy-medium` (English).

Run the script before `publish.ps1` so the models are copied into the release.

### 3.3 Start-up options

- Default: the window opens full-screen.
- `--landscape`: opens a centred 1200×800 window, to simulate the Surface Go on a desktop.

While the app is open, Windows is kept from going to sleep and from turning the display off.

### 3.4 First launch

On first launch the profile is filled from the seed contract for the system language
(`Content/{en|it}/default.contract.json`). The interface language is taken from Windows (Italian if
Windows is Italian, otherwise English) and can be changed in Settings. The seed contains:

| Content | Count |
|---|---|
| Wardrobe items | 12 |
| Outfit rules | 2 |
| Chores | 3 |
| Forced-outfit presets | 1 |
| Plug & Play items / Diaper items / Chastity-device items | 1 each |
| Permission types | 5 |

If the seed file is missing or invalid, a minimal built-in profile is used instead.

---

## 4. Main menu

The main menu shows: **Continue**, **New Game**, **Load Game**, **Settings**, **Exit**.

| Button | Behaviour |
|---|---|
| Continue | Enabled when the most recently played save is not completed. Opens that save at the outfit screen (§6.1). |
| New Game | With 5 saves already, shows "No Free Slots" and stops. If no player name is set, first asks for a **name** and optional **forms of address** (comma-separated), then opens the contract (§5). |
| Load Game | List of saves (§5.3). |
| Settings | Profile settings (§10). |
| Exit | Closes the app. |

---

## 5. Games and saves

### 5.1 New Game — the contract

The New Game screen shows the current profile as a numbered contract (debt range, interest, wages,
check-ins, punishments, wardrobe and so on), plus a fixed clause on variable severity.

- **Accept the terms**: creates a save named `save_YYYYMMDD_HHMM`, rolls the initial debt at random
  between the configured minimum and maximum (rounded to whole pounds), and goes to the outfit screen.
- **Edit the terms**: opens Settings; coming back rebuilds the contract from the edited profile.
- **Load another contract**: imports a contract file, replacing the profile after a confirmation (§11.3).
- **Back**: returns to the main menu.

The new save takes a **snapshot** of the profile (settings, wardrobe, rules, chores, punishments,
permissions). Editing Settings later does not change games already started.

### 5.2 Daily interest and the safe

Each time a save is opened, daily interest is applied **only if the safe is open**. Interest grows any
outstanding debt and reduces any credit (negative debt) towards zero, never past it. While the safe is
locked the balance does not change between sessions.

### 5.3 Load Game

Up to 5 slots, each showing the debt (or "Credit"), safe status (Locked/Open) and buttons:

- **Load**: opens the save at the outfit screen. A load error shows "Load Error" with the reason.
- **Contract**: shows the terms that save was started with (read-only). Escape closes it.
- **Delete**: asks for confirmation. If the safe is locked the warning says the encrypted combination
  photo will be destroyed for good. Deleting removes the save database and its media folder, and counts
  the game as failed in the profile statistics.

---

## 6. Starting a session

### 6.1 Today's outfit

Madame proposes an outfit drawn from the save's wardrobe (§10.5). The screen shows the items by layer
and the **Total Multiplier** (outfit multiplier × bonus).

- **Accept**: keeps the outfit.
- **Regenerate**: after a confirmation, draws a new outfit. The bonus is halved each time, the refused
  items become more likely in future draws, and the refusal is recorded. After two regenerations the
  outfit is locked automatically.
- **Modify**: after a confirmation, lets the player compose the outfit by hand. The bonus is cut to 20%
  of its current value.
- **Quit**: back to the main menu.

The outfit is chosen **once per day**: reopening the game the same day shows the locked outfit with
Accept only.

Regenerate and Modify are not available when:

- a **Forced Outfit** punishment is in effect: its preset items are imposed (the whole outfit if the
  preset is "full outfit", otherwise as seeds the generator completes);
- any item has been refused 5 or more times: those items are imposed as seeds.

A configured **Chastity Device** item is always included. It changes only if the safe was opened at
the end of the previous session. A **Plug & Play** accessory still worn when the previous session ended
is put back into the outfit, and its countdown resumes.

### 6.2 Compose an outfit (Modify)

Items are shown as chips per layer, with states: selected, required (by a rule), unavailable
(incompatible or slot taken) and locked (chastity device).

- **Integrate Outfit**: lets the generator complete the selection.
- **Save outfit**: locks the composed outfit.
- **Clear** and **Back** are also available.

### 6.3 Outfit Confirmed

Madame asks the player to change into the outfit, then press **I am ready, Madame**. A short outfit
video is then recorded with the webcam. If recording is impossible, the step can be skipped.

### 6.4 Safe Lock

Shown only when the safe is not already locked. The player puts a distracting object (for example a
phone) in a physical combination safe and points the camera at the combination **without looking at
it**. The centre of the live preview is pixelated.

- **Start Camera** → capture → the photo is encrypted (AES-256-GCM) and the plain copy deleted.
  **Retry Capture** allows another try; **Start Session** continues.
- **Skip (no safe)**: continues without locking. The game remains playable, but daily interest applies (§5.2).

---

## 7. The dashboard (idle mode)

### 7.1 Layout

- **Debt** widget: remaining debt and progress.
- **Tasks**: the save's chores plus any occasional tasks. Each card shows name, value and **Start**, and
  expands for details.
- **Outfit**: a drawer with the current outfit and multiplier.
- **Status**: idle state, pay rate and time to the next wage.
- Buttons: **Add task**, **Request Permission**, **Pending Punishments** (enabled when there are some),
  **Shop**, **End Session**, and **Complete Game** (visible when the debt is zero or less and no punishment is pending).

### 7.2 Idle wage

Every *wage interval* (default 30 minutes) spent in idle mode, the debt is reduced by *idle wage amount*
(default £5) × the outfit bonus. If a check-in popup ended with a negative outcome since the last payout
(failed Impossible Question, failed quote, wrong Self-Check), that payout is skipped. Lateness alone does
not skip it. Busy mode, punishments and breaks pause the wage clock, which restarts on return to idle.

### 7.3 Check-in popups

At random intervals between *popup minimum* and *popup maximum* (defaults 180–1200 seconds) a desk bell
rings and Madame summons the player by name or by one of the forms of address; the summons is also
spoken when voice models are installed. The popup stays open until answered. Answering after
*popup timeout* (default 15 s) costs 0.5 severity per second late.

One popup is picked at random among those currently possible:

| Popup | What the player does | Possible when | Outcome |
|---|---|---|---|
| Impossible Question | Answers a question with no good answer | always | Negative outcome → punishment (lateness + 3–5) |
| Hydration | Confirms drinking water | enabled, and at least *minimum hydration interval* (default 30 min, halved with Diaper Play) since the last one | — |
| Pose for Camera | Follows a pose instruction; a photo is taken after a countdown | a webcam is present and an outfit (or Plug & Play item) is worn | photo stored for Self-Check |
| Self-Check | Sees an earlier pose photo and answers whether a named garment is visible | pose photos exist that can be checked | Wrong answer → punishment (lateness + 5, or + 7 if the garment was the one requested at capture) |
| Curtsy | Records a curtsy video (default 5 s) | enabled and a webcam is present | — |
| Motivational Quote | Reads a phrase aloud (or types it); three attempts | enabled | All three failed → punishment (lateness + 5) |
| Kneel | Frames the kneeling spot, then kneels for a random time (default 60–300 s) while photos are taken at random intervals (default 5–30 s) | enabled and a webcam is present | — |
| Plug & Play — put on | Puts on one of the Plug & Play accessories until told otherwise | enabled, nothing already worn, no conflict with the outfit, and no full forced outfit in effect | — |
| Plug & Play — remove | Removes the accessory | the accessory's random duration has elapsed | — |

While a chore is running, only Hydration and the two Plug & Play popups can appear.

### 7.4 Chores (busy mode)

1. **Start** on a task → *Position the camera* screen (camera picker if several cameras) → **Confirm**.
2. A timer runs and the camera silently takes photos or short videos at random intervals (defaults:
   every 300–600 s; videos 3–10 s long).
3. **End Chore** judges the time against a hidden target, drawn between 85% and 115% of the chore's
   minimum time:
   - **before the target**: too early. No pay, punishment (severity 5 + minutes outside the window).
   - **after target × 1.35**: too slow. Paid at 50%, with a punishment.
   - **in between**: paid in full (chore value × outfit bonus).

### 7.5 Occasional tasks

**Add task** opens a form with a name (required), an optional description and an estimated time in
minutes. The task belongs to the current save only and goes through the same busy mode, judged with the
estimate as its minimum time.

- **Pay**: twice the idle-wage rate per minute actually spent, × the outfit multiplier.
- **Too slow**: half pay, based on the estimate.
- **Too early**: the task stays in the list.
- **Once paid**: the task is removed.

### 7.6 Permissions (breaks)

**Request Permission** opens a panel with the five break types:

| Permission | Grant chance | Duration | Cooldown |
|---|---|---|---|
| Use the toilet (n.1) | 70% | 5 min | 10 min |
| Use the toilet (n.2) | 50% | 8 min | 10 min |
| Sit down | 20% | 3 min | 20 min |
| Cigarette break | 20% | 5 min | 30 min |
| Tea or coffee break | 60% | 10 min | 30 min |

- **Granted**: a break screen pauses all popups until **End Break**. Overrunning the duration earns a
  punishment, sized like popup lateness.
- **Denied**: the type goes on a cooldown of 75–125% of its base value. Asking again during the cooldown
  is refused automatically and restarts the cooldown.
- **Bonus cards** owned from the Shop can be played in the same panel before asking (§7.8).
- **Diaper Play**: when active and a diaper item is worn, toilet requests are always refused.

### 7.7 Pending punishments

The **Pending Punishments** panel lists punishments with **Start**, and **Soften Punishment** /
**Cancel Punishment** when the matching card is owned (§8).

### 7.8 Shop and Fortune Deck

The **Shop** sells cards. Prices are multiples of the idle wage, and a purchase is always allowed:
the price is added to the debt.

| Card | Effect | Price (× idle wage) | Copies |
|---|---|---|---|
| Cooldown bypass | Clears a permission cooldown | 10 | 6 |
| Duration boost +1 / +3 / +5 min | Longer break | 10 / 30 / 50 | 6 / 5 / 4 |
| Probability boost +5 / +10 / +20 % | Better grant chance (capped at 95%) | 5 / 10 / 20 | 6 / 5 / 4 |
| Grant permission | Guaranteed grant | 110 | 2 |
| Soften punishment | Halves a pending punishment | 50 | 3 |
| Cancel punishment | Removes a pending punishment | 120 | 2 |

Copies are a stock shared with the Fortune Deck. A card shows **None left** when every copy is held,
and a used card returns to the stock.

**Il Ventaglio della Sorte** (Fortune Deck) costs 10 × idle wage per draw, added to the debt. The player
picks one card from a fan of face-down cards. Outcomes include:

- winning a card;
- money won (2×, 5× or 10× the stake) or lost (1× or 2×);
- a hidden bonus or malus on a random permission type, applied at its next request;
- softening, cancelling or aggravating (×1.5) a random pending punishment;
- nothing at all.

The odds are not shown in the app.

### 7.9 End Session

**End Session** asks for confirmation:

- **Close Without Combination**: ends the session and **closes the app**.
- **Show Safe Combination and Close** (only when the safe is locked): decrypts and shows the combination
  photo, marks the safe as open (so daily interest starts applying), then offers **Close Application**.
  The decrypted image is also saved as `safe_combo.png` in the save's media folder.
- **Cancel**: back to the dashboard.

### 7.10 Game complete

When the debt is zero or less and every punishment has been served or cancelled, **Complete Game**
opens *Service Complete*:

- **Export Outfit Video**: saves the save's outfit video with a file dialog.
- **Export Safe Combination Photo**: decrypts the combination photo and saves it with a file dialog.
- **Finish and Free Slot**: counts the game as completed and **deletes the save and all its media**.

---

## 8. Punishments

### 8.1 How they arise

| Cause | Severity |
|---|---|
| Late check-in | 0.5 per second late |
| Wrong Impossible Question / failed quote / wrong Self-Check | see §7.3 |
| Chore ended too early or too slow | 5 + minutes outside the valid window |
| Permission break overrun | like popup lateness |
| Cornertime failed or abandoned | number of movements detected (+3 if abandoned) |

Before a punishment is assigned, its severity is multiplied by a hidden coefficient between 1 and 3 that
depends on overdue chores, refused garments, pending punishments and remaining debt.

The punishment type is drawn among those enabled. The **Fine** is always available and applied at once.
If the severity exceeds a type's maximum, the excess becomes further punishments.

### 8.2 Types

| Type | Size per severity point (default) | Limits (default) | Notes |
|---|---|---|---|
| Fine | £10 | none | Added to the debt immediately; cannot be disabled |
| Cornertime | 30 s of stillness | 60–900 s required; fails 180 s past the initial requirement | See below |
| Writing Lines | 3 lines | 5–150 lines | See below |
| Forced Outfit | hours set per preset | max 30 h | See below |

**Cornertime.** The player frames the corner, then **Begin**.

- The camera checks for movement: a movement is counted when more than 3% of the pixels change.
- Each movement adds 10–30 s to the requirement, with 10–30 s between two penalties.
- Optional voice narration speaks the instructions.
- Reaching the initial requirement + grace time without completing fails the punishment. Failing or **Abandon**ing applies a fine based on the
  severity and an escalation punishment.

**Writing Lines.** The player types a sentence chosen from Madame's phrase bank, repeatedly.

- A mistake restarts the line and adds penalty lines (default 1).
- The screen shows progress, accuracy, characters per minute, errors and time.
- Two optional extras, each drawn at random per punishment:
  - **No Idle**: 5 s without typing adds 2 lines, repeatedly.
  - **Interruptions**: a taunting message every 60–180 s, during which keystrokes are ignored.
- **Set Down the Pen** abandons the punishment, which applies a fine and keeps it pending.

**Forced Outfit.** A preset is drawn among the enabled ones. Its items are imposed at the next outfit
choice (§6.1).

- It is served in real time while the app is open. The first 30 minutes of each continuous stretch do
  not count.
- Several Forced Outfits queue up, one at a time.
- **Start** on a Forced Outfit only shows how long remains.

### 8.3 Cards on punishments

- **Soften**: halves the severity, rounding up.
- **Cancel**: removes the punishment.
- Fines are never pending, so they cannot be targeted.

---

## 9. Optional hardware and what happens without it

At the start of every session the app checks the webcam, the speech-recognition model and the
microphone. If something is missing, a notice "A Word Before We Begin" lists it.

| Missing | Effect |
|---|---|
| Webcam (or the selected camera not found) | Pose for Camera, Curtsy and Kneel popups are never chosen. The outfit video and the safe photo offer a skip / continue-without-safe button. |
| Speech-recognition model | Motivational Quote asks the player to type the phrase instead. |
| Microphone (model present) | Same: the phrase is typed. |
| Voice (TTS) models | Spoken summons, Kneel instructions and Cornertime narration are silently skipped. Sound effects still play. |

Settings → General → *Webcam & Speech Recognition* shows "Available" / "Not available" for both.

### 9.1 Camera selection

- The camera is chosen in Settings → General → **Detected cameras**.
- It can also be chosen in any capture dialog or the *Position the camera* screen when several cameras
  are present.
- The choice is a device setting: it is saved in the profile, not in the save, and is never exported.
- Photos and videos use the camera's full frame at its highest resolution.

### 9.2 Speech recognition test

Settings → Popups → *Quote Reading* has a **Start/Stop** test that listens to a sample phrase and
reports the match. *Quote recognition sensitivity* (default 82%) is the minimum similarity accepted.

---

## 10. Settings

Opened from the main menu or from New Game (**Edit the terms**). Each tab shows a mark when it has
unsaved edits.

- Buttons: **Save**, **Save & Back**, **Back** and **Discard**. Ctrl+S saves.
- Settings edit the **profile**. A game already started keeps the copy taken when it was created; this
  includes name, forms of address and the auto-save interval. Language and camera apply immediately
  everywhere.
- Linked minimum/maximum fields push each other, so the minimum never exceeds the maximum.
- Clearing a numeric field resets it to its minimum.

### 10.1 General

| Setting | Default | Range |
|---|---|---|
| Name | (asked at first New Game) | — |
| Appellatives (forms of address, comma-separated) | empty | max 10 entries, 30 characters each |
| Auto-save (minutes) | 10 | — |
| Min / Max debt (£) | 500 / 1000 | 0–1,000,000 |
| Daily interest rate | 0.03 | 0.005–0.10 |
| Idle wage amount (£) | 5 | 0–100,000 |
| Wage interval (minutes) | 30 | 1–120 |
| Diaper Play | Enabled | items marked in a list |
| Chastity Device | — | items marked in a list; can only change while the safe is open |
| Plug & Play | Enabled | items marked, each with min/max minutes |
| Language | system | English, Italiano |
| Webcam & Speech Recognition | status + Detected cameras | — |
| Contract | Export Contract / Import Contract | §11.3 |

Changing the language asks whether to **Restore** the wardrobe, rules, chores and gameplay settings with
the new language's defaults (your edits to them are lost), or **Keep** them.

### 10.2 Chores

- *Capture*: wait between captures, 300–600 s; video length, 3–10 s.
- Per chore: **Name**, **Value (£)**, **Freq. (Days)**, **Minimum time (minutes)**, **Description**,
  **Delete**.
- **Add Chore**.

### 10.3 Popups

| Setting | Default | Range |
|---|---|---|
| Popup minimum / maximum (seconds) | 180 / 1200 | 10–86,400 |
| Popup timeout (seconds) | 15 | 5–60 |
| Hydration — enabled, minimum interval (min) | on, 30 | 1–1,440 |
| Curtsy — enabled, video duration (s) | on, 5 | 2–30 |
| Quote Reading — enabled, listening window (s), sensitivity (%) | on, 45, 82 | 5–120, 1–100 |
| Kneel — enabled, duration min/max (s), photo interval min/max (s) | on, 60/300, 5/30 | 1–86,400, 1–3,600 |

### 10.4 Punishments

Fine (£ per severity, default 10). Cornertime, Writing Lines and Forced Outfit each have an
Enabled toggle and the parameters listed in §8.2.

Forced Outfit presets:

- The list offers **Add Outfit**, **Edit preset** and **Delete preset**, plus an Enabled toggle per preset.
- The preset editor has **Name**, **Hours per severity**, *Fix the entire outfit, not just the selected
  items*, Enabled and an item picker.

### 10.5 Wardrobe

A master/detail table of items. Each item has:

- **Name**;
- **Layers** (Underwear, Extra, Base, Outerwear, Accessories);
- **Slots** (an item excludes others in the same slot on the same layer);
- **Multiplier** (earning multiplier);
- **Weight** (draw likelihood; 0 = never drawn at random);
- **Tags**.

Table tools: search (by name, layer, slot or tag), filters, multi-selection with bulk edit, and
**Manage slots** / **Manage tags** dialogs with Undo.

**Rules** (button *Edit rules*) are tag-based:

- Relations are *Tag — Requires / Incompatible — Related tag*.
- **Add Rule** and **Delete** manage the list.
- **Test Rules** shows a sample outfit, with **Regenerate**. Escape closes the editor.

Accepting an outfit lowers its items' weight; refusing raises it. Every 10 refused outfits the weights
are rescaled to the wardrobe's original total.

### 10.6 Permissions

Per type: **Grant chance**, **Duration (minutes)**, **Cooldown (minutes)**. Defaults in §7.6.

---

## 11. Data, privacy, import/export

### 11.1 Where data is stored

All under `data/` next to the program (next to the launcher in a release; under `bin/` when run with
`dotnet run`):

| Path | Content | Format |
|---|---|---|
| `data/profile.db` | Profile: settings, wardrobe, rules, chores, presets, permissions, statistics | LiteDB, password-encrypted |
| `data/saves/save_YYYYMMDD_HHMM.db` | One game: state, sessions, punishments, cards, snapshot of the profile | LiteDB, password-encrypted |
| `data/saves/save_YYYYMMDD_HHMM/` | That game's media | see below |

Media folder contents:

| File | Encrypted |
|---|---|
| `safe_combo.enc` — the combination photo | **yes** (AES-256-GCM) |
| `outfit.avi` — outfit video | no |
| busy-mode photos `*.png` and videos `*.avi` | no |
| pose, kneel, curtsy, cornertime evidence `*.png` / `*.avi` | no |
| `safe_combo.png` — written when the combination is revealed (§7.9) | no |

The encryption key is built into the program. It deters casual browsing but does not protect against
someone with the program and the files.

Up to 5 saves exist at once. Deleting a save, or finishing a game, removes its database and media folder.

### 11.2 Auto-save

The game is saved on events and on a timer (default every 10 minutes).

### 11.3 Contract import/export

The **contract** (`*.contract.json`) is the only import/export format. It holds every game setting plus
the wardrobe, outfit rules, chores, punishment presets and permissions.

- **Never included**: player name, language, camera and autosave interval.
- **Export**: Settings → General → **Export Contract** (default name `chastimaid.contract.json`).
- **Import**: Settings → General → **Import Contract**, or New Game → **Load another contract**.
  - Any `.json` file can be chosen; it is validated first, including the numeric ranges listed in §10.
  - After a confirmation it **replaces** the whole profile. Saves are not affected.
- Load Game → **Contract** shows the contract a save was started with.

---

## 12. Touch vs mouse, tablet vs desktop

- All interactive elements are at least 44×44 px for finger use. Numeric fields have wide up/down
  buttons and ask touch keyboards for the number layout.
- **Keyboard shortcuts** (desktop):
  - Ctrl+S saves Settings.
  - Escape closes the outfit-rules editor, the contract view in Load Game, and the popup result
    message on the dashboard.
- No on-screen keyboard is opened by the app itself. Text entry on a tablet (names, occasional tasks,
  Writing Lines, typed quotes) relies on the Windows touch keyboard.
- Landscape only; portrait is not supported.
- On a device with several cameras (Surface Go front/rear), pick the camera as described in §9.1.

---

## Uncertain / needs my decision

### A. Behaviour that differs from specs or project notes

1. ~~**Game completion ignores pending punishments.**~~ Fixed: `CanComplete` now also requires no pending
   punishment (`DashboardViewModel.cs:224`), as `gameplay.md` and `CLAUDE.md` describe.
2. **Captured media is not encrypted.** Only `safe_combo.enc` goes through `CryptoService`. The outfit
   video, busy-mode captures and pose/kneel/curtsy/cornertime files are plain PNG/AVI. `CLAUDE.md` says
   "CryptoService encrypts captured media and the safe-combination photo". The revealed combination is
   also left in clear as `safe_combo.png`. This matters for privacy wording in the manual.
3. **Back from "Compose an outfit" restores the full bonus.** The Modify dialog says there is "no
   changing your mind or going back". `ChangeOutfitViewModel` Back builds a new `StartSessionViewModel`,
   whose bonus starts again at 1.0 (`StartSessionViewModel.cs:18`). This also undoes earlier
   regenerations' halving and their count.
4. ~~**Forced Outfit description in Settings.**~~ Resolved: it now reads "The selected items become
   mandatory for a number of hours of play." (IT: "…per un certo numero di ore di gioco."), matching the
   real-time hours in the code and `punishments.md`. Three code comments that spoke of sessions were
   corrected too.
5. **Popup interval.** `gameplay.md` says every 3 to 10 minutes; the defaults are 180–1200 s (3–20 min).
6. **Settings tabs.** `gameplay.md` lists an "Outfit Rules" tab and no Popups/Permissions tabs. The code
   has General, Chores, Popups, Punishments, Wardrobe and Permissions; rules are an overlay inside
   Wardrobe and are **tag-based**, not item-to-item.
7. **Cornertime failure.** `punishments.md` says a failed Cornertime applies a fine. The code applies the
   fine **and** assigns an escalation punishment (movements + 3 if abandoned) of any enabled type.
8. ~~**Chastity Device** documentation.~~ Resolved: documented as a feature (README, CHANGELOG). It is
   still not described in `gameplay.md`, only in `wardrobe.md` and `new-game-contract.md`.
9. ~~**Whisper variant.**~~ Resolved: `Download-SpeechModels.ps1` no longer takes `-WhisperVariant` and
   always downloads `base`, the only variant the app loads (`SherpaOnnxSttService.cs:41`). It also leaves
   out the unused `*.int8.onnx` files and `test_wavs` (about 150 MB less in the release).

### B. Recorded but not visible / unused

10. **Statistics.** `PlayerStats` (`GamesCompleted`, `GamesFailed`, `TotalSessions`) are stored, but no
    screen shows them, and `TotalSessions` is never incremented.
11. **`GameState.IsCompleted` is never set to true.** The "Completed" label in Load Game and the Continue
    check depend on it, but finishing a game deletes the slot instead. If the player leaves *Service
    Complete* without pressing **Finish and Free Slot**, the save stays as an ordinary, loadable game.

### C. Not verified (need a real-device test)

12. **.NET runtime on the target PC.** The app in `Bin/` is published framework-dependent. Does a release
    require installing the .NET 10 Desktop Runtime, or should `publish.ps1` make it self-contained?
13. **Camera or microphone blocked by Windows privacy settings.** The microphone check only lists audio
    devices (`DeviceInformation.FindAllAsync`). A blocked microphone would probably be reported as present
    and then fail when listening. The camera behaviour when blocked was not traced.
14. **Starting a chore with no webcam.** No special handling was found in `CameraSetupViewModel`.
    Behaviour (blank preview? capture errors?) not verified.
15. **Touch keyboard.** The app relies on Windows to show the touch keyboard in tablet mode; whether it
    pops up for every text field (Writing Lines in particular) on the Surface Go was not verified.
16. **Daily outfit lock uses the UTC date** (`OutfitLockedDate?.Date == DateTime.UtcNow.Date`). In Italy
    "a new day" starts at 01:00 or 02:00 local time, not midnight. Intended?
17. **Name, forms of address and auto-save are frozen per save.** The dashboard reads them from the save's
    snapshot (`DashboardViewModel.cs:109`), so changing them in Settings does not affect a game in
    progress. The contract treats them as device settings (`[JsonIgnore]`). Intended?
18. **Wage rule vs spec.** `gameplay.md` says the wage is paid "without missing a popup". Popups can't be
    missed (they never close on their own); the code skips the wage after a *negative outcome* instead.
19. **Two new games in the same minute** get the same slot name `save_YYYYMMDD_HHMM`. Behaviour not
    tested.

### D. Release / legal

20. **No `LICENSE`.** The project license is undecided.
21. **Bundled fonts without license files:** Cormorant Infant, Special Elite and Lucide (`Assets/Fonts/`).
    Their licenses (typically SIL OFL / ISC) usually require shipping the license text.
22. **Third-party model licenses.** The Piper voices and Whisper model come with their own model cards
    (`MODEL_CARD` is present in the TTS folders). Their terms are not reflected anywhere in the repo.
23. ~~**No version number.**~~ Resolved: `Directory.Build.props` sets `<Version>0.1.0</Version>` for the
    app and the launcher (tracked through a `.gitignore` exception). Not shown in the UI yet. The tag
    `v1-pre-reset` is left as it is.
