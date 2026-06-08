---
inclusion: always
---

# Tech Stack & Conventions

## Platform & Runtime
- .NET 10.0 (`net10.0`), Windows desktop (`WinExe`)
- Nullable reference types enabled
- Implicit usings enabled
- Compiled bindings enabled (`AvaloniaUseCompiledBindingsByDefault=true`)

## UI Framework
- Avalonia UI 12.0.1 — cross-platform XAML UI framework
- Fluent theme (`Avalonia.Themes.Fluent`)
- Inter font (`Avalonia.Fonts.Inter`)
- Views use `.axaml` file extension
- Dark theme (`RequestedThemeVariant="Dark"`)

## MVVM & State Management
- ReactiveUI 23.2.1 — MVVM framework
- ReactiveUI.Avalonia 11.4.12
- ReactiveUI.SourceGenerators 2.6.1 — compile-time source generation for reactive properties

## Data Layer
- LiteDB 5.0.21 — embedded NoSQL document database
- Databases are encrypted with a derived password (SHA-256 of a static key)
- Data stored under `data/` relative to the executable; saves under `data/saves/`

## Common Commands
```bash
dotnet build        # Build
dotnet run          # Run
dotnet publish -c Release  # Publish
```

## Architecture Patterns

### MVVM
- All ViewModels inherit `ViewModelBase` (extends `ReactiveObject`)
- `ViewLocator` maps ViewModels to Views by replacing `ViewModel` with `View` in the fully qualified type name
- Navigation via `MainWindowViewModel.CurrentPage` property, rendered by `TransitioningContentControl`
- Navigate by calling `_mainVm.NavigateTo(new SomeViewModel(...))`

### Reactive Properties
- Use `this.RaiseAndSetIfChanged(ref _field, value)` for observable properties with a private backing field
- Use `ReactiveCommand.Create(...)` or `ReactiveCommand.CreateFromTask(...)` for commands
- Expose commands as `ICommand` properties

### Services
- `DatabaseService` — manages encrypted LiteDB connections (profile DB + per-slot save DBs); implements `IDisposable`
- `ProfileRepository(LiteDatabase)` — CRUD for profile-scoped data (wardrobe, chores, outfit rules, punishment templates, player profile, stats)
- `SaveRepository(LiteDatabase)` — CRUD for per-save data (game state, sessions, active punishments, snapshotted copies of profile data)
- `SaveSlotManager(DatabaseService)` — lists and deletes save slots
- `DataSeeder` — static class that seeds default profile data on first run
- `OutfitBuilder` — builds randomized outfits from wardrobe items respecting rules (requires/incompatible), layer slots, and weighted selection
- Repositories use primary constructor syntax: `public class FooRepository(LiteDatabase db)`

### Models
- All persisted models use `[BsonId] public int Id { get; set; }` for LiteDB identity
- Singleton documents (one per DB) use `Id = 1` by convention
- Enums defined alongside their primary model (e.g. `OutfitRelation` in `OutfitRule.cs`)
- Use `record` types for immutable value objects and lightweight view-layer DTOs (e.g., `DifficultyConfig`, `ChoreItemViewModel`, `SaveSlotInfo`)

### Styling
- Global styles in `GlobalStyles.axaml`, included via `StyleInclude` in `App.axaml`
- Font sizes are dynamically scaled based on primary screen height (reference: 1080p) and stored as `DynamicResource` keys: `FontSizeBody`, `FontSizeSmall`, `FontSizeButton`, `FontSizeHeading`, `FontSizeDisplay`
- Font names: DisplayFont (Instrument Serif), HeadingFont (Instrument Serif Italic), DefaultFont (Inter Light)
- Use style classes for variants: `TextBlock.heading`, `TextBlock.display`, `Button.primary`, `Button.compact`

## Code Style Rules
- Use file-scoped namespaces
- Use target-typed `new()` and collection expressions (`[]`) where possible
- Use `is not null` / `is null` over `!= null` / `== null`
- Use pattern matching and `switch` expressions
- Prefer `var` for local variables when the type is obvious
- Keep ViewModels focused: business logic belongs in Services or Models, not in ViewModels
- Translate comments in Italian to English whenever is possible

## Notes
- `Avalonia.Diagnostics` is included only in Debug builds
- App entry point: `Program.cs` → `BuildAvaloniaApp()` → `StartWithClassicDesktopLifetime`
- App initialization in `App.axaml.cs` wires up `DatabaseService`, `ProfileRepository`, `SaveSlotManager`, seeds data, and sets `MainWindowViewModel` as the root DataContext
- `DatabaseService` is disposed on application exit
