# Task: add vertically stacked spinner buttons to NumericUpDown

## Context

This project already has a set of additive, class-scoped styles for `NumericUpDown`
(class `underline`) that render it as an underline-style input with icon-only spinner
buttons. Locate the file containing the `NumericUpDown.underline` selectors. Those
styles MUST keep working exactly as they do now.

By default the Fluent theme lays the spinner's increase/decrease buttons side by side,
inside a horizontal `StackPanel` named `PART_SpinnerPanel` in the `ButtonSpinner`
control template. `NumericUpDown` hosts that `ButtonSpinner` internally.

## Goal

Allow the spinner buttons to be displayed stacked vertically (increase chevron on top,
decrease chevron below) instead of side by side — without modifying or breaking any
existing `underline` behavior, and without overriding any `ControlTemplate`.

## Approach — additive, scoped styles only

1. Introduce a new, composable class `stacked-spinner` so it can be combined with the
   existing class (e.g. `Classes="underline stacked-spinner"`) and reused on its own.
2. Add a style scoped to that class that flips `PART_SpinnerPanel`'s `Orientation` to
   `Vertical`. Use the same descendant-selector pattern already proven to resolve across
   template boundaries in this file (cf. `NumericUpDown.underline RepeatButton`).
   Recommended selector:

       <Style Selector="NumericUpDown.stacked-spinner StackPanel#PART_SpinnerPanel">
           <Setter Property="Orientation" Value="Vertical"/>
       </Style>

   Before relying on this, confirm against the installed Avalonia Fluent theme that the
   part is still named `PART_SpinnerPanel` and is a `StackPanel`; adjust the selector if
   the installed version differs.
3. A vertical `StackPanel` does NOT distribute height between its children, so the two
   buttons would otherwise sit top-aligned with empty space below. Center the pair so it
   reads as intentional: set the panel's `VerticalAlignment` to `Center` (optionally a
   small `Spacing`, e.g. 2). Do not force per-button heights.
4. Leave the spinner icons untouched: the default increase button already carries the up
   chevron and the decrease button the down chevron, so the natural stacking order
   (increase first) gives up-over-down, which is correct.

## Constraints

- Additive only: add new `Style` blocks; do not edit or remove the existing `underline`
  styles, and do not replace any `ControlTemplate`.
- Place the new styles in the same file, immediately after the existing
  `NumericUpDown.underline` block, with a header comment matching the surrounding style.
- All comments in English.

## Touch consideration (Surface Go target)

Stacking roughly halves each button's effective height. Keep icons/padding compact but
do not shrink them to the point of unusable tap targets on the touch build. If the
current spinner `RepeatButton` `Padding="4,0"` leaves them too short when stacked, prefer
a small symmetric padding (e.g. `4,2`) over reducing the icon size.

## Verification

- Build the solution; confirm there are no XAML/markup errors.
- Visually verify (via `Design.PreviewWith` in the styles file or a test view):
    - `Classes="underline stacked-spinner"` → two chevrons stacked vertically, up over
      down, centered, not clipped.
    - `Classes="underline"` only → unchanged (buttons still side by side).
    - Spin up/down still increments/decrements correctly.

## Commit

Conventional commit, e.g.:
feat(controls): add stacked-spinner class for vertical NumericUpDown spinners