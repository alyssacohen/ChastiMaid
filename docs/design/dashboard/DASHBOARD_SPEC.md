# ChastiMaid — Dashboard (Idle State): Design Spec

## Wireframe references

| File                      | Describes                          |
|---------------------------|------------------------------------|
| `wireframe-portrait.svg`  | Dashboard in portrait orientation  |
| `wireframe-landscape.svg` | Dashboard in landscape orientation |
| `wireframe-drawer.svg`    | Outfit drawer open (portrait)      |
| `wireframe-dialog.svg`    | Punishment list dialog (portrait)  |

---

## 1. Project context

- **Framework**: .NET 8, Avalonia UI, ReactiveUI, Fluent theme (no SukiUI)
- **Target device**: Surface Go (10", 1800×1200, 3:2 ratio), tablet mode, touch
- **Naming convention**: `FooViewModel` / `FooView` — ViewLocator maps by replacing
  `"ViewModel"` with `"View"`. No `Page` suffix.
- **Project structure**: `ViewModels/`, `Views/`, `Views/Controls/`, `Views/Dialogs/`
- **Typography**: use the existing `DynamicResource` keys — do not hardcode font sizes
  or families.
- **Existing DynamicResource typography keys**:
  `DisplayFontSize`, `TitleFontSize`, `SubtitleFontSize`, `BodyFontSize`, `CaptionFontSize`,
  `DisplayFontFamily` (Instrument Serif), `BodyFontFamily` (Inter)

---

## 2. Design tokens to add to App.axaml

### Color values

| Key                        | Hex       | Usage                                       |
|----------------------------|-----------|---------------------------------------------|
| `ColorPageBackground`      | `#F5F0E8` | Main page background (warm ivory)           |
| `ColorSurface`             | `#EDE8DE` | Status bar, action bar, left rail           |
| `ColorCard`                | `#E8E0D0` | Debt widget card                            |
| `ColorButtonSecondary`     | `#DDD8CE` | Secondary buttons                           |
| `ColorProgressTrack`       | `#D0C8BC` | Debt bar track                              |
| `ColorBorder`              | `#C8C0B0` | Standard dividers and borders               |
| `ColorBorderWarm`          | `#C8B898` | Card and action bar borders                 |
| `ColorTextPrimary`         | `#2C2C2C` | Main text, filled buttons                   |
| `ColorTextSecondary`       | `#5A5550` | Subtitles, section labels                   |
| `ColorTextHint`            | `#9A9288` | Timestamps, estimated durations             |
| `ColorTextMuted`           | `#C0B8AC` | Scroll indicators, canvas labels            |
| `ColorAmber`               | `#C8882A` | Punishment badge and border — warnings only |
| `ColorAmberBackground`     | `#F5E6C8` | Punishment button fill, dialog header       |
| `ColorAmberText`           | `#8A5A10` | Text on amber background                    |
| `ColorStatusIdle`          | `#7CB87C` | Green dot in status bar                     |

Add a corresponding `SolidColorBrush` resource for each colour (e.g. `PageBackgroundBrush`,
`SurfaceBrush`, `AmberBrush`, etc.) following the same naming pattern already in use.

---

## 3. New files to create

```
Views/
  DashboardView.axaml + .axaml.cs
  Controls/
    DebtWidget.axaml + .axaml.cs       (UserControl, reusable)
    OutfitDrawer.axaml + .axaml.cs     (UserControl, overlay)
  Dialogs/
    BaseDialog.axaml + .axaml.cs       (Window, base for all modals)
    PunishmentListDialog.axaml + .axaml.cs

ViewModels/
  DashboardViewModel.cs
  TaskItemViewModel.cs
  PunishmentItemViewModel.cs
  OutfitLayerViewModel.cs
```

---

## 4. DashboardView layout

### Portrait (see `wireframe-portrait.svg`)

Top-to-bottom, single column:

1. **Status bar** — fixed height ~50px, `ColorSurface` background, thin warm bottom border.
   Left: green dot + "Idle" label.
   Right: "Outfit ▸" button (opens the drawer).

2. **Scrollable area** (`ScrollViewer` filling remaining height):
    - `DebtWidget` UserControl (16px side margin, 16px top margin)
    - **Tasks section**: header row ("Tasks" label + count badge) + divider +
      `ItemsControl` of task rows. Each row: name + estimated duration (left),
      "Start" dark button (right). `MinHeight="56"` per row. Thin divider between rows.
    - **Punishment button**: full-width, amber styling (`ColorAmberBackground` fill,
      `ColorAmber` border), count badge (amber circle, white number) on the right.
      Active only when `HasPendingPunishments = true`; disabled and unstyled otherwise.

3. **Action bar** — pinned at bottom, `ColorSurface` background, thin warm top border.
   Three buttons in a row (left to right):
    - Request Permission
    - Shop
    - **End Session** — `Danger` styling (destructive: leads to the safe-combination
      reveal / abandon-game flow).

4. **OutfitDrawer** — renders as the topmost child of the root `Panel`, overlaying
   everything. Hidden when closed. See §6.

### Landscape (see `wireframe-landscape.svg`)

Two-column layout triggered when `Bounds.Width` exceeds a threshold (~900 device px):

- **Left rail** (~260px wide, `ColorSurface` background, right border):
  Top: DebtWidget card.
  Middle: Request Permission, Shop, End Session buttons (stacked).
  Bottom: "Current Outfit ▸" button (replaces the portrait status bar shortcut).

- **Right area** (remaining width, scrollable):
  Tasks header + task rows + Punishment button — same structure as portrait.

- **Status bar** remains full-width at the top in both orientations.

---

## 5. DebtWidget (UserControl)

A read-only display component. Accepts `DebtTotal` and `DebtSettled` as
`StyledProperty<decimal>` bindings. Computes `DebtRemaining` and `DebtProgress`
internally.

### Visual layout (inside a `ColorCard` rounded card, warm border)

```
[Title: "Outstanding Balance" — DisplayFontFamily, italic, CaptionFontSize, TextSecondary]

[Settled              ]   [                   Remaining]
[340 pts              ]   [                     180 pts]
  TitleFontSize, FontWeight=Light, TextPrimary

[████████████░░░░░░░░░]   6px tall bar, rx=3
  Dark fill = settled portion  (DebtSettled / DebtTotal × full width)
  Light track = ColorProgressTrack

[Total incurred: 520 pts — CaptionFontSize, TextHint]
```

The bar fills **left to right** as debt is settled. Full bar = debt fully cleared.
Use a value converter (`ProgressToWidthConverter`) or a styled `ProgressBar` to
drive the fill width from `DebtProgress` (0.0–1.0).

---

## 6. OutfitDrawer (UserControl)

Overlays the entire view from the right edge. Fixed width ~290px, full parent height.

### Closed state

`IsVisible="False"` (or opacity 0 with a transition — animation can be added later).

### Open state

- **Scrim**: full-size semi-transparent rectangle behind the panel
  (`#2C2C2C`, ~28% opacity). Tapping the scrim closes the drawer.
- **Drawer panel**: `ColorPageBackground`, left border (`ColorBorderWarm`), right-anchored.
    - **Header** (~52px): `ColorSurface`, "Current Outfit" title
      (DisplayFontFamily, italic, SubtitleFontSize), × close button (32×32px, top-right).
    - **Layer list** (scrollable `ItemsControl`): one row per dressing layer.

### Layer rows (see `wireframe-drawer.svg`)

Five layers in fixed order: UNDERWEAR, EXTRA, BASE, OUTERWEAR, ACCESSORIES.

Each row contains:

- Layer name: all-caps, `CaptionFontSize`, `ColorTextHint`, letter-spacing +1
- Item name: `BodyFontSize`, `ColorTextPrimary`. If no item assigned: "(none)" in `ColorTextHint`
- Item detail (when present): `CaptionFontSize`, `ColorTextHint` (e.g. "Black · Polyester blend")
- Thin divider (0.5px, `ColorBorder`) between rows

---

## 7. BaseDialog (Window used as modal)

Used for: punishment list, permission request, shop, and future confirmations.
Shown with `ShowDialog(parentWindow)`. Implemented as a `Window` with
`SystemDecorations="None"`, `WindowStartupLocation="CenterOwner"`,
`SizeToContent="Height"`, fixed width ~380px.

`BaseDialog` exposes `Confirm()` and `Dismiss()` methods and a `bool DialogResult`
property. Subclass dialogs call these to close and return a result.

### Structure (see `wireframe-dialog.svg`)

```
┌──────────────────────────────────────────┐
│  Header — ColorAmberBackground, ~50px    │
│  Title (DisplayFontFamily, italic)  [×]  │
├──────────────────────────────────────────┤
│                                          │
│  Content slot (scrollable, MaxHeight)    │
│                                          │
├──────────────────────────────────────────┤
│  Footer — thin top border                │
│  [Close / Dismiss button — centered]     │
└──────────────────────────────────────────┘
```

Add a subtle `BoxShadow` on the outer border.
The amber header is the same for all dialogs in this app.

### PunishmentListDialog content

An `ItemsControl` of punishment rows. Each row:

- **Name**: `BodyFontSize`, `TextPrimary`
- **Label**: "Assigned DD Mon · Expires DD Mon" or "· No expiry" — `CaptionFontSize`, `TextHint`
- **Start button** (right): 64×34px, dark fill, `MinHeight="44"` via padding
- Thin divider between rows

Tapping "Start" closes the dialog and returns the selected punishment.

---

## 8. DashboardViewModel

### Properties

| Property                  | Type                                            | Notes                                                      |
|---------------------------|-------------------------------------------------|------------------------------------------------------------|
| `DebtTotal`               | `decimal`                                       | `[Reactive]`                                               |
| `DebtSettled`             | `decimal`                                       | `[Reactive]`                                               |
| `DebtRemaining`           | `decimal`                                       | Computed: `DebtTotal - DebtSettled`                        |
| `DebtProgress`            | `double`                                        | Computed: `DebtSettled / DebtTotal`, range 0–1             |
| `Tasks`                   | `ObservableCollection<TaskItemViewModel>`       |                                                            |
| `Punishments`             | `ObservableCollection<PunishmentItemViewModel>` |                                                            |
| `PendingPunishmentsCount` | `int`                                           | `[ObservableAsProperty]`, derived from `Punishments.Count` |
| `HasPendingPunishments`   | `bool`                                          | `[ObservableAsProperty]`, true when count > 0              |
| `IsOutfitDrawerOpen`      | `bool`                                          | `[Reactive]`                                               |
| `CurrentOutfit`           | `OutfitViewModel?`                              | `[Reactive]`, bound to the drawer                          |

### Commands

| Command                     | Parameter type            | Enabled when            | Action                                                     |
|-----------------------------|---------------------------|-------------------------|------------------------------------------------------------|
| `StartTaskCommand`          | `TaskItemViewModel`       | Always                  | Navigate to active task view                               |
| `OpenPunishmentsCommand`    | —                         | `HasPendingPunishments` | Open `PunishmentListDialog`                                |
| `StartPunishmentCommand`    | `PunishmentItemViewModel` | Always                  | Close dialog + navigate to punishment view                 |
| `ToggleOutfitDrawerCommand` | —                         | Always                  | Toggle `IsOutfitDrawerOpen`                                |
| `RequestPermissionCommand`  | —                         | Always                  | Open permission request dialog (spec TBD)                  |
| `OpenShopCommand`           | —                         | Always                  | Open shop dialog (spec TBD)                                |
| `EndSessionCommand`         | —                         | Always                  | Confirm then navigate to session-end view                  |

### Child ViewModels

**TaskItemViewModel**

- `Name: string`
- `EstimatedDuration: TimeSpan`
- `EstimatedDurationLabel: string` — "Est. N min", empty string if zero

**PunishmentItemViewModel**

- `Name: string`
- `AssignedDate: DateOnly`
- `ExpiryDate: DateOnly?`
- `AssignedLabel: string` — "Assigned DD Mon · Expires DD Mon" or "· No expiry"

**OutfitLayerViewModel**

- `LayerLabel: string` — e.g. "UNDERWEAR" (displayed all-caps)
- `ItemName: string` — defaults to "(none)"
- `ItemDetail: string` — fabric/color description; empty if no item assigned
- `HasItem: bool` — computed, `ItemName != "(none)"`

---

## 9. Interactions

| Trigger                                 | Expected behavior                                                 |
|-----------------------------------------|-------------------------------------------------------------------|
| Tap **Start** on a task                 | Navigate to active task view                                      |
| Tap **Punishments** button              | Open `PunishmentListDialog`                                       |
| Tap **Start** inside punishment dialog  | Close dialog, navigate to punishment execution view               |
| Tap **Outfit ▸** / **Current Outfit ▸** | Toggle `IsOutfitDrawerOpen`                                       |
| Tap scrim while drawer is open          | Close drawer (same toggle command)                                |
| Tap **Request Permission**              | Open permission request dialog (spec TBD)                         |
| Tap **Shop**                            | Open shop dialog (spec TBD)                                       |
| Tap **End Session**                     | Show confirmation dialog; on confirm navigate to session-end view |

---

## 10. Empty states

| Element                            | Empty state                                                                       |
|------------------------------------|-----------------------------------------------------------------------------------|
| Tasks list (0 tasks)               | Centered `TextBlock`: "No pending tasks", `CaptionFontSize`, `TextHint`           |
| Punishments button (0 punishments) | `IsEnabled=false`, amber styling hidden, badge hidden                             |
| Outfit layer with no item          | "(none)" in `TextHint` — handled in `OutfitLayerViewModel`                        |
| Debt at zero                       | Progress bar shows empty track, numbers show "0 pts" — no special handling needed |

---

## 11. Touch target guidelines

- Minimum tappable height: **44px** for all interactive elements
- Task rows: `MinHeight="56"` (comfortable finger tap)
- Action bar buttons: `Height="42"` (full width makes them easy to tap)
- Close buttons (drawer, dialogs): **32×32px** minimum
- Punishment "Start" buttons: `Height="34"`, `MinHeight="44"` via padding
