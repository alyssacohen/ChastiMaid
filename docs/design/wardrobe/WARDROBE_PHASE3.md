# Task: Wardrobe Table Redesign — Phase 3 of 3

# Wire everything into SettingsView + style polish

## Prerequisites

Phases 1 and 2 must be complete and the project must build cleanly.

## Context

Read `docs/design/wardrobe/WARDROBE_TABLE_SPEC.md` (full spec).
Read `docs/design/wardrobe/wardrobe-settings.svg` (visual reference).

---

## 1. Replace Wardrobe TabItem content in SettingsView.axaml

Locate the comment `<!-- ==================== WARDROBE ====================  -->`.

Replace the entire content of that `TabItem` (everything between the
`<TabItem>` tags, except the header) with:

```xml
<Grid RowDefinitions="Auto,*,Auto" Margin="16,8,16,8">
    <views:WardrobeToolbar Grid.Row="0" Margin="0,0,0,8" />
    <ScrollViewer Grid.Row="1" Name="WardrobeScroll">
        <views:WardrobeTable />
    </ScrollViewer>
    <views:WardrobeBulkBar Grid.Row="2" Margin="0,8,0,0" />
</Grid>
```

The `TabItem.Header` with `DirtyTabHeader` must be preserved unchanged.

---

## 2. App.axaml — add any missing tokens

Check whether these brush/style keys exist. Add only what is missing:

| Key                     | Value                              |
|-------------------------|------------------------------------|
| `ChipBackgroundBrush`   | `#DDD8CE` (warm grey chip fill)    |
| `ChipTextBrush`         | `#4A4240`                          |
| `DangerBackgroundBrush` | `#F5D8D0`                          |
| `DangerBorderBrush`     | `#C84820`                          |
| `DangerTextBrush`       | `#8A2010`                          |
| `AccentSubtleBrush`     | `#F0EAE0` (expanded row warm tint) |

Add a global `.danger` button style to App.axaml following the same
template-level pattern used by `.delete` and `.primary`. It should apply
`DangerBackgroundBrush` background, `DangerTextBrush` foreground,
`DangerBorderBrush` border at 1px.

Add a global `.chip` toggle button style if not already present:

- Normal: `ChipBackgroundBrush` bg, `ChipTextBrush` fg, rounded (radius 9)
- `:checked` / active state: `PrimaryTextBrush` bg, `PageBackgroundBrush` fg
- All state overrides must be at template level (inside `ControlTemplate`),
  not via property triggers on the `ToggleButton` element directly.

---

## 3. Verify scroll behavior

Open the app, navigate to Settings › Wardrobe.
Confirm:

- The toolbar stays fixed above the scroll area
- The bulk bar stays fixed below the scroll area
- Only the table rows scroll
- Expanding a row pushes subsequent rows down (no clipping)
- The new-item row scrolls with the list and stays visible at the bottom

---

## 4. Touch target audit

In `WardrobeTable.axaml` and `WardrobeToolbar.axaml`, verify every
interactive element has `MinHeight="44"`. Add it explicitly where missing:

- Row checkboxes
- Expand chevron buttons
- Filter chips
- Add item button
- New-item row inputs and Add button

---

## 5. Clean up

- Remove any TODO comments left from Phases 1–2 that are now implemented
- Confirm no hardcoded color values exist in any of the three new UserControls
- Confirm `TypographyService.cs` is not referenced in any new file
  (it is dead code scheduled for removal)

---

## Acceptance criteria

- Project builds with no warnings
- App runs; navigate to Settings › Wardrobe
- All 21 seed items render in the table
- Filtering by layer name hides non-matching rows
- Typing in the name filter hides non-matching rows
- Expanding a row reveals Multiplier, Weight, Tags, Delete button
- Checking items and pressing "Delete selected" removes them and marks dirty
- Adding via the new-item row adds an item and clears the fields
- Save button persists changes (existing behavior, must still work)
- No colors are defined in view files; all come from App.axaml tokens

