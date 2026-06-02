# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ChastiMaid is a gamification desktop app (Avalonia, .NET 10, Windows) where the user role-plays a maid completing chores to pay off a randomly-generated virtual debt. Earned credits reduce the debt; the game ends when the debt is cleared and no punishments are pending.

Detailed domain specs live in `.kiro/steering/` (always-relevant design docs):
- `product.md` — the **employer character voice** (Edwardian, ironic, never scolds). All user-facing copy must match this register.
- `gameplay.md` — game/session/dashboard flow, idle vs. busy mode, popups.
- `wardrobe.md` — outfit generation, layers/slots, the daily lock + bonus/multiplier mechanic.
- `punishments.md` — punishment types and severity scaling.
- `tech.md` — conventions (also summarized below).

## Commands

```powershell
dotnet build               # Build
dotnet run                 # Run (fullscreen desktop app)
dotnet publish -c Release  # Publish (excludes Avalonia.Diagnostics)
```

There is **no test project** — there is no `dotnet test` to run. Verify changes by building and running.

## Architecture

**MVVM via ReactiveUI + ViewLocator convention.**
- All ViewModels extend `ViewModelBase` (→ `ReactiveObject`). `ViewLocator` maps a ViewModel to its View by replacing `ViewModel`→`View` in the type name, so `Views/{Name}View.axaml` must be named to match `ViewModels/{Name}ViewModel.cs`.
- Navigation is **not** a router: `MainWindowViewModel.CurrentPage` (a `ViewModelBase`) is rendered by a `TransitioningContentControl`. Navigate with `mainVm.NavigateTo(new SomeViewModel(...))`. ViewModels receive the `MainWindowViewModel` to navigate and to reach shared services.

**Startup wiring** (`App.axaml.cs` → `OnFrameworkInitializationCompleted`): constructs `DatabaseService`, opens the profile DB, runs `DataSeeder.SeedIfEmpty`, then builds `MainWindowViewModel` with the shared services (`ProfileRepository`, `SaveSlotManager`, `AutoSaveService`, `WebcamPreviewService`). All are disposed on `desktop.Exit`.

**Two-database persistence model (LiteDB, encrypted).**
- `DatabaseService` opens AES-encrypted LiteDB files under `data/` (relative to the exe). Password is derived from a static `KeySource` — **do not change `KeySource`**, it breaks all existing saves.
- **Profile DB** (`data/profile.db`, one): the master/editable data — wardrobe, chores, outfit rules, punishment templates. Accessed via `ProfileRepository`. Edited in Settings.
- **Save DB** (`data/saves/{slot}.db`, up to 5): per-game state plus a **snapshot copy** of wardrobe/rules/chores taken at new-game time (so editing Settings later doesn't alter an in-progress game). Accessed via `SaveRepository`. `MainWindowViewModel.OpenSaveSlot` opens one at a time and disposes the previous.
- Persisted models use `[BsonId] public int Id`. Singleton documents (GameState, GameSettings) use `Id = 1`.

**Session runtime services** (created per active session, hold a `SaveRepository`):
- `SessionLoopService` — the game loop: pays periodic idle wage and fires random-interval check-in popups. `Pause()`/`Resume()` toggle for the idle↔busy transition. Exposes `OnIdleWagePaid`/`OnPopupTriggered` callbacks.
- `AutoSaveService` — `TriggerSave()` for event-driven saves + a periodic timer; `Bind`/`Unbind` the active `SaveRepository`.
- `GameClock` — **all in-game time must go through `GameClock.UtcNow()`/`Today`, never `DateTime.UtcNow` directly.** It applies a test offset (`Advance`/`Reset`) so time-dependent mechanics (daily outfit lock, interest) can be fast-forwarded in test mode.
- `MediaCaptureService` / `WebcamPreviewService` — webcam capture (OpenCvSharp) for the photo/video check-ins; `CryptoService` encrypts captured media and the safe-combination photo.

**OutfitBuilder** builds a randomized outfit from snapshot wardrobe items, honoring requires/incompatible rules, layer probabilities, per-layer slot exclusivity, and weighted selection. Accepting an outfit lowers item `Weight`; rejecting raises it and increments `RejectionCount` (see `SaveRepository.AcceptItems`/`RejectItems`). The effective earning multiplier is `OutfitMultiplier × OutfitBonusPercent`.

## Conventions

### Language features
- File-scoped namespaces everywhere (no exceptions).
- Target-typed `new()` and collection expressions `[]` for initializers.
- `is null` / `is not null` instead of `== null` / `!= null`.
- Pattern matching and `switch` expressions over if/else chains.
- `var` when the type is obvious from the right-hand side.

### Classes and constructors
- **Repositories and stateless managers use primary-constructor syntax**: `ProfileRepository(LiteDatabase db)`, `SaveRepository(LiteDatabase db)`, `SaveSlotManager(DatabaseService dbService)`.
- **Stateful/lifecycle services use traditional constructors** — they own fields, timers, `CancellationTokenSource`, `IDisposable`: `DatabaseService`, `AutoSaveService`, `SessionLoopService`, `OutfitBuilder`, `WebcamPreviewService`, `MediaCaptureService`. Do not convert these to primary constructors.

### MVVM / ReactiveUI
- ViewModels extend `ViewModelBase` (→ `ReactiveObject`).
- **Reactive properties are written by hand**: a private backing field plus `get => _field; set => this.RaiseAndSetIfChanged(ref _field, value);`.
- Commands: `ReactiveCommand.Create(...)` / `CreateFromTask(...)`, exposed as read-only `ICommand` auto-properties.
- ⚠️ `ReactiveUI.SourceGenerators` is referenced in the `.csproj` but **not yet in use** — no `[Reactive]`/`[ReactiveCommand]` attributes, no `partial` ViewModels. Adoption is planned; until then follow the manual pattern above.
- Keep business logic in Services/Models, not ViewModels. (`WebcamPreviewService` is intentionally a `ReactiveObject` so its preview bitmap can be data-bound.)

### Comments
- Write comments in English. Italian comments have been fully translated; if you spot any stragglers, translate them when you touch the surrounding code.

## Styling

- Global styles in `GlobalStyles.axaml`, included via `StyleInclude` in `App.axaml`. `RequestedThemeVariant="Dark"`.
- Font sizes are scaled at runtime from primary screen height (1080p reference) and exposed as `DynamicResource` keys: `FontSizeBody`, `FontSizeSmall`, `FontSizeButton`, `FontSizeHeading`, `FontSizeDisplay`.
- Fonts: DisplayFont (Instrument Serif), HeadingFont (Instrument Serif Italic), DefaultFont (Inter Light).
- Use style classes for variants, e.g. `TextBlock.heading`, `TextBlock.display`, `Button.primary`, `Button.compact`. Icons via FluentIcons.Avalonia.
