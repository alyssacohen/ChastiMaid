# Project Structure

## Root

```
ChastiMaid/
├── Assets/          # Static assets (icons, images)
├── Models/          # Data models (currently empty, folder reserved)
├── Repositories/    # Data-access repositories (ProfileRepository, SaveRepository)
├── Services/        # Business logic services
├── ViewModels/      # ReactiveUI ViewModels
├── Views/           # Avalonia AXAML views + code-behind
│   ├── Controls/    # Reusable UI controls
│   └── Dialogs/     # Modal dialogs
├── docs/            # Design specs and wireframes
│   ├── specs/       # Domain specs (product, gameplay, wardrobe, punishments, tech)
│   └── design/      # UI wireframes
├── App.axaml        # Application definition, theme setup
├── App.axaml.cs     # App startup, main window initialization
├── Program.cs       # Entry point
├── ViewLocator.cs   # Resolves ViewModel → View via naming convention
└── ChastiMaid.csproj
```

## MVVM Conventions

- All ViewModels inherit from `ViewModelBase` (which extends `ReactiveObject`)
- ViewLocator maps ViewModels to Views by replacing `ViewModel` with `View` in the fully qualified type name
    - e.g. `ChastiMaid.ViewModels.WelcomeViewModel` → `ChastiMaid.Views.WelcomeView`
- Each View has a paired `.axaml` + `.axaml.cs` code-behind
- Navigation is handled by `MainWindowViewModel.CurrentPage` (a `ViewModelBase`), rendered via
  `TransitioningContentControl`

## Adding New Pages

1. Create `ViewModels/{Name}ViewModel.cs` extending `ViewModelBase`
2. Create `Views/{Name}View.axaml` + `Views/{Name}View.axaml.cs`
3. Set `MainWindowViewModel.CurrentPage` to an instance of the new ViewModel to navigate to it

## Naming

- ViewModels: `PascalCase` + `ViewModel` suffix
- Views: `PascalCase` + `View` suffix (must match ViewModel name for ViewLocator to work)
- Files follow the same naming as their contained class
