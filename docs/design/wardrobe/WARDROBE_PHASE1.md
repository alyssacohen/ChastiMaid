# Task: Wardrobe Table Redesign — Phase 1 of 3

# Extend WardrobeEditViewModel

## Context

Read `docs/design/wardrobe/WARDROBE_TABLE_SPEC.md` before starting.
Read `docs/design/wardrobe/wardrobe-settings.svg` as visual reference.

This is Phase 1 of a 3-phase refactor. Do NOT touch any View or AXAML file
yet. This phase is ViewModel-only.

---

## What to do

### 1. Modify `WardrobeEditViewModel`

Open the existing file. Add the properties listed below. Do not remove or
rename any existing property or command.

**New properties:**

```
bool IsSelected    — [Reactive], default false
bool IsExpanded    — [Reactive], default false
bool IsEvenRow     — { get; init; }, set once by SettingsViewModel on creation
ReactiveCommand<Unit, Unit> ToggleExpandedCommand
IReadOnlyList<string> ActiveLayers   — derived, reactive
IReadOnlyList<string> SlotList       — derived, reactive
```

**ActiveLayers** must update automatically whenever any of the five
`LayerX` booleans changes. Use `WhenAnyValue` on all five properties,
`.Select(...)` to build the list, and `.ToProperty(this, x => x.ActiveLayers)`.

**SlotList** must update whenever `Slots` changes. Split on `','`,
trim whitespace, remove empty entries.

**ToggleExpandedCommand** implementation:
`IsExpanded = !IsExpanded;`

---

### 2. Modify `SettingsViewModel`

Add the properties and commands listed below. Do not remove or rename
anything existing.

**New filter state:**

```
string WardrobeNameFilter    — [Reactive], default string.Empty
string? WardrobeLayerFilter  — [Reactive], default null  (null = All)
```

**FilteredWardrobeItems:**
Use DynamicData to build a filtered observable collection derived from
`WardrobeItems`. The filter function:

- name match: `WardrobeNameFilter` is empty, OR `item.Name` contains it
  (OrdinalIgnoreCase)
- layer match: `WardrobeLayerFilter` is null, OR `item.ActiveLayers`
  contains it (OrdinalIgnoreCase)

Bind the result to a `ReadOnlyObservableCollection<WardrobeEditViewModel>`
field, exposed as a public property `FilteredWardrobeItems`.

**WardrobeSelectedCount:**

```csharp
public IObservable<int> WardrobeSelectedCount { get; }
```

Derived via DynamicData AutoRefresh on `IsSelected`, `.ToCollection()`,
`.Select(items => items.Count(x => x.IsSelected))`.

**DeleteSelectedWardrobeItemsCommand:**
Enabled when `WardrobeSelectedCount > 0`.
Implementation: remove all items where `IsSelected == true` from
`WardrobeItems`, then set `IsWardrobeDirty = true`.

**Update AddWardrobeItemCommand:**
After adding a new item to `WardrobeItems`, set `IsEvenRow` on each item
by index (index % 2 == 0). Do this for the full list on every add and
on every delete, to keep parity correct.

---

## Acceptance criteria

- Project builds with no errors and no new warnings
- Existing Wardrobe tab still renders (cards layout unchanged for now)
- All new ViewModel properties are observable / reactive
- Unit-testable: no View dependencies in ViewModel

