# Prompt for Claude Code — Wardrobe master/detail refactor

Work on a dedicated branch (e.g. `refactor/wardrobe-master-detail`), created from the GetText.NET branch.
Follow the project's `CLAUDE.md` and the `chastimai-design-system` skill.

## Inputs

- Spec: `docs/specs/wardrobe-master-detail.md` — read it fully first. It complements the domain spec
  `docs/specs/wardrobe.md` (read that too; on domain semantics it prevails).
- Reference mockup: `docs/design/wardrobe/wardrobe-master-detail.html` — **read and interpret it**
  (do not try to run or port it literally). The spec explains how to read its format.
  Pay particular attention to the script block (`renderVals()`) for the logic, and to how the view
  changes when menus open and when chip states change.
- Important: the mockup does **not** use the latest custom controls already present in the app.
  Prefer the existing controls and styles, and adapt them to the new context where needed.
  Do not re-implement controls that already exist.
  - `docs/design/stacked-spinner.md` describes one of them (`NumericUpDown` with `underline stacked-spinner`),
    to be used for Weight and Value. It is **not the only one**: read the other `docs/design/*.md` files
    and the styles under `Assets/Styles` to find every existing control that fits this screen.
- This refactor removes the "Regole outfit" tab; its content will move to a modal window (spec, section 9).
  For now the Rules button and link open a **stub modal**.
- Base this work on the **GetText.NET** version of the app (branch to be confirmed by me): UI strings use
  the `.po` catalogue (see `docs/design/localization.md`). Reuse strings that are already translated; the Italian text in the mockup is
  placeholder wording (spec, section 12).

## Working rules

- Express changes in terms of ViewModel extension points; adapt to the existing architecture
  (MVVM, ViewLocator, ReactiveUI, DynamicData) rather than imposing a new one.
- No inline styles, colours or sizes in views; use tokens and existing style classes.
- Code comments and docstrings in English; follow best practices and StyleCop.
- Do not change the item model or the import/export format without asking first.
- Do not decide ambiguous cases on your own: list them and wait for my answer.
- Build after each phase and report the result. Commit per phase with conventional commits.
- **Stop at the end of each phase and wait for my confirmation before starting the next one.**

## Phase 0 — Audit (read-only, no code changes)

Report back on:

1. The current Wardrobe view/ViewModel, the "Regole outfit" tab and how they are wired.
2. An **inventory of the existing custom controls and styles** (stacked-spinner and every other one found in
   `docs/design/*.md` and `Assets/Styles`) and how each maps to a mockup element: chips, filter chips,
   underline TextBox / NumericUpDown, buttons, table rows, nav-item accent bar, dialogs, etc. Say which ones
   need adapting — notably a **partial / mixed state for chips**, and an **empty/mixed state with watermark
   for the stacked-spinner NumericUpDown**.
3. How the mockup's concepts map onto the existing model and services (an item has **several layers**,
   canonical layer/slot/tag order, rules data, UI "Value" vs `ClothingItem.Multiplier`, real default, min, max
   and step of Weight and Value).
4. The strings in the mockup versus the strings **already translated** in the `.po` catalogue: which can
   be reused as they are, which conflict, which are new.
5. The width budget on the Surface Go (NavPanel 320 + new master pane token) and your proposal for
   the new token value.
6. The points to verify in spec section 12 (including persistence timing, stepper behaviour on mixed values, and
   **where the slot / tag lists are stored** and how the current text-to-chip field works).
   If the lists are derived from items, say so: a persisted catalogue would change the model, so ask first.
7. Risks, deviations from the spec you would propose, and a file-by-file change plan.

## Phase 1 — ViewModel layer

Implement selection (active item + checked set + targets; ticked items replace the active one as targets),
search/filter/sort pipeline, tri-state aggregation for layers/slots/tags (all three are sets), aggregated
weight/value with drafts and range, add/rename/delete with undo, and the rules-for-item projection
(Requires, plus Incompatible in both directions).
Include slot / tag list management (spec 6.7): add (applied to targets), duplicate check, "last user removed"
detection, delete with usage counts (items and rules), cascade on items, filters and rules.
Keep it view-agnostic and unit-tested.

## Phase 2 — Tri-state chip and mixed-value number field

Extend the existing chip control/styles with the partial state (faded, dashed border, `n/total` counter),
including accessibility state. Adapt the existing `NumericUpDown underline stacked-spinner` for the
empty/mixed state (null value, "Different values" watermark, stepper behaviour agreed after the audit).
Show both in isolation if the project has a place for control samples.

## Phase 3 — Wardrobe view (landscape)

Build the master/detail view with toolbar, popovers, active-filters bar, list, detail pane and undo notice,
the "+" chips with their add modal, the confirmation dialogs and the *Manage slots / tags* modals (spec 6.7,
replacing the current text-to-chip field), reusing existing controls (Weight and Value with `NumericUpDown` `underline stacked-spinner`).
Add the new master-pane width token (Double + `GridLength`). Add the stub rules
modal using the existing modal styles. Add the new strings to the `.po` catalogue (with tests). Remove the
accordion list. Keep the structure ready for a future portrait
twin layout (do not implement it now).

## Phase 4 — Touch and polish

Verify touch targets (including the stacked spinner), virtual-keyboard behaviour for search/rename/number
fields, focus order and accessible names. Check desktop and Surface Go landscape sizes.

## Phase 5 — Rules modal and tab removal (only when I ask)

Relocate the existing rules editing UI into a modal opened from the toolbar Rules button and the
"Edit rules" link, reusing the existing rules ViewModel and validation. Then remove the
"Regole outfit" tab and its navigation entry.
Until this phase, the Rules button/link open the stub modal and the existing tab stays in place.
