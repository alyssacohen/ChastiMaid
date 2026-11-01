# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-11-01

First public release. Its known issues are listed in the [README](README.md#known-issues).

### Added

#### Games and saves
- Full-screen main menu with Continue, New Game, Load Game, Settings and Exit.
- New games start from a contract that lists every term: debt range, interest, wages, chores, wardrobe,
  punishments and permissions. You can accept the terms, edit them in Settings, or load another
  contract file.
- The initial debt is drawn at random within the configured range.
- Up to 5 saved games. Each slot card shows the debt (or credit) and the safe status. You can view the
  contract a save was started with, or delete it after a warning.
- Each game keeps its own copy of the terms, so later changes in Settings do not affect games already
  in progress.
- Paying past zero leaves a credit that can be spent.
- Daily interest applies at most once per calendar day, and only while the safe is open.
- The game can be completed once the debt is cleared and no punishment is pending (served, or cancelled
  with a card).
- Game completion screen: export the outfit video and the safe combination photo, then free the slot.

#### Sessions
- Daily outfit proposed from your wardrobe, honouring layers, slots, weights and tag rules (requires /
  incompatible).
  - Regenerating halves the earning bonus; after two regenerations the outfit is locked.
  - Composing the outfit by hand cuts the bonus to a fifth of its current value.
  - The outfit stays the same for the rest of the day.
- Garments you refuse become more likely to be drawn. Items refused 5 times are imposed.
- Outfit video recorded when the outfit is confirmed.
- Safe lock: photograph the combination of a real safe without looking. The photo is stored encrypted
  and shown only when you end a session (which opens the safe) or finish the game.
- Ending a session can reveal the safe combination, then closes the app.
- Photos and videos taken during play are kept in the save's own folder. Only the safe combination
  photo is encrypted.
- The system and the display are kept awake while the app runs.

#### Dashboard
- Dashboard with the debt, tasks, current outfit, pay rate and time to the next wage.
- An idle wage is paid at a regular interval, multiplied by the outfit bonus.
- Check-in popups at random intervals, announced by a bell and a spoken summons that uses your name or
  forms of address:
  - Impossible Question;
  - Hydration;
  - Pose for Camera;
  - Self-Check (judge whether a garment is visible in an earlier pose photo);
  - Curtsy video;
  - Motivational Quote read aloud;
  - Kneel.

  Late answers are punished in proportion to the delay.
- Chores in busy mode with a timer and silent photo/video captures. Each chore is judged against a
  hidden target time:
  - too early: no pay and a punishment;
  - too slow: half pay and a punishment.
- Occasional one-off tasks added from the dashboard, paid by the minute.
- Permission requests for five kinds of break, each with its own grant chance, duration and cooldown.
  Insisting during a cooldown is refused and restarts it, and overrunning a granted break is punished.
- Shop selling cards that:
  - bypass a cooldown, lengthen a break, raise the grant chance or guarantee a grant;
  - soften or cancel a pending punishment.
  Purchases are added to the debt.
- Fortune Deck draw. It can win cards or money, lose money, place hidden
  bonuses or penalties on future permission requests, soften, cancel or aggravate a punishment, or do
  nothing. Its cards come from a shared, limited stock with the Shop.

#### Punishments
- Fine, added to the debt immediately.
- Cornertime with camera motion detection and optional voice narration. Movements extend the time.
- Writing Lines: a typing drill with penalty lines for mistakes, a live accuracy and speed display, and
  optional No-Idle and Interruptions difficulty.
- Forced Outfit: named presets that impose garments (or a whole outfit), for a certain time.
- Severity grows with how displeased the employer is, based on overdue chores, refused garments,
  pending punishments and remaining debt.
- Severity beyond a punishment's maximum becomes further punishments.
- Failing or abandoning a punishment applies a fine and keeps it pending.

#### Optional features
- Chastity Device: items marked in Settings are added to every outfit. One is drawn at random and stays
  the same until the safe is opened at the end of a session. It cannot be removed when composing an
  outfit by hand.
- Plug & Play: accessories requested by a popup and removed after a random time. They carry over to
  the next session.
- Diaper Play: while an item marked as diaper is worn, toilet requests are refused and hydration reminders come
  twice as often.

#### Settings
- Settings tabs: General, Chores, Popups, Punishments, Wardrobe and Permissions.
  - Each tab shows a mark when it has unsaved changes. Ctrl+S saves.
  - Linked minimum/maximum fields keep each other consistent.
- Wardrobe editor:
  - item table with search, filters, multi-selection and bulk editing;
  - slot and tag management with undo;
  - tag-based outfit rules with a test preview.
- Contract export and import (`*.contract.json`). The file covers every game setting and all content,
  but never your name, language, camera or auto-save interval.
- Camera selection in Settings and in every capture screen, for devices with more than one camera.
- Webcam and speech-recognition status, plus a speech-recognition test.

#### Speech, language and devices
- Offline speech recognition (Whisper) and offline voice (Piper).
  - Without a microphone or speech model, quotes are typed instead.
  - Without a webcam, camera check-ins are skipped.
  - A notice at session start lists what is missing.
- English and Italian interface and starting content. The language is detected from Windows and can be
  changed in Settings, optionally replacing the content with that language's defaults.
- Touch-friendly layout (44 px minimum targets) for tablets such as the Surface Go, in landscape.
- `--landscape` start-up option that opens a 1200×800 window.
- Release layout with a launcher (`ChastiMaid.exe`) that keeps all data in `data/` next to it.

[Unreleased]: https://github.com/alyssacohen/ChastiMaid/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/alyssacohen/ChastiMaid/releases/tag/v0.1.0
