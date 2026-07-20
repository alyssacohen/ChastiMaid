# Typing Punishment — Live typing view

## Context

The punishment already has, on the ViewModel side:
- a system that proposes the phrase(s) to type;
- a system that tracks keystrokes and checks them against the proposed phrase.

**Missing:** the visual layer that shows this to the player, and the "clear on
mistake" interaction rule. This spec covers both. Inspect the existing
ViewModel before writing the View, and reuse whatever properties/commands
already exist rather than inventing parallel ones — if a property needed
below doesn't exist yet, add it to the ViewModel following the existing
naming conventions in that file, don't guess a name and hope.

Mockup attached: `mockup.html` (open in a browser). It uses the project's
actual light theme colors, not the dark placeholder aesthetic that inspired
the initial idea — ignore any dark-themed reference images if you encounter
them elsewhere.

## Layout (top to bottom)

1. **Target phrase** — the phrase to type, displayed prominently, static.
2. **Typing field** — shows what the player has typed so far, re-rendered
   character by character:
   - characters that match the target phrase so far → success color
   - the first mismatched character (if any) → error color, visually marked
     as the error (see mockup: underline)
   - remaining, not-yet-typed characters of the target phrase → muted/faded,
     so the player can still see what's coming
3. **Previous lines** — a running history panel below the typing field. Every
   time a line is completed (correctly finished, or abandoned after a
   mistake), append a read-only entry:
   - fully correct line → rendered entirely in the success color
   - line that was cleared after a mistake → rendered up to and including the
     mistyped character, with that last character emphasized in the error
     color (this is a record of the attempt, not a live field — it doesn't
     need per-character logic beyond "where did it go wrong")
4. **Live Performance stats** — Progress, CPM, Accuracy, Errors, Time. This
   part is already implemented per the existing screenshot/component; reuse
   it as-is, just confirm it sits below the history panel.

## Interaction rule: clear on mistake

The moment a keystroke doesn't match the expected character, the typing
field must reset to empty immediately — not on next keystroke, not after a
delay. The player restarts the current line from scratch. Before clearing,
push the failed attempt into the history panel (see point 3 above) so the
mistake stays visible to the player as a record, even though the live field
is now empty.

This is a behavior change to the existing keystroke-tracking logic, not
just a View concern — check where that logic currently lives and extend it
there, keeping the View a passive renderer of ViewModel state as usual in
this codebase.

## Typography

Use a new typography class dedicated to this feature: large, monospace,
generous letter-spacing, evoking an old typewriter — distinct from the
`Inter`/`Cormorant Garamond` pair used everywhere else in the app.

- Recommended font: **Special Elite** (Google Fonts, OFL license). It's a
  deliberately distressed, worn-typewriter monospace — irregular letterforms,
  uneven "ink" look — chosen over a cleaner vintage monospace specifically
  for that consumed/aged quality. Confirmed against a reference image: this
  is the intended aesthetic, not a placeholder choice.
  - Fallback if legibility at a glance (correct/error scanning) turns out to
    be a problem in practice: **Cutive Mono** (cleaner vintage monospace) or
    **Courier Prime** (conventional typewriter monospace), same license,
    less character but easier to scan.
- This is a deliberate exception to the "two font families only" rule in the
  design system reference — flagging it explicitly rather than adding it
  silently. If you'd rather keep the two-family rule airtight, ask Alyssa
  before proceeding instead of just picking one.
- Bundle the font file(s) as a project asset (same approach as the existing
  Inter integration) rather than relying on the system having it installed,
  to keep the Surface Go build self-contained and offline.
- Suggested size: ~24px for the target phrase and typing field (generous,
  per the mockup), ~15px for history entries.

## Color tokens

Add to `Colors.axaml`:

| Key                  | Hex       | Use                                               |
|----------------------|-----------|---------------------------------------------------|
| `BrushTypingCorrect` | `#5B7355` | Correctly typed characters, correct history lines |

Reuse the existing `BrushButtonDanger` (`#8B3A2A`) for the error character/
line — no new red token needed.         

## Surface Go note

This screen requires the on-screen keyboard on the Surface Go in tablet
mode. The typing field is a text input that needs focus to trigger TABTIP —
this is the same virtual-keyboard-on-touch-focus issue already tracked for
the Settings section; if `VirtualKeyboardHelper` exists by the time this is
implemented, wire this field through it too instead of solving the problem
twice.

## Suggested phases

1. Add the font asset, the new typography class, and the `BrushTypingCorrect`
   token — verify build, no logic yet.
2. Build the View (target phrase, typing field, stats) bound to the existing
   ViewModel state, static/no history yet.
3. Implement the clear-on-mistake behavior in the ViewModel logic.
4. Implement the history panel: append-on-line-complete (success or cleared
   attempt), bind the View to it.

Build/verify between phases, as usual.
