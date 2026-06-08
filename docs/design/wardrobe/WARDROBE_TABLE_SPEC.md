# ChastiMaid — Settings › Wardrobe: Table Redesign Spec

## Wireframe reference
`docs/design/wardrobe/wardrobe-settings.svg`

---

## 1. Context & goals

The current Wardrobe tab in `SettingsView` renders one `Border.card` per
`WardrobeEditViewModel` item — fine for a handful of entries, but unwieldy
when the wardrobe grows. The redesign replaces it with a compact, scrollable
table that supports:

- Inline viewing of Name, Layers, and Slots for every item at a glance
- Expandable detail rows for secondary fields (Multiplier, Weight, Tags)
- A persistent "new item" row at the bottom of the table for fast entry
- Layer-based filter chips in a toolbar above the table
- Checkbox-based multi-select with a bulk-delete action bar

The overall `SettingsView` shell (tab structure, Save/Back buttons, dialog
overlay) is preserved. Only the content of the Wardrobe `TabItem` changes,
plus targeted modifications to `WardrobeEditViewModel`.

---

## 2. Visual design tokens (already defined in App.axaml)

Use only existing brush/style keys. Do not hardcode colors in views.

| Role | Key to use |
|---|---|
| Page background | `PageBackgroundBrush` |
| Panel / nav tint | `SurfaceBrush` |
| Card / table background | `CardBackgroundBrush` |
| Header row fill | `SurfaceBrush` |
| Alternating row tint | `CardBackgroundBrush` (odd) / `PageBackgroundBrush` (even) |
| Expanded row fill | `AccentSubtleBrush` (or closest warm tint in tokens) |
| Separator lines | `BorderBrush` |
| Primary text | `PrimaryTextBrush` |
| Hint / muted text | `TextHintBrush` |
| Layer / slot chips | `ChipBackgroundBrush` + `ChipTextBrush` |
| Active chip (filter) | `PrimaryTextBrush` bg + `PageBackgroundBrush` text |
| Delete button | `DangerBackgroundBrush` + `DangerTextBrush` |
| Dashed input borders | `BorderBrush` at 50% opacity |

Typography classes: `.fieldLabel` (column headers), body size for cell text,
`.small` for chip text. All already defined.

---

## 3. Layout structure

```
SettingsView (unchanged shell)
└── TabItem "Wardrobe"
    └── Grid RowDefinitions="Auto,*,Auto"   ← toolbar / table / bulk bar
        ├── Row 0: WardrobeToolbar (new UserControl)
        ├── Row 1: ScrollViewer
        │           └── WardrobeTable (new UserControl)
        └── Row 2: WardrobeBulkBar (new UserControl)
```

Splitting into three UserControls keeps `SettingsView.axaml` readable and
makes each piece independently testable.

---

## 4. WardrobeToolbar

A single horizontal row containing:

- `TextBox` — filter by name (placeholder "Filter by name or tag…")
  Binds to `SettingsViewModel.WardrobeNameFilter`
- Label "Layer:" followed by a `WrapPanel` of toggle chips:
  All · undies · base · extra · outer · gears
  Binds to `SettingsViewModel.WardrobeLayerFilter` (null = All)
- `Button Classes="primary"` — "+ Add item"
  Command: `SettingsViewModel.AddWardrobeItemCommand`
  Scrolls the table to the new-item row after execution.

Touch target: all chips and the Add button `MinHeight="44"`.

---

## 5. WardrobeTable

### 5a. Column header row

Fixed (not scrolled). Columns:

| Col | Width | Content |
|---|---|---|
| Checkbox | 40 | `CheckBox` — select/deselect all visible rows |
| Name | `*` (fills remaining) | "NAME" label |
| Layers | 200 | "LAYERS" label |
| Slots | 180 | "SLOTS" label |
| Expand | 44 | "⋮" (decorative) |

### 5b. Data rows

Each row is a `WardrobeRowView` (DataTemplate or UserControl, Claude Code's
choice based on complexity). It renders in two states:

**Collapsed (default)**
- Checkbox bound to `WardrobeEditViewModel.IsSelected`
- Name text
- Layers: one pill chip per layer value, using `ItemsControl` + chip style
- Slots: same pattern
- Expand chevron `›` button — Command: `ToggleExpandedCommand`

Row height: `MinHeight="44"` (touch target). Alternating background via
index-based trigger or a simple `IsEvenRow` bool on the ViewModel.

**Expanded**
When `WardrobeEditViewModel.IsExpanded` is true, the row renders an
additional inline panel below the main row cells (not a separate row —
keep it attached to avoid z-order issues in `ItemsControl`):

```
[ Multiplier: NumericUpDown ]  [ Weight: NumericUpDown ]
[ Tags: TextBox (comma-separated, shows chips inline if possible) ]
[ Delete item (danger button) ]
[ italic hint: "Multiplier and weight affect outfit scoring. Tags are used by outfit rules." ]
```

Multiplier and Weight: `NumericUpDown`, `FormatString="0.0"`, `Increment="0.1"`.
Tags: `TextBox`, comma-separated string, `PlaceholderText="maid, elegant…"`.

### 5c. New-item row (always visible, pinned at bottom of list)

Visually distinct (dashed borders, `F7F3EC`-equivalent fill).
Fields: Name TextBox · Layers picker · Slots TextBox · ↵ Add button.

Layers picker: a compact inline `WrapPanel` of `CheckBox` items (same five
values as the filter chips). No popup/dialog — keep it flat and touch-friendly.

The Add button is enabled only when Name is non-empty AND at least one layer
is checked AND Slots is non-empty.

On confirm: calls `SettingsViewModel.AddWardrobeItemCommand`, clears the
new-item fields, scrolls to the newly added row.

### 5d. Scrollbar

Standard Avalonia `ScrollViewer` with vertical scrollbar. No horizontal scroll.

---

## 6. WardrobeBulkBar

Always rendered at the bottom of the tab, below the table.
Dark background (`PrimaryTextBrush` fill), light text.

- Left: "{N} items selected" label, bound to
  `SettingsViewModel.WardrobeSelectedCount`
- Right: "Delete selected" `Button Classes="danger"`,
  Command: `SettingsViewModel.DeleteSelectedWardrobeItemsCommand`
  Enabled only when `WardrobeSelectedCount > 0`

When count is 0, the bar is visible but the button is disabled (do not
collapse — collapsing causes layout shift while scrolling).

---

## 7. ViewModel changes

### WardrobeEditViewModel (modify existing)

Add the following properties. Do not remove any existing property.

```csharp
// Selection & expansion state (UI-only, not persisted)
[Reactive] public bool IsSelected { get; set; }
[Reactive] public bool IsExpanded { get; set; }

// Alternating row background helper
public bool IsEvenRow { get; init; }   // set once by SettingsViewModel

// Expand/collapse toggle
public ReactiveCommand<Unit, Unit> ToggleExpandedCommand { get; }
// Implementation: this.RaiseAndSetIfChanged(ref _isExpanded, !_isExpanded)
```

Layer properties already exist as `bool` fields (`LayerUndies`, `LayerBase`,
`LayerExtra`, `LayerOuter`, `LayerGears`). Expose a computed
`IReadOnlyList<string> ActiveLayers` for the chip display:

```csharp
// Derive via WhenAnyValue on all five LayerX booleans
public IReadOnlyList<string> ActiveLayers { get; }
// e.g. ["base", "extra"] — updated reactively
```

Similarly, `Slots` is already a comma-separated `string`. Expose:

```csharp
public IReadOnlyList<string> SlotList { get; }
// Split(", ") — updated reactively when Slots changes
```

### SettingsViewModel (modify existing)

Add the following. Do not remove or rename existing members.

```csharp
// Filter state
[Reactive] public string WardrobeNameFilter { get; set; } = string.Empty;
[Reactive] public string? WardrobeLayerFilter { get; set; }  // null = All

// Filtered view (derive from WardrobeItems + filter properties)
public IObservableList<WardrobeEditViewModel> FilteredWardrobeItems { get; }
// Use DynamicData: WardrobeItems.Connect()
//   .Filter(this.WhenAnyValue(...).Select(BuildFilter))
//   .ObserveOn(RxApp.MainThreadScheduler)
//   .Bind(out _filteredWardrobeItems)

// Selection aggregate
public IObservable<int> WardrobeSelectedCount { get; }
// = WardrobeItems.Connect()
//     .AutoRefresh(x => x.IsSelected)
//     .ToCollection()
//     .Select(items => items.Count(x => x.IsSelected))

// Commands
public ReactiveCommand<Unit, Unit> DeleteSelectedWardrobeItemsCommand { get; }
// Removes all WardrobeItems where IsSelected == true
// Marks IsWardrobeDirty = true
```

`AddWardrobeItemCommand` already exists — reuse it, but update its
implementation to also set `IsEvenRow` on the new item based on its index,
and to clear the new-item row state.

---

## 8. Filter logic

```
BuildFilter(nameFilter, layerFilter):
  item matches if:
    (nameFilter is empty OR item.Name contains nameFilter, case-insensitive)
    AND
    (layerFilter is null OR item.ActiveLayers contains layerFilter)
```

---

## 9. Touch targets (Surface Go)

- All row checkboxes: `MinWidth="44" MinHeight="44"`
- Expand chevron button: `Width="44" Height="44"`, no visible border
- Chip filter buttons: `MinHeight="44"`, `Padding="12,0"`
- New-item Add button: `MinHeight="44"`
- Bulk delete button: `Height="32"` within the 44-tall bar

---

## 10. What must NOT change

- The `SettingsView` tab order: General · Chores · Punishments · Wardrobe · Outfit Rules
- The `DirtyTabHeader` component on the Wardrobe tab header
- The Save / Back button bar at the bottom of `SettingsView`
- The dialog overlay panel at the root of `SettingsView`
- All existing Chores, Punishments, and Outfit Rules tab content
- The `App.axaml` design token principle: no colors defined in views

