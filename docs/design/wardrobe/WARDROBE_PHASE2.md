# Task: Wardrobe Table Redesign — Phase 2 of 3
# New UserControls: WardrobeToolbar, WardrobeTable, WardrobeBulkBar

## Prerequisites
Phase 1 must be complete and the project must build cleanly before
starting this phase.

## Context
Read `docs/design/wardrobe/WARDROBE_TABLE_SPEC.md` (full spec).
Read `docs/design/wardrobe/wardrobe-settings.svg` (visual reference).

This phase creates three new UserControls. Do NOT modify `SettingsView.axaml`
yet — that is Phase 3.

---

## 1. Create `Views/Controls/WardrobeToolbar.axaml`

A single-row horizontal layout. `DataContext` is `SettingsViewModel`.

Contents left to right:
- `TextBox` bound to `WardrobeNameFilter`, placeholder "Filter by name or tag…",
  width 240, `MinHeight="44"`
- Label `TextBlock` "Layer:" with `VerticalAlignment="Center"`, left margin 16
- `WrapPanel` of six toggle items for: All, undies, base, extra, outer, gears.
  Each is a `ToggleButton` with class `.chip`.
  - "All" is active when `WardrobeLayerFilter` is null; clicking it sets
    `WardrobeLayerFilter = null`
  - Each named layer is active when `WardrobeLayerFilter == "layername"`;
    clicking sets `WardrobeLayerFilter = "layername"`
  - Only one can be active at a time (radio behavior via commands, not
    a RadioButton group — ToggleButton + Command is cleaner in MVVM)
  - `MinHeight="44"`, `Padding="12,0"`
- Spacer (`Grid` stretch column or `HorizontalAlignment="Right"` on next item)
- `Button Classes="primary"` with content "+ Add item",
  `Command="{Binding AddWardrobeItemCommand}"`, `MinHeight="44"`

---

## 2. Create `Views/Controls/WardrobeTable.axaml`

`DataContext` is `SettingsViewModel`.

### Header row
`Grid` with five columns matching the spec (40 / * / 200 / 180 / 44).
Fixed above the scroll area (not inside the ScrollViewer).

- Col 0: `CheckBox` — on Checked/Unchecked, set `IsSelected` on all items
  in `FilteredWardrobeItems`. Use code-behind or a converter; do not
  add a SelectAllCommand if it can be avoided cleanly.
- Cols 1–3: `TextBlock` with `.fieldLabel` class, text NAME / LAYERS / SLOTS
- Col 4: `TextBlock` "⋮", centered, muted

Separator `Border` at the bottom of the header.

### Data rows — ItemsControl
`ItemsControl ItemsSource="{Binding FilteredWardrobeItems}"`.

Each item uses a `DataTemplate` (or promote to a separate
`WardrobeRowView.axaml` UserControl if the template exceeds ~60 lines).

**Row structure (two-part):**
Use a `StackPanel` (Orientation=Vertical) per item.

Part A — always visible, `MinHeight="44"`:
```
Grid (same 5-column definition as header)
  [0] CheckBox bound to IsSelected
  [1] TextBlock bound to Name
  [2] ItemsControl bound to ActiveLayers → chips
  [3] ItemsControl bound to SlotList → chips
  [4] Button (chevron ›/⌄) Command=ToggleExpandedCommand
```
Background alternates via `IsEvenRow`:
- true  → `{DynamicResource CardBackgroundBrush}`
- false → `{DynamicResource PageBackgroundBrush}`
Use a `DataTrigger` or a value converter — no code-behind.

Part B — expanded panel, `IsVisible="{Binding IsExpanded}"`:
```
Border (AccentSubtleBrush or nearest warm token, left-indent 40)
  StackPanel Spacing="8"
    Grid ColumnDefinitions="120,120,*"
      [0] StackPanel: fieldLabel "MULTIPLIER" + NumericUpDown Multiplier
      [1] StackPanel: fieldLabel "WEIGHT" + NumericUpDown Weight
      [2] StackPanel: fieldLabel "TAGS" + TextBox Tags
    Button Classes="danger" Content="Delete item"
           Command="{Binding DeleteCommand}" HorizontalAlignment="Right"
    TextBlock (italic, small, muted): "Multiplier and weight affect outfit
               scoring. Tags are used by outfit rules."
```

### New-item row (pinned below ItemsControl, inside same StackPanel or
as a sibling after the ItemsControl)

Visual: dashed `Border` (`BorderBrush` at 50% opacity, `BorderThickness="1"`,
`CornerRadius="2"`), warm tinted background.

Grid (same 5-column definition):
```
[0] (empty — no checkbox)
[1] TextBox bound to SettingsViewModel.NewItemName,
    placeholder "New item name…", MinHeight="44"
[2] WrapPanel of five CheckBox items (undies / base / extra / outer / gears)
    each bound to SettingsViewModel.NewItemLayer{X}
[3] TextBox bound to SettingsViewModel.NewItemSlots,
    placeholder "torso, legs…", MinHeight="44"
[4] Button "↵ Add" Command=AddWardrobeItemCommand
    IsEnabled: Name non-empty AND ≥1 layer checked AND Slots non-empty
    MinHeight="44"
```

Add to `SettingsViewModel` (Phase 1 did not include these — add now):
```
[Reactive] string NewItemName
[Reactive] bool NewItemLayerUndies
[Reactive] bool NewItemLayerBase
[Reactive] bool NewItemLayerExtra
[Reactive] bool NewItemLayerOuter
[Reactive] bool NewItemLayerGears
[Reactive] string NewItemSlots
```
`AddWardrobeItemCommand`: read the `NewItemX` properties, build a new
`WardrobeEditViewModel`, add to `WardrobeItems`, clear all `NewItemX`
properties, set `IsWardrobeDirty = true`.

---

## 3. Create `Views/Controls/WardrobeBulkBar.axaml`

`DataContext` is `SettingsViewModel`.

Dark background strip (`{DynamicResource PrimaryTextBrush}` fill,
or the nearest token — check App.axaml), height 44.

```
Grid ColumnDefinitions="*,Auto" VerticalAlignment="Center" Margin="16,0"
  TextBlock "{Binding WardrobeSelectedCount, StringFormat='{}{0} items selected'}"
            Foreground="{DynamicResource PageBackgroundBrush}"
  Button Classes="danger" Content="Delete selected"
         Command="{Binding DeleteSelectedWardrobeItemsCommand}"
         HorizontalAlignment="Right"
```

`WardrobeSelectedCount` is `IObservable<int>` — bind via
`{Binding WardrobeSelectedCount^}` (observable binding in Avalonia) or
expose a `[Reactive] int WardrobeSelectedCountValue` property updated by
subscribing to the observable in the constructor.

---

## Acceptance criteria

- Project builds with no errors
- Three new UserControls compile and render without runtime exceptions
- `SettingsView` still works unchanged (new controls not wired in yet)
- No hardcoded colors in any new AXAML file

