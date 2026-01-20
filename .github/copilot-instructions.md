# ChastiMaid Copilot Instructions

## Project Overview
ChastiMaid is a .NET 8 game application with two projects:
- **ChastiMaid.Core**: Business logic, data models, and services (class library targeting net8.0)
- **ChastiMaid.UI**: Desktop UI built with Avalonia (WPF-like XAML framework, targeting net8.0)

The application manages a wardrobe system with daily outfit generation, game state progression, and encrypted LiteDB storage.

## Architecture & Key Components

### Core Layers
**DatabaseService** ([ChastiMaid.Core/Services/DatabaseService.cs](ChastiMaid.Core/Services/DatabaseService.cs))
- Manages LiteDB connection with AES encryption
- Returns typed collections: `OutfitItem`, `OutfitRule`, `GameState`
- Initializes indexes for `Name` (items) and `ItemId` (rules)
- Connection string includes password: `Password={password};Connection=shared`

**GameEngine** ([ChastiMaid.Core/Services/GameEngine.cs](ChastiMaid.Core/Services/GameEngine.cs))
- Tracks session timing with `Stopwatch`
- Manages progression (Level, TotalExperience, TotalPlayTime)
- `UpdateTotalTime()` persists elapsed time to `GameState` and restarts timer
- Prevents cheating by encrypting `GameState` in database

**WardrobeService** ([ChastiMaid.Core/Services/WardrobeService.cs](ChastiMaid.Core/Services/WardrobeService.cs))
- `GetTodaysOutfit()`: Retrieves or auto-generates daily outfit using `DailyOutfit` collection
- `ForceRegenerate(date)`: Overwrites existing outfit for a date
- Applies `OutfitRule` constraints during generation
- Respects item `Weight` and `Multiplier` properties for selection probability

### Data Models (LiteDB Collections)
- **OutfitItem**: Clothing pieces with `Name`, `Weight` (selection bias), `Multiplier`, `Layers`, `Slots`, `Tags`
- **OutfitRule**: Constraints between items (`ItemId` field indexed for queries)
- **GameState**: Singleton game progress (Id, Level, TotalExperience, TotalPlayTime, CurrentTaskName)
- **DailyOutfit**: Date-based outfit snapshot with ItemIds

### UI Layer (Avalonia MVVM)
**Architecture Pattern**: CommunityToolkit.MVVM with:
- `@ObservableProperty` attributes for automatic change notification
- `@RelayCommand` for button commands
- ViewModels inherit from `ViewModelBase`

**Key ViewModels**:
- `MainViewModel`: Page navigation container (Intro → Settings → Dashboard)
- `DashboardViewModel`: Session gameplay display
- `WardrobeViewModel`: Wardrobe management

**Navigation Flow**: `MainWindow` → `MainViewModel` → `CurrentPage` binding switches between views

## Data Persistence & Seeding

**DataSeeder** ([ChastiMaid.Core/Helpers/DataSeeder.cs](ChastiMaid.Core/Helpers/DataSeeder.cs))
- Loads initial `OutfitItem` data from `/seed.json` only if `clothing_items` collection is empty
- Falls back to empty array if seed.json missing (in Release deployments)
- Uses `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`

**Database Location**: `{BaseDirectory}/Data/GameData.db` (created if missing)

## Build & Deployment

**Solution**: `ChastiMaid.slnx`

**UI Project Specifics**:
- Self-contained exe: `SelfContained=true`, `PublishSingleFile=false` (includes runtime dependencies)
- Windows-only: `RuntimeIdentifier=win-x64`
- Avalonia version 11.3.10 with Fluent theme
- Compiled bindings enabled: `AvaloniaUseCompiledBindingsByDefault=true`

**Core Project**:
- Nullable reference types enabled: `Nullable=enable`
- Implicit usings: `ImplicitUsings=enable`
- Dependencies: CommunityToolkit.Mvvm 8.4.0, LiteDB 5.0.21

## Critical Conventions

1. **ObservableObject Pattern**: All UI-bound models use `@ObservableProperty` on private fields (CommunityToolkit.Mvvm)
2. **LiteDB ObjectId**: Use `ObjectId.NewObjectId()` for auto-generated PKs, NOT int/Guid
3. **Database Context**: Services use `using var db = dbService.GetDatabase()` pattern (connection pooling)
4. **Tags as HashSet**: Item `Tags` stored as `HashSet<string>` with `TagsText` string helper for UI binding
5. **Date Comparison**: Strip time part: `DateTime.Today` not `DateTime.Now` for daily outfit lookups
6. **Encryption**: All sensitive state encrypted via LiteDB password at database level, never individual fields
7. **Avalonia Views**: Pair `.axaml` UI with `.axaml.cs` code-behind; use `DependentUpon` in csproj

## Common Tasks

**Adding a new outfit rule constraint**:
1. Update `OutfitRule` model in [ChastiMaid.Core/Models](ChastiMaid.Core/Models)
2. Modify wardrobe generation logic in `WardrobeService.GenerateAndSaveNewOutfit()`
3. Add index to `DatabaseService.InitializeIndexes()` if querying new field

**Extending GameState progression**:
1. Add property to [GameState.cs](ChastiMaid.Core/Models/GameState.cs)
2. Update `GameEngine.UpdateTotalTime()` or add new method
3. Update UI ViewModel binding via `@ObservableProperty`

**Adding UI page**:
1. Create `.axaml` + `.axaml.cs` in [Views/](ChastiMaid.UI/Views/)
2. Create corresponding ViewModel in [ViewModels/](ChastiMaid.UI/ViewModels/)
3. Add `@RelayCommand` in `MainViewModel` to navigate to it
4. Update seed.json if needed for outfit data

## Notes for AI Agents

- **Core is stateless**: Services accept dependencies (DatabaseService, GameState) rather than holding state
- **No direct database calls in ViewModels**: Inject services and call their methods
- **Avalonia XAML bindings**: Use `{Binding PropertyName}` syntax; property names case-sensitive
- **Password management**: Database password passed to `DatabaseService` constructor (currently hardcoded, could be config)
- **No async**: Current codebase uses synchronous LiteDB calls; maintain consistency unless refactoring
