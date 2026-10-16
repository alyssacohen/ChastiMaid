# Wardrobe screen — master/detail refactor

> UI design spec for Claude Code. Intent-based: it states behaviour, structure and constraints,
> not ready-made code. Adapt to the existing codebase; flag anything ambiguous instead of deciding silently.
>
> **Relation to `docs/specs/wardrobe.md`:** that file is the *domain* spec (items, layers, slots, tags,
> multiplier, outfit rules). This file only describes the *screen* that edits that domain. If the two
> disagree on domain semantics, `wardrobe.md` wins: report the discrepancy, do not silently pick one.

Reference mockup: `docs/design/wardrobe/wardrobe-master-detail.html` (read it — see "How to read the mockup").

---

## 1. Why

The current Wardrobe screen is an accordion list (each row expands to show layers, slots, tags, weight,
value, plus a checkbox used only for bulk delete), with a separate "Regole outfit" tab for tag rules.
With the real number of items this is slow and unintuitive.

Replace it with a webmail-style **master/detail** screen:

- left: a plain list of items (name + checkbox);
- right: a detail pane that edits the selected item(s), including **multi-selection editing**;
- top: search plus quick actions (filter, sort, rules, delete).

As a consequence, the **"Regole outfit" tab goes away**: rule editing moves into a modal window
(see section 9).

---

## 2. How to read the mockup

The mockup is a single HTML file in a "Design Component" format. **Do not try to run or port it
literally.** Read it as an executable description of structure and behaviour:

- `<x-dc>` holds the markup; `{{ name }}` are bindings to values returned by `renderVals()`
  in the `<script type="text/x-dc">` block at the bottom.
- `<sc-for>` / `<sc-if>` are repeat / conditional blocks.
- `renderVals()` is the best place to understand the **logic**: filtering, sorting, selection,
  tri-state chip computation, mixed numeric values, stepper buttons, delete/undo. Treat it as
  pseudo-code for the ViewModel layer.
- Inline `style="..."` values are visual intent only. **Map them to the project's design tokens**
  (section 11), never copy them.
- The 14 sample items, the tag names and the three sample rules are **invented placeholder data**
  (layer and slot names do match the real domain, see section 3). Do not import the sample items or rules anywhere.

Interactions worth reading in the file: open each toolbar menu, tick several rows, and watch how the
chips in the detail pane change state; select items with different weights and look at the number fields.

### What the mockup does NOT contain

- **The latest custom controls already developed in the app.** The mockup uses plain approximations
  (basic buttons, text inputs, checkboxes, chips, hand-drawn stepper chevrons). The real project already
  has newer, more refined custom controls, with their own design docs in `docs/design/*.md` and their
  styles under `Assets/Styles`. **Prefer the existing controls** and adapt them to this context where
  needed. Do not re-implement a control that already exists. If an existing control cannot be adapted
  cleanly, stop and report why.
  - **`stacked-spinner`** (`docs/design/stacked-spinner.md`) is one of these controls, and is **mandatory**
    for the Weight and Value fields (see 6.4 and 11). It is **not the only one**: the audit must inventory
    every custom control and style that fits this screen (chips, filter chips, underline TextBox /
    NumericUpDown, buttons, table rows, nav-item accent bar, dialogs, ...) and map each mockup element to it.
- **The Settings `NavPanel`** (the 320 px panel used to move between Settings tabs). The mockup shows
  only the content area of the Wardrobe tab; in the app the NavPanel sits to its left (see 11).
- A portrait / Surface Go twin layout (landscape only, see section 10).
- The real rules modal (only a stub is shown, see section 9).

---

## 3. Data model assumptions

Domain reference: `docs/specs/wardrobe.md`. Per wardrobe item the screen edits:
`Name`, `Layers`, `Slots`, `Tags`, `Weight`, `Value`.

| Field  | Cardinality | Notes |
|--------|-------------|-------|
| Layers | set (1..n) — **an item can belong to several layers at once** | Confirmed by the author. Real layers: underwear, extra, base, outerwear, accessories (canonical order as in `wardrobe.md`). |
| Slots  | set (0..n)  | Real slots: torso, legs, arms, crotch, waist, head, neck, feet (canonical order as in `wardrobe.md`). **User-managed list**: the user can add and delete slots (see 6.7). New slots are appended at the end of the list. |
| Tags   | set (0..n)  | Free-form tags; keep a stable canonical order (as the current code does). **User-managed list**, same as slots (see 6.7). Matching is case-insensitive and trimmed. |
| Weight | number ≥ 0  | Probability weight used by outfit generation. Decimal comma accepted on input. |
| Value  | number ≥ 0  | **Presumably `ClothingItem.Multiplier`** (neutral = 1.0, typical range about 0.9–1.3). **Verify in the audit** and report if the UI label and the model property differ. |

The mockup's sample data already follows this shape (including two multi-layer items and a Base/Extra item).

Do not change the item model, the import/export format or the outfit generation logic as part of this
task unless the audit shows it is unavoidable — and then ask first. In particular, check in the audit how
the current model stores an item's layers and that nothing in the UI change requires touching it.

---

## 4. Layout (landscape)

Page title "Wardrobe" (existing `PageTitle` style) with an item count next to it.

Below, top to bottom:

1. **Toolbar** (one row): search box (flexible width), then four quick actions, in this order:
   - **Filter** — icon button, opens a filter popover. Shows a small badge with the number of active filters.
     At the bottom of the popover: two hyperlink-style entries, *Manage slots* and *Manage tags* (see 6.7).
   - **Sort** — icon button, opens a sort popover. Tooltip/accessible name shows the current sort.
   - **Rules** — icon + label button. Opens the rules modal (a stub for now, section 9).
   - **Delete** — icon button (destructive colour), disabled when nothing is targeted.
2. **Active-filters bar** (only when filters are active): removable chips, e.g. `Layer: Base ×`.
3. **Master/detail area**, filling the remaining height; each pane scrolls independently.
   - **Master (left, fixed width)**: list panel.
   - **Detail (right, fluid)**: edit panel, content max width about 720.

### 4.1 Master pane

- **Header row**: a select-all checkbox, a small **chevron button** next to it that opens the selection
  menu, and a text such as "14 items · 3 selected".
  - Selection menu: *Select all visible (n)*, *Invert selection*, *Clear selection*.
- **List**: one row per visible item.
  - Checkbox, item name, optional small caption listing the item's layers, e.g. "Underwear · Base"
    (the caption is a switch in the mockup; keep it easy to turn off; it truncates, the name has priority).
  - The row is the "active" row when it is the single current item: highlighted background, accent bar
    on the left (same look as the Settings nav item), semi-bold name.
  - Checked rows are highlighted too.
  - The active row also shows a small **pencil button** (rename without double-click, for touch).
  - Empty state when nothing matches: "No item matches search and filters."
- **Footer**: "+ New item" button (existing `Add` style) and a one-line hint about renaming.

### 4.2 Detail pane

Empty state when nothing is targeted ("Select an item to edit its details.").

Otherwise, top to bottom:

1. Title: the item name, or "N items selected" in multi-selection. In multi-selection also show a short
   names preview (first three names, then "and N more") and a **legend** of the three chip states.
2. **LAYER** chips.
3. **SLOT** chips, followed by a "+" chip (add slot, see 6.7).
4. **TAG** chips, followed by a "+" chip (add tag, see 6.7).
5. **WEIGHT** and **VALUE** fields, side by side: existing `NumericUpDown` with classes
   `underline stacked-spinner` (see 6.4).
6. **Rules section** — single selection only: read-only list of the rules that involve the item's tags
   (see 9), or "No rule involves this item's tags", plus a discreet hyperlink-style button
   **"Edit rules"** that opens the rules modal.
7. Destructive **Delete** button (same action as the toolbar icon).

---

## 5. Selection model

Two concepts:

- **Active item**: the single item shown in the detail pane, set by clicking a row.
- **Checked set**: items ticked with the checkboxes.

**Targets** (what the detail pane edits and what Delete removes) =
the checked set if it is non-empty, otherwise the active item.

Rules:

- Clicking a row (not its checkbox) sets it as active **and clears the checked set**.
- Toggling a checkbox only changes the checked set.
- Select-all checkbox: checked when every visible item is checked; toggling it checks/unchecks
  all **visible** items. (Suggested improvement, not in the mockup: show the indeterminate state when
  only some are checked.)
- Selection menu actions apply to the **visible** items only.
- **Decided:** ticking a checkbox makes the ticked item(s) the targets; the previously active item is
  not included unless it is ticked too.

---

## 6. Behaviour details

### 6.1 Search, filter, sort

- **Search**: case-insensitive substring match against item name, layer names, slot names and tag names.
- **Filters**: three groups (layer, slot, tag), each a set of toggle chips in a popover.
  Within a group the logic is OR (item has any of the selected values, which for multi-layer items means
  any of its layers); across groups it is AND.
  A "Reset filters" button clears all groups.
- **Sort**: Name, Layer, Slot, Weight, Value.
  - Layer sorts by the item's first layer in canonical layer order; Slot sorts by the item's first slot
    in canonical slot order (items without layers / slots first).
  - Clicking the active sort key again flips direction; clicking another key selects it ascending.
  - Ties are broken by name (Italian culture-aware comparison).
- All popovers close on outside click.
- Search, filters and sort only change what is **visible**; they never change targets by themselves,
  except that hidden items must not be reachable through select-all / invert.

### 6.2 Rename

- Double-click on the name, or the pencil button, switches the row to an inline underline TextBox with
  the text selected.
- Enter or focus loss commits; Escape cancels; an empty/whitespace name is ignored (old name kept).

### 6.3 Tri-state chips (the core idea)

For each chip, count how many **targets** have that value, `n` out of `total`:

| State   | Condition   | Look |
|---------|-------------|------|
| full    | n == total  | selected chip look |
| partial | 0 < n < total | **faded**, dashed border, small `n/total` counter |
| none    | n == 0      | default (unselected) chip look |

With a single target only full/none can occur.

Click behaviour:

- **Layer / Slot / Tag** (all three are sets, same behaviour): clicking a *full* chip **removes** the
  value from all targets; clicking a *partial* or *none* chip **adds** it to all targets. Result is kept in
  canonical order.
- Edits apply immediately (no Save button) and are reflected live in the list captions.
- Domain constraints (e.g. an item must keep at least one layer, if `wardrobe.md` says so) are enforced
  as the current code does; report in the audit how the current UI handles removing the last layer.

The partial state needs a new visual state on the chip control: design it by **extending the existing
chip control/styles**, not by creating a parallel one. It must also be exposed for accessibility
(mixed / pressed / not pressed).

### 6.4 Weight and Value (including multi-selection)

Both fields use the existing `NumericUpDown` with **`Classes="underline stacked-spinner"`**
(see `docs/design/stacked-spinner.md`: vertical up/down buttons stacked in the spinner area; use the touch
padding suggested there). The mockup draws this look by hand with two small chevron buttons; use the
real control and styles, not a copy.

Behaviour (adapt the control where needed, keep the adaptation in styles/behaviours, not in a copy of the control):

- If all targets share the same value, show it. Otherwise the field is **empty with the placeholder
  "Different values"** (italic; for `NumericUpDown` this means a `null` value plus a watermark) and a
  caption below shows the range, e.g. "from 0,5 to 1".
- Typing a valid number (≥ 0, decimal comma or dot) writes it to **all** targets immediately.
- While the user is typing, keep their raw text (a "draft") so partial input like `0,` is not reformatted;
  discard the draft on blur and whenever the selection changes. If `NumericUpDown` cannot support this
  cleanly, say so in the audit and propose the least invasive adaptation.
- Invalid input is not applied.
- Numbers are displayed with a decimal comma (current UI culture).
- **Stepper buttons (decision to confirm):** with a single target they add/subtract the step (mockup:
  0.1 for Weight, 0.05 for Value; clamp at ≥ 0, round to 2 decimals). With **mixed values the mockup
  disables the stepper** (faded) so that nothing is silently overwritten; the user must type a value
  first. Use the real steps/min/max already defined in the existing controls and models.

### 6.5 New item

"+ New item" appends an item with name "New item", layers = the single active layer filter if exactly
one is set (otherwise the default layer), no slots, no tags, and the **default weight and value used by
the current code** (the mockup uses 1 and 1, i.e. neutral multiplier — verify). It resets search and
filters (so the item is visible), becomes the active item and enters **rename mode**.

### 6.6 Delete and undo

Delete (toolbar icon, detail button) removes all targets at once, clears the checked set and shows a
transient notice "N items deleted" with an **Undo** action that restores the previous list.
No confirmation dialog: undo replaces it.

### 6.7 Adding, removing and managing slots and tags

Slots and tags are user-managed lists. Layers are **not**: they are fixed and carry the generation
probabilities (see `wardrobe.md`). The flow below **replaces the current special text field that converts typed
words into chips**; the mockup shows every part of it (add chip, add modal, both confirmation dialogs, manage modals).

**Add chip.** At the end of the SLOT chips and of the TAG chips there is a "+" chip: same height, shape and
style as a chip in the *none* state, icon only, tooltip and accessible name "Add slot" / "Add tag". It opens a small
modal in the style of Gmail's "New label": title, one underline text field (focused on open), Cancel / Add;
Enter confirms, Esc cancels.

- On confirm the new value is appended to the list **and applied to all current targets** (like Gmail applies a
  new label to the selected conversations).
- Empty names are refused. Duplicates (case-insensitive, trimmed) are refused with an inline message.

**Last user removed.** When un-ticking a slot / tag chip removes the value from the last item that had it (no
item uses it any more), a confirmation dialog appears: *"No item uses the slot/tag «name». Delete it?"*

- Confirm: the entry is removed from the list and from the active filters. For a tag, the rules that cite it are
  deleted too, and the dialog says how many (addition proposed in the mockup, to be confirmed).
- Cancel: the entry stays in the list, unused.
- Does not apply to layers. It is triggered only by un-ticking a chip, **not** by deleting items.

**Manage entries.** *Manage slots* / *Manage tags* (bottom of the filter popover, touch target ≥ 44) open a modal
listing every existing entry with its usage ("5 items · 1 rule"; slots show items only), a delete icon per row,
and an add field at the top (this one only creates the entry, it does not apply it to any item).

- Delete asks for confirmation **always**, even with zero usages: *"The tag «x» is used by 5 items and 1 rule.
  Deleting it removes it from all items and deletes the rules that cite it. Continue?"*
- After deletion a notice "Tag «x» deleted" is shown (the mockup has no undo here: confirmation replaces it).

Accessibility: the "+" chips and the row delete icons need accessible names; dialogs are modal, focus moves into
the dialog and returns to the invoking control. All texts above are placeholders (localization rules, section 12).

---

## 7. Toolbar details

- Filter / Sort / Delete are **icon-only**: each needs an accessible name and tooltip.
- Delete is disabled when there are no targets; its tooltip says whether it deletes one or N items.
- The **Rules** button is icon + label. For now it opens a **stub modal** (see section 9).

---

## 8. Accessibility

- Every interactive element is a real control (no clickable non-interactive panels).
- Chips expose pressed / mixed state.
- Icon-only buttons have accessible names.
- Visible focus ring on all controls.
- Caption/secondary text in the mockup is **darker than the `TextMuted` token** on purpose (contrast).
  Flag it in the audit; do not silently change the token — it is a design decision.

---

## 9. Rules ("Regole outfit") — tab removal and modal

This refactor **removes the separate "Regole outfit" tab**. Its content (tag-based rules, stored in LiteDB
as `outfit_rules_v2`) moves into a **modal window**, opened from:

1. the **Rules** button in the toolbar;
2. the **"Edit rules"** link in the detail pane.

Rule semantics (see `wardrobe.md`): `Requires` (tag A requires tag B) and `Incompatible`
(tag A is incompatible with tag B, **bidirectional**). Verify in the audit that this still matches the code.

The modal is a later phase and is **not designed in the mockup** (the button and link are placeholders).
Constraints:

- **Decided:** the Rules button and the "Edit rules" link open a **stub modal** — a placeholder dialog
  (title "Regole outfit", short explanatory text, Close button; see the mockup). Use the project's existing
  modal styles (`dialogOverlay` / `dialogBox`).
- **Do not remove the tab until the real modal content exists**, otherwise rules become uneditable.
  Until then the existing tab stays as it is.
- Reuse the existing rules ViewModel and data/validation logic; this is a relocation, not a rewrite.
- Rules continue to be validated against wardrobe/tag data exactly as today (including the import
  behaviour described in `docs/design/content-import-export.md`).
- The read-only rules list in the detail pane (single selection only) is derived from the same rules data:
  every rule that involves one of the item's tags. A `Requires` rule is listed for its source tag
  (e.g. "Lace requires Maid"); an `Incompatible` rule is bidirectional, so it is listed for **both** tags
  (e.g. "Summer is incompatible with Winter" appears on a Summer item and on a Winter item).

---

## 10. Surface Go / touch

- Target both 1920×1080 desktop and Surface Go (touch-first, Windows 10 tablet mode, low power).
- Touch targets: primary buttons and rows ≥ 44 px (rows 48 = `TableRowHeight`), chips ≥ `ChipHeight`.
  The stacked spinner must stay comfortably tappable (see the touch padding in its doc).
  No hover-only or right-click-only features.
- The mockup is **landscape only**. A portrait layout is out of scope here; when it is tackled, follow
  Golden Rule 6 (twin layout via `IsLandscape`, not fluid breakpoints). Structure the new view so adding a
  second subtree later is straightforward.
- Search, rename and number fields must work with the existing virtual-keyboard handling.
- The pencil button exists precisely because double-click is awkward on touch.

---

## 11. Design system mapping

Use tokens and existing styles; never inline colours, sizes or spacing in the view.

| Mockup value | Use |
|---|---|
| `#FAF7F2` page / header row | `BrushWindowBackground` / `BrushSurfaceSubtle` |
| `#FFFFFF` panels | `BrushSurfaceBase` |
| `#F2EDE4` unselected chip, row highlight | `BrushChipDefault` / `BrushSurfaceMuted` |
| `#6B4F35` selected chip, accent bar, links | `BrushChipSelected` / `BrushButtonPrimary` / `TextAccent` |
| `#C8B99A` borders | `BrushBorderSubtle` |
| `#8B3A2A` delete | `BrushButtonDanger` (destructive only) |
| text `#2C2118` / `#4A3F35` | `TextPrimary` / `TextSecondary` |
| captions | existing `Caption` / `BodySmall` classes |

Other points:

- **Existing custom controls first.** Besides `stacked-spinner`, map every mockup element to the existing
  control/style it should use (audit item 2). The mockup's approximations are never the source of truth.
- **Settings context.** Wardrobe is a Settings tab: in the app the `NavPanel` (320, `NavPanelWidthGridLength`)
  sits to the left of the area shown in the mockup, which does not include it.
- **Decided: introduce a new token** for the master pane width (Double + `GridLength` companion, e.g.
  `WardrobeListPanelWidth` / `WardrobeListPanelWidthGridLength`); do not reuse `NavPanelWidth`.
  The mockup uses 360 as a starting value.
- **Width budget — verify in the audit.** On the Surface Go in landscape (1200×800 DIP at 150% DPI) the
  NavPanel (320) plus the master pane (360) leave about 440 for the detail pane. Check real layouts at
  1200×800 and 1920×1080 and propose the pane width (and chip wrapping, and whether Weight/Value fit
  side by side) accordingly.
- Chip height in the mockup is 32; use the project's `ChipHeight` (30).
- Buttons: `Ghost`, `Add`, `Danger`, `Delete`, `SmallGhost` as appropriate; icons as existing SVG assets
  or `Path` geometries following the project's current convention.
- Underline inputs: existing `TextBox.underline` for text and `NumericUpDown.underline stacked-spinner` for numbers.
- Filter chips: existing `ToggleButton.filterChip` (extend, do not duplicate).
- Remember the dual spacing tokens (`Double` for `Spacing`, `Thickness` for `Margin`/`Padding`).

---

## 12. Decisions taken and points to verify

Decided by the author:

1. **Layers are multi-valued**: an item can have several layers at once (sets, like slots and tags).
2. **Selection**: the ticked item(s) become the targets (see 5).
3. **Rules button / link**: open a **stub modal** (see 9).
4. **Master pane width**: a **new token** is needed, because the mockup omits the Settings `NavPanel`
   (see 11).
5. **Localization**: this work is applied to the **GetText.NET version** of the app (`.po` files, Poedit).
   Branch from the GetText branch (confirm its name). Rules:
   - Follow `docs/design/localization.md` (the project's localization design doc).
   - The Italian strings in the mockup are **placeholders**. Before adding any string, check which
     equivalent strings are **already translated** in the `.po` catalogue (e.g. Layer, Slot, Tag, Weight,
     Value, New item, Delete) and reuse those msgids and that wording.
   - New strings: ViewModel-provided text and the project's markup extension for static labels, exactly as
     in the migration; add only what is genuinely missing, in English msgids with Italian translations.
   - Report in the audit any existing translation that conflicts with the mockup wording; do not silently
     rename existing strings.
   - Include the new strings in the `.po` tests (Karambolo.PO) used by the project.
   - Layer / slot / tag **names** are data, not UI strings: follow however the migration handles seed data.
6. **Existing custom controls** (`stacked-spinner` and others) must be integrated into the new UI (see 2, 6.4, 11).
7. **Slots and tags** are added with a "+" chip and a Gmail-style modal, removed through the "no item uses it"
   confirmation, and managed from *Manage slots* / *Manage tags* in the filter popover (see 6.7). This replaces
   the current text field that turns words into chips.

Proposed in the mockup, **not yet confirmed by the author**: a new value is applied to the current targets; the
"unused" prompt and the delete prompt also mention the rules that will be deleted; deleting items never prompts
about newly unused entries; no undo after deleting a slot/tag.

To verify in the audit:

- **Where the list of slots and tags lives.** If the lists are derived from the items, an entry added through
  *Manage* (or left unused after the "keep" answer) would vanish on reload: a persisted catalogue is then
  required (Profile DB, import/export format, Save DB snapshot). That touches the model: **ask first**.
- `wardrobe.md` lists a fixed set of slots as "available". Check how the builder and the current Settings UI
  treat slots outside that set, and update `wardrobe.md` if slots are in fact user-defined.
- Whether anything else references slot or tag names (rules, import/export, outfit selection screen) so that
  delete / rename-free deletion stays consistent.
- How the current model stores an item's layers and whether the UI change needs anything beyond view and
  ViewModel changes (see 3).
- Whether UI "Value" is `ClothingItem.Multiplier`, and the real default/min/max/step of Weight and Value.
- Stepper behaviour on mixed values (6.4): the mockup disables it; **the author still has to confirm**.
- When edits are persisted: immediately, or when the Profile DB is saved as today.
- The width budget on the Surface Go (see 11).
- Whether the Wardrobe tab keeps its own `PageTitle` inside Settings or the Settings shell already
  provides one (the mockup shows a title for completeness).
- Whether the rule semantics in 9 (Requires / Incompatible bidirectional) match the code.

---

## 13. Acceptance criteria

- The accordion list and the "Regole outfit" tab are no longer reachable from the Wardrobe screen
  (tab removal only once the modal exists, see 9).
- Search matches name, layer, slot and tag; filters and sort behave as in 6.1.
- Multi-selection editing: chips show full / partial (with counter) / none correctly; clicking applies
  to all targets; weight and value show common value or placeholder + range.
- Weight and Value use `NumericUpDown` with `underline stacked-spinner`; existing custom controls are reused
  or extended, none re-implemented.
- Rename, new item, delete with undo behave as in 6. Layers behave exactly like slots and tags.
- Slots and tags: "+" chip and add modal, "no item uses it" confirmation, *Manage slots / tags* modals with
  usage counts and delete confirmation behave as in 6.7; the old text-to-chip field is gone.
- The Rules button and the "Edit rules" link open the stub modal; the existing tab is untouched until
  the real modal content exists.
- All new UI strings go through the GetText catalogue, reusing existing translations where they exist.
- All new logic that is not purely visual (filter/sort pipeline, tri-state aggregation, mixed numeric
  values, delete/undo) is covered by unit tests.
- No inline colours/sizes in the view.
- Builds with no new StyleCop warnings; works with touch on the Surface Go (landscape).
