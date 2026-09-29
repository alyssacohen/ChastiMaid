# New Game Contract Screen — Implementation Prompt

You are working on **ChastiMaid** (C#, .NET 10, Avalonia UI with the Fluent theme, ReactiveUI, DynamicData, MVVM with the ViewLocator convention `{Name}ViewModel` → `{Name}View`).

This prompt is **intent-based**: it describes requirements and extension points, not ready-made code. Adapt to the existing codebase, follow its conventions, and **flag ambiguous cases instead of deciding them autonomously**.

## How to work

- Work in the phases below. **Stop at the end of every phase**, summarize what you did, confirm the build is green (`dotnet build`, tests, zero new StyleCop warnings), and wait for my confirmation before continuing.
- Code comments and XML doc comments are written in **English**. Public members get XML docs. Follow C# best practices (nullable reference types, small classes, single responsibility, no logic in code-behind).
- Commits follow conventional commits, one or more per phase, grouped by concern (e.g. `feat(contract): add GameContract model`).
- The final spec goes to `docs/design/new-game-contract.md` (outside the `.csproj`, tracked in Git).

## Current behavior

The welcome screen offers: Continue, New Game, Load Game, Settings, Exit.

"New Game" opens a screen that only communicates **Initial Debt** and **Daily Interest**, with the buttons *Start Game*, *Settings* and *Back*. The Settings button there is redundant.

## Goal

Keep the existing flow, but turn the New Game screen into a **contract** that presents **all the currently configured settings** in the form of a contract, with actions at the bottom to accept it, edit it, or load a different contract. The contract can also be **exported to a file**, so that it can be reused or sent to someone else.

### 1. Contract screen (replaces the current New Game screen)

- Reads like a contract: a preamble, then numbered clauses generated from the current settings, grouped like the Settings sections (initial debt and daily interest, hourly wage and other difficulty-related values, wardrobe and outfit rules, popups, punishments, other game-level settings), then the closing actions. The wardrobe clause summarizes the wardrobe (e.g. counts per layer) rather than listing every item.
- Actions at the bottom:
  - **Accept the terms** (Italian: "Accetto le condizioni") — `Primary`. Starts the game exactly as the current *Start Game* button does.
  - **Edit the terms** (Italian: "Modifica condizioni") — `Ghost`. Opens Settings. When the user leaves Settings, they return to the contract screen, which shows the updated values.
  - **Load another contract** (Italian: "Carica un altro contratto") — `Ghost`. Opens a file picker, validates the file, applies it (see "Loading a contract" below), and the screen shows the loaded terms.
  - **Back** (Italian: "Indietro") stays as plain navigation to the welcome screen; it is not one of the three actions above. Keep it visually distinct and less prominent.
- Remove the old *Settings* button from this screen: *Edit the terms* replaces it.
- No signature, no checkboxes, no extra confirmation controls.

### 2. Contract export (Settings → General → Import/Export content)

Extend the existing Import/Export content section so that it can **export the contract**: the game-rule settings serialized to a file, following the JSON approach already decided (`System.Text.Json`, explicit `schemaVersion`, human-readable). Working extension: `.contract.json`.

- As decided earlier, the contract file also **includes the wardrobe and outfit rules**, by reusing the existing DTOs and serializers of the content import/export (`docs/design/content-import-export.md`), so that a game can be fully prepared for someone else. The existing three-file content export/import stays untouched and keeps working.
- The contract contains only **game-rule settings**. Device-specific settings (e.g. selected camera, audio devices, window/orientation preferences) must **not** be in the file.
- The file carries no acceptance information.
- Also add the matching **Import contract** action in the same section, using the same service as *Load another contract*, unless the audit shows a reason not to (Q3).

### 3. Loading a contract

Loading a contract applies its game-rule settings to the current settings, and its wardrobe and outfit rules through the existing import order (wardrobe → outfit rules, validated against wardrobe IDs; invalid entries discarded and reported; independent fallbacks preserved). Because this **replaces** the user's current configuration:

- show a confirmation (`dialogOverlay` / `dialogBox`) stating what will be replaced, before applying anything;
- validate the whole file first (schema version, every value); out-of-range values are clamped or discarded and **reported to the user**; a file that fails validation changes nothing;
- device-level settings are never touched.

### 4. New clause: variable punishment severity (feature not implemented yet)

Add a clause warning the user that **the severity of punishments is subject to variation depending on how well they are behaving**. The clause is **informational only**:

- Do **not** implement any calculation or change any punishment logic.
- For your information (and for the spec): the future "behavior" parameter will be computed from (a) how well the recommended task frequencies are respected, (b) the number of pending punishments, and (c) the number of refused garments. Do not encode weights or formulas anywhere.
- The clause is fixed contract text, not a setting: it is not serialized, and it appears in every contract. Add a clear `TODO` in code pointing to the future feature, and record it in the spec.
- Whether the clause names the three factors or stays in-world and vague is open question Q4.

## Non-goals

- No wizard, no new screens beyond the contract screen, no changes to gameplay logic, outfit generation or the existing content import/export flows.
- No cryptographic protection of the contract: it is a plain, editable file. Integrity here means schema validation, not anti-tampering.
- No new gameplay settings, no difficulty presets. If you think one is needed, flag it.

## Design principles

- **One place builds the contract.** A `GameContract` model (working name) with a service that (a) builds it from the current settings, (b) serializes and deserializes it, (c) validates it, (d) applies it. The contract screen's clauses, the export and the import all rely on it. No parallel copies of the same data.
- **Reactive rendering.** The contract screen must always reflect the current settings, including after returning from Settings or loading a file, without ad-hoc refresh code.
- **Clarity over cleverness.** Reuse the existing settings and services; don't invent a draft/preview layer unless the audit shows it is necessary (Q1).

## UI requirements

- Follow the design system in `Assets/Styles/` (Colors, Spacing, Typography, Buttons, Chips, Tables, Components). **No inline styles in views**: views only assign classes; if a new style or token is needed (contract paper, clause layout), add it to the proper style file and tell me. Do not introduce new font usages: Cormorant Garamond only for `PageTitle`, `SectionTitle`, `EmployerVoice`; Special Elite is reserved for the typing punishment. Warm light theme only.
- Spacing tokens have two typed families: `Double` for `StackPanel.Spacing`, `Thickness` for `Margin`/`Padding`. Use `SpacingButtonStripThickness` for the button strip; use explicit `ColumnDefinition` elements with `NavPanelWidthGridLength` for token-driven column widths.
- Building blocks to reuse: `card`, `Primary` / `Ghost` buttons, `dialogOverlay` / `dialogBox`.
- **Touch first** (Surface Go, Windows 10, tablet mode): min 44px height for buttons, no hover-only affordances, no Windows 11-only APIs. A long contract must scroll comfortably with touch, and the actions must remain reachable (decide with me in the audit whether they are pinned at the bottom or at the end of the scrollable content, Q5).
- **Twin-layout, not fluid breakpoints.** If the contract screen needs different layouts for landscape and portrait, define two subtrees toggled by the existing `IsLandscape` ViewModel pattern (see `DashboardView`); otherwise a single scrolling column is fine. Flag it.
- Contract copy follows `docs/design/TONE_GUIDE.md` (Late Victorian/Edwardian voice; Italian uses the "Voi" form). Write **English first**, with an Italian draft that I will review manually; mark Italian copy as "needs review". Appellatives must **not** be interpolated dynamically into Italian sentences: use pre-built phrase variants per appellative. Numbers (debt, interest, wage) may be interpolated.

## Phases

### Phase 0 — Audit (no code changes)

Report on: the current New Game flow (welcome screen, view, ViewModel, services, navigation); every `GameSettings` field, classified as game-rule vs device-level; **where settings live** (global before a game exists, copied per save, or both) and what happens to them when a game starts; how Settings is opened and whether it can return to the contract screen; the current Settings → General → Import/Export content section and the state of the content import/export implementation; how localization is done. Then propose the mapping of settings to contract clauses and answer/flag the open questions below. **Stop and wait.**

### Phase 1 — Contract domain (no UI)

`GameContract` model (game-rule settings + wardrobe + outfit rules), build/serialize/deserialize/validate/apply service, mapping to and from the current settings, and unit tests (valid, invalid, out-of-range, unknown schema version, rules referencing missing wardrobe IDs, round-trip, device-level settings excluded). **Stop and wait.**

### Phase 2 — Export / import in Settings

Contract export (and import, per Q3) in Settings → General → Import/Export content, with file dialogs, validation report and the replace-confirmation. **Stop and wait.**

### Phase 3 — Contract screen

Replace the New Game screen: contract rendering with clauses, the three actions plus Back, navigation to Settings and back, *Load another contract* wired to the service from Phase 2, the informational punishment-severity clause. Remove the redundant Settings button. **Stop and wait.**

### Phase 4 — Polish and documentation

Token audit (no inline values in the new views), touch-target check on Surface Go in portrait and landscape, Italian copy marked for review, and the spec in `docs/design/new-game-contract.md`, including the future punishment-severity feature described above.

## Open questions to flag in Phase 0 (report, don't decide)

- **Q1 — Loaded contract vs current settings:** loading a contract overwrites the current settings (proposal, with confirmation) versus keeping it as a temporary draft. This depends on where settings live today, and on whether Settings can edit a draft at all. Prefer the simplest option that works with the existing architecture.
- **Q2 — Wardrobe in the contract:** confirm that including wardrobe and outfit rules is feasible with the existing serializers, how large the file can get, and whether **tasks** (the third content file) should also be included (proposal: leave them out unless the audit shows they define a game).
- **Q3 — Import in Settings:** add *Import contract* next to *Export contract* in Settings, in addition to *Load another contract* on the contract screen (proposal: yes, same service).
- **Q4 — Severity clause wording:** name the three behavior factors explicitly, or keep the clause in-world and vague (proposal: mention them loosely in-world, without formulas or weights). Copy needs my review either way.
- **Q5 — Actions placement** on long contracts (pinned vs end of scroll).
- **Q6 — Anything in the audit** that contradicts an assumption in this prompt, including the interaction between the future variable severity and the existing punishment severity cap (verify that cap exists under Settings → Popup and Punishments).
