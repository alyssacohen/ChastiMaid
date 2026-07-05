# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ChastiMaid is a gamification desktop app (Avalonia, .NET 10, Windows) where the user role-plays a maid completing chores
to pay off a randomly-generated virtual debt. Earned credits reduce the debt; the game ends when the debt is cleared and
no punishments are pending.

Detailed domain specs live in `docs/specs/` (always-relevant design docs):

- `product.md` — the **employer character voice** (Edwardian, ironic, never scolds). All user-facing copy must match
  this register.
- `gameplay.md` — game/session/dashboard flow, idle vs. busy mode, popups.
- `wardrobe.md` — outfit generation, layers/slots, the daily lock + bonus/multiplier mechanic.
- `punishments.md` — punishment types and severity scaling.

## Commands

```powershell
dotnet build               # Build
dotnet run                 # Run (fullscreen desktop app)
dotnet test                # Run unit tests (ChastiMaid.Tests project)
dotnet publish -c Release  # Publish (excludes Avalonia.Diagnostics)
```

## Target Platforms

The app runs on two form factors with different constraints:

**Primary: Windows 11 desktop (development machine)**
Full keyboard and mouse, large screen. Used for development and testing.

**Secondary: Surface Go (Windows 10, tablet mode)**

- Screen: 1800×1200 (10"), typically used at 150% DPI scaling → effective ~1200×800
- Input: touch-first; mouse and type cover optional. All interactive elements must be
  large enough for finger tap (minimum 44×44px hit target).
- Orientation: primarily landscape, but portrait must not break layouts.
- Performance: low-power Intel Pentium Gold / 4 GB RAM — avoid heavy background work
  on the UI thread; keep animations simple.
- Cameras: **two cameras** (front-facing and rear-facing). `WebcamPreviewService` supports camera
  enumeration and lets the user select which camera to use. The selected camera is persisted in
  `GameSettings` (`SelectedCameraIndex`).
- No font scaling: instead of scaling font sizes to screen dimensions, layouts use
  generous padding/spacing so they breathe naturally on larger screens. Font size keys
  (`FontSizeBody`, `FontSizeDisplay`, etc.) are fixed constants.

## Architecture

**MVVM via ReactiveUI + ViewLocator convention.**

- All ViewModels extend `ViewModelBase` (→ `ReactiveObject`). `ViewLocator` maps a ViewModel to its View by replacing
  `ViewModel`→`View` in the type name, so `Views/{Name}View.axaml` must be named to match
  `ViewModels/{Name}ViewModel.cs`.
- Navigation is **not** a router: `MainWindowViewModel.CurrentPage` (a `ViewModelBase`) is rendered by a
  `TransitioningContentControl`. Navigate with `mainVm.NavigateTo(new SomeViewModel(...))`. ViewModels receive the
  `MainWindowViewModel` to navigate and to reach shared services.

**Startup wiring** (`App.axaml.cs` → `OnFrameworkInitializationCompleted`): constructs `DatabaseService`, opens the
profile DB, runs `DataSeeder.SeedIfEmpty`, then builds `MainWindowViewModel` with the shared services (
`ProfileRepository`, `SaveSlotManager`, `AutoSaveService`, `WebcamPreviewService`). All are disposed on `desktop.Exit`.

**Two-database persistence model (LiteDB, encrypted).**

- `DatabaseService` opens AES-encrypted LiteDB files under `data/` (relative to the exe). Password is derived from a
  static `KeySource` — **do not change `KeySource`**, it breaks all existing saves.
- **Profile DB** (`data/profile.db`, one): the master/editable data — wardrobe, chores, outfit rules, punishment
  templates. Accessed via `ProfileRepository`. Edited in Settings.
- **Save DB** (`data/saves/{slot}.db`, up to 5): per-game state plus a **snapshot copy** of wardrobe/rules/chores taken
  at new-game time (so editing Settings later doesn't alter an in-progress game). Accessed via `SaveRepository`.
  `MainWindowViewModel.OpenSaveSlot` opens one at a time and disposes the previous.
- Persisted models use `[BsonId] public int Id`. Singleton documents (GameState, GameSettings) use `Id = 1`.

**Session runtime services** (created per active session, hold a `SaveRepository`):

- `SessionLoopService` — the game loop: pays periodic idle wage and fires random-interval check-in popups. `Pause()`/
  `Resume()` toggle for the idle↔busy transition. Exposes `OnIdleWagePaid`/`OnPopupTriggered` callbacks.
- `AutoSaveService` — `TriggerSave()` for event-driven saves + a periodic timer; `Bind`/`Unbind` the active
  `SaveRepository`.
- `GameClock` — **all time-dependent game mechanics (daily outfit lock, interest, idle wage, popup timing) must go
  through `GameClock.UtcNow()`/`Today`, never `DateTime.UtcNow` directly.** It applies a test offset
  (`Advance`/`Reset`) so these mechanics can be fast-forwarded in test mode. Timestamps used only for naming files
  (e.g. capture filenames, save-slot names) are not game mechanics and may use `DateTime.Now` directly.
- `WebcamPreviewService` — webcam capture (OpenCvSharp) for the photo/video check-ins, including camera enumeration
  and selection; `CryptoService` encrypts captured media and the safe-combination photo.

**OutfitBuilder** builds a randomized outfit from snapshot wardrobe items, honoring requires/incompatible rules, layer
probabilities, per-layer slot exclusivity, and weighted selection. Accepting an outfit lowers item `Weight`; rejecting
raises it and increments `RejectionCount` (see `SaveRepository.AcceptItems`/`RejectItems`). The effective earning
multiplier is `OutfitMultiplier × OutfitBonusPercent`.

## Conventions

### Language features

- File-scoped namespaces everywhere (no exceptions).
- Target-typed `new()` and collection expressions `[]` for initializers.
- `is null` / `is not null` instead of `== null` / `!= null`.
- Pattern matching and `switch` expressions over if/else chains.
- `var` when the type is obvious from the right-hand side.

### Classes and constructors

- **Repositories and stateless managers use primary-constructor syntax**: `ProfileRepository(LiteDatabase db)`,
  `SaveRepository(LiteDatabase db)`, `SaveSlotManager(DatabaseService dbService)`.
- **Stateful/lifecycle services use traditional constructors** — they own fields, timers, `CancellationTokenSource`,
  `IDisposable`: `DatabaseService`, `AutoSaveService`, `SessionLoopService`, `OutfitBuilder`, `WebcamPreviewService`.
  Do not convert these to primary constructors.

### MVVM / ReactiveUI

- ViewModels extend `ViewModelBase` (→ `ReactiveObject`).
- **Reactive properties are written by hand**: a private backing field plus
  `get => _field; set => this.RaiseAndSetIfChanged(ref _field, value);`.
- Commands: `ReactiveCommand.Create(...)` / `CreateFromTask(...)`, exposed as read-only `ICommand` auto-properties.
- Use of `ReactiveUI.SourceGenerators`. Is already referenced in the `.csproj.
- Keep business logic in Services/Models, not ViewModels. (`WebcamPreviewService` is intentionally a `ReactiveObject` so
  its preview bitmap can be data-bound.)

### Comments

- Write comments in English. Italian comments have been fully translated; if you spot any stragglers, translate them
  when you touch the surrounding code.

## Styling

**Warm light theme only.** `RequestedThemeVariant="Light"`. No dark theme, no conditional palettes.

Style files live in `Assets/Styles/` and are loaded by `App.axaml`:

| File               | Root element         | Contents                                                                                                               |
|--------------------|----------------------|------------------------------------------------------------------------------------------------------------------------|
| `Colors.axaml`     | `ResourceDictionary` | All `Color` and `SolidColorBrush` resources                                                                            |
| `Spacing.axaml`    | `ResourceDictionary` | Spacing scale + layout constants                                                                                       |
| `Typography.axaml` | `Styles`             | `TextBlock.*` style classes                                                                                            |
| `Buttons.axaml`    | `Styles`             | `Button.*` style classes                                                                                               |
| `Chips.axaml`      | `Styles`             | `Border.chip` + `TextBlock.chip-label` styles                                                                          |
| `Tables.axaml`     | `Styles`             | `Grid.TableRow.*` styles                                                                                               |
| `Components.axaml` | `Styles`             | Layout classes: `Panel.dialogOverlay`, `Border.dialogBox`, `Border.card`, `ToggleButton.filterChip`, `Button.slotCard` |

**Colors.axaml** is always loaded first (others reference its brushes). `Colors.axaml` must stay first in
`MergedDictionaries`.

**No inline styles.** Never hardcode colors, font sizes, or spacing directly in views. All tokens live in
`Assets/Styles/`. Views assign classes and reference resources only.

**No font scaling.** Typography uses fixed pixel values.

**Font families** — referenced as embedded Avalonia assets, not installed system fonts:

- `Cormorant Garamond` — display serif, used in `PageTitle`, `SectionTitle`, `EmployerVoice` only. Files live in
  `Assets/Fonts/CormorantGaramond/`; registered in `Typography.axaml` via URI
  `avares://ChastiMaid/Assets/Fonts/CormorantGaramond#Cormorant Garamond`.
- `Inter` — all other UI text, provided by the `Avalonia.Fonts.Inter` NuGet package (auto-registered).

### Typography classes (`TextBlock`)

`PageTitle` · `SectionTitle` · `Heading` · `Subheading` · `Body` · `BodySmall` · `Caption` · `LabelUI` · `NumberLarge` ·
`EmployerVoice`

`LabelUI` text is written in natural case; uppercasing is applied at render time via `{conv:UpperCase ...}`
(`Views/Converters/UpperCaseExtension.cs`) for literals or `UpperCaseConverter` for bindings — never hardcode
ALLCAPS text.

### Button classes

`Primary` · `Ghost` · `Danger` · `SmallGhost` · `Amber` · `Emergency` · `Delete` · `Add` · `SlotCard`

Modifier: `compact` (reduces height to 32px; combinable: `Classes="Danger compact"`).

### Color brush keys

**Surfaces:** `BrushWindowBackground` · `BrushSurfaceBase` · `BrushSurfaceSubtle` · `BrushSurfaceMuted`

**Text:** `TextPrimary` · `TextSecondary` · `TextMuted` · `TextAccent`

**Interactive:** `BrushButtonPrimary` · `BrushButtonPrimaryText` · `BrushButtonDanger` · `BrushBorderSubtle` ·
`BrushChipDefault` · `BrushChipSelected` · `BrushChipRequired`

**Semantic:** `BrushAmber` · `BrushAmberBackground` · `BrushAmberText` · `BrushStatusIdle` · `BrushEmergencyText`

Icons via `FluentIcons.Avalonia`.

### Migration status

All views are fully migrated to the design system. `WelcomeView` is the reference view. When editing any view, use only
the class names and brush keys listed above.
