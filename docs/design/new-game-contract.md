# New Game Contract

The New Game screen presents the current configuration as a **contract**. The same configuration can be exported
to a file (`.contract.json`) and loaded again, on this machine or another, so that a whole game can be prepared for
someone else.

The contract is the app's only data file format: it is also the per-language seed (see
[Default contracts (seed)](#default-contracts-seed)).

## Where the settings live

- The **profile DB** holds the editable configuration (`GameSettings` and the wardrobe, outfit rules, tasks,
  punishment and permission collections). Settings edits it.
- Starting a game **snapshots** all of it into the save DB (`SaveRepository.CreateFromProfile`). Later profile
  edits never reach a game in progress.
- Consequence: loading a contract **overwrites the profile** (after a confirmation). There is no draft layer — before
  a game starts there is nothing to protect, and games already started are protected by their snapshot.

## Screen (`NewGameViewModel` / `NewGameView`)

Reached from Welcome → *New Game*. One scrolling column (no twin layout) on a `contractPaper` sheet, with a pinned
action bar (`contractActionBar`) that stays reachable on long contracts.

| Action | Class | Behaviour |
|---|---|---|
| Accept the terms / *Accetto le condizioni* | `Primary` | Identical to the former *Start Game*: rolls the debt from the profile settings, creates the save slot from the profile, starts the session. |
| Edit the terms / *Modifica condizioni* | `Ghost` | Opens Settings; leaving Settings returns to a **new** instance of this screen, which reads the edited profile. |
| Load another contract / *Carica un altro contratto* | `Ghost` | Shared `ContractImportFlow` (below); on success the screen re-renders from the new profile. |
| Back / *Indietro* | `Quiet` | Plain navigation to Welcome. |

No signature, checkbox or extra confirmation. The clauses derive from `NewGameViewModel.Contract`
(`ContractClauseBuilder.Build`), so any change to the contract re-renders them without refresh code.

### Clauses

Built by `ContractClauseBuilder` (pure, unit-tested). Numbers are interpolated; **appellatives never are** — only
their count is stated — so Italian sentences stay grammatical. The contract addresses "the Maid", never the player
name. Voice follows `TONE_GUIDE.md` (Italian: "Voi", no gendered adjectives).

| # | Clause | Source |
|---|---|---|
| 1 | Debt and Interest | `MinDebt`, `MaxDebt`, `DailyInterestRate` |
| 2 | Wages | `IdleWageAmount`, `IdleWageIntervalMinutes` |
| 3 | The Wardrobe | garment count per layer, rule counts, Plug and Play, Diaper Play, chastity devices |
| 4 | Standing Tasks | task count and total value |
| 5 | Summons and Check-ins | popup intervals/timeout, hydration, curtsy, quote reading, kneel, busy captures |
| 6 | Corrective Measures | fine, Cornertime, Writing Lines, Forced Outfit |
| 7 | Requests for Leave | per-kind permission tuning |
| 8 | Sundry Provisions | Cornertime narration, count of forms of address |
| 9 | Variable Severity | **fixed text**, see below |

A feature that is disabled (or enabled but with nothing designated) is stated as "not in force" rather than omitted.

### Variable severity (not implemented)

Clause 9 warns that the severity of punishments varies with the player's behaviour. It is **informational only**:
fixed text, present in every contract, not a setting, not serialized. There is a
`TODO(variable-severity)` in `ContractClauseBuilder`.

The future "behavior" parameter will be computed from:

1. how well the recommended task frequencies (`Chore.FrequencyDays`) are respected;
2. the number of pending punishments;
3. the number of refused garments (`ClothingItem.RejectionCount`).

No weights or formulas are defined, in code or here. The clause names the factors loosely, in-world.

**Interaction with existing caps.** There is **no global severity cap** in Settings. Caps exist per punishment kind and
are applied when a punishment is assigned or scaled (`SaveRepository.AssignPunishment` / `ScalePunishment`):
Cornertime `CornertimeMin/MaxRequiredSeconds`, Writing Lines `WritingLinesMin/MaxLines`, Forced Outfit
`ForcedOutfitMaxHours`. The fine has no cap. Severity itself is unbounded (popup lateness adds seconds without a
ceiling). A variable severity should therefore feed into the severity *before* those clamps, and the clause must not
promise any overall maximum.

## File format

`.contract.json`, `System.Text.Json` with `ContentJsonOptions` (camelCase, indented, camelCase enums). Model:
`Models/GameContract.cs`. Current `schemaVersion`: **1**.

```jsonc
{
  "schemaVersion": 1,
  "settings": { /* GameSettings, minus machine-specific fields */ },
  "wardrobe": [ /* ClothingItem */ ],
  "outfitRules": [ /* OutfitRule */ ],
  "tasks": [ /* Chore */ ],
  "forcedOutfitPresets": [ /* ForcedOutfitPreset */ ],
  "plugAndPlayItems": [ /* PlugAndPlayItemConfig */ ],
  "diaperPlayItems": [ /* DiaperPlayItemConfig */ ],
  "chastityDeviceItems": [ /* ChastityDeviceItemConfig */ ],
  "permissionTypes": [ /* PermissionTypeConfig */ ]
}
```

All nine sections are required; a missing one is a fatal error (an empty array is fine, except the wardrobe — see
below). The file carries **no acceptance information and no progress**. It is a plain, editable file: integrity means
schema validation, not tamper protection. The models are serialized directly (there are no DTOs), like the content
files.

### Included and excluded

The contract carries everything `CreateFromProfile` snapshots, so a game can be recreated from it.

Excluded from `settings` (marked `[JsonIgnore]` on `GameSettings`; a test pins the list): `Id`, `PlayerName`,
`Language`, `AutoSaveIntervalMinutes`, `SelectedCameraIndex`. These belong to the person or machine, not the game;
`Apply` keeps the current values. `Appellatives` and `CornertimeVoiceEnabled` **are** included.

Also excluded: `WardrobeBalance` and `InfractionSeverity` (static constants, not settings), and per-item runtime
state (`RejectionCount`, `LastCompletedAt`, `IsOccasional`, already `[JsonIgnore]`).

Config entries (`plugAndPlayItems`, `diaperPlayItems`, `chastityDeviceItems`, `forcedOutfitPresets`) reference
wardrobe item ids, which are preserved by the contract, so the references stay valid.

## Service (`GameContractService`)

The one place that builds, serializes, validates and applies a contract. Screen clauses, export and import all use it.

- `Build()` — from the current profile.
- `Serialize(contract)` — JSON.
- `Validate(json)` / `Validate(contract)` — never touches the profile; returns the normalized contract and every
  issue (`ContractValidationResult`).
- `Apply(result)` — throws if the result is fatal; otherwise replaces settings (keeping the machine-specific values)
  and every collection.

### Validation

Validate-first and all-or-nothing: a file that fails changes nothing.

| Problem | Result |
|---|---|
| Unreadable JSON, missing/non-numeric `schemaVersion` | **Fatal** `InvalidFile` |
| `schemaVersion` ≠ 1 | **Fatal** `UnsupportedSchemaVersion` |
| Missing section | **Fatal** `MissingSection` (all reported) |
| No usable wardrobe item left | **Fatal** `EmptyWardrobe` |
| Numeric setting out of range | Clamped, reported (`ValueClamped`) |
| Min > Max pair | Swapped, reported (`RangeSwapped`) |
| Wardrobe item without name/valid layer, duplicate id | Discarded, reported |
| Outfit rule referencing missing **tags** | Discarded, reported (reuses `OutfitRuleContentService.Validate`) |
| Task without name, duplicate id | Discarded, reported; numbers clamped |
| Config entry pointing at a missing wardrobe item, duplicate | Discarded, reported |
| Preset item ids not in the wardrobe | Trimmed, reported; preset discarded if none remain |
| Permission kind unknown or duplicated | Discarded, reported |

Rules are validated against **tags**, not ids (`OutfitRuleContentService.Validate`).

Ranges live in one table (`GameContractService.Ranges`/`Pairs`) and mirror the limits of the Settings inputs; fields
the UI leaves unbounded above get a generous ceiling. A test fails if a numeric game-rule setting has no entry, so
new settings cannot bypass validation. *Known ceiling:* the table duplicates the UI limits; the upgrade path is to
make the Settings views read it.

## Export / import UI

Settings → General → *Contract* has *Export Contract* and *Import Contract*.

- **Export** builds from the **saved** profile (unsaved edits on screen are not included; the status says so) and
  writes a single file. `ContractFileDialogs` forces the `.contract.json` extension.
- **Import** and *Load another contract* share `ContractImportFlow`: pick file → read → validate → replacement
  confirmation (`dialogOverlay` / `dialogBox` via `ConfirmDialogViewModel`) → apply → report. The confirmation lists
  what will be replaced, the corrections found while reading, and — in Settings — a warning that unsaved edits will
  be lost. Cancelling changes nothing.
- After a successful import the cached Settings screen is rebuilt or invalidated so it never shows old values.

## Default contracts (seed)

On first run (wardrobe, outfit rules and tasks all empty) `DataSeeder.SeedIfEmpty` loads
`Content/{language}/default.contract.json` for the resolved UI language and applies it through the same
`Validate` + `Apply` path as an import. If the file is missing, unreadable or fatally invalid, the in-code
`MinimalContentSeeder.Contract` is applied instead. A test checks that every shipped seed validates with no issues.
To change the default content, edit the profile in Settings and export it over the seed file.

The former three-file content import/export (wardrobe, outfit rules, tasks) was removed: the contract is a superset.

## Styling

Reuses `card` conventions, `Primary`/`Ghost`, `dialogOverlay`/`dialogBox` and existing typography (`PageTitle`,
`SectionTitle`, `Body`, `EmployerVoice`); no new font usage. Added to `Assets/Styles/`:

- `Border.contractPaper`, `Border.contractActionBar` (`Components.axaml`)
- `Button.Quiet` (`Buttons.axaml`) — borderless, muted, still 44 px tall
- `ContractPaperMaxWidth` (`Spacing.axaml`)

Touch: every action is at least 44 px tall; the bar wraps in portrait; nothing depends on hover.

## Localization

English first, Italian drafted. Every Italian contract string carries `NEEDS REVIEW: Italian draft` as a resx
`<comment>` — **the Italian copy has not been reviewed**. Known limits:

- the layer names in the wardrobe summary come from `WardrobeLayer.DisplayName`, which is hardcoded English
  (as in Settings);
- durations use whole units with singular/plural keys (`ContractSecondOne`/`Many`, …); ranges such as "between 1
  and 1 lines" are not special-cased.

## Open items

- Italian review of all `Contract*`, `SettingsContract*` and related strings.
- `docs/specs/fortune-deck.md` (≈ lines 71 and 76) is stale: it cites `ForcedOutfitMaxSessions`, which does not exist
  (the code uses `ForcedOutfitMaxHours`). Not touched here.
- Starting a game has pre-existing quirks that *Accept* deliberately keeps: the save is created without going
  through `MainWindowViewModel.OpenSaveSlot`, the slot name (`save_yyyyMMdd_HHmm`) can collide within a minute, and
  the 5-slot limit is only checked in `WelcomeViewModel`.
- `SummonPhraseBank` strings break the tone guide (exclamation marks, informal Italian) and interpolate appellatives
  into sentences. Unrelated to the contract.
