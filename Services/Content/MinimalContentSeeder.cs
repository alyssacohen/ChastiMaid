using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

/// <summary>
///     In-code, filesystem-independent fallback contract used when a language's seed contract is
///     missing or fails validation at startup. Must never depend on the filesystem.
/// </summary>
public static class MinimalContentSeeder
{
    public static GameContract Contract => new()
    {
        Settings = new GameSettings(),
        Wardrobe =
        [
            new() { Id = 1, Name = "plain dress", Layers = [3], Slots = ["chest", "legs"], Multiplier = 1.0, Weight = 1.0, Tags = [] },
        ],
        // A single wardrobe item cannot form a meaningful requires/incompatible pair.
        OutfitRules = [],
        Tasks =
        [
            new() { Id = 1, Name = "Dusting", Description = "", Value = 10, FrequencyDays = 1, MinCompletionMinutes = 5 },
        ],
        ForcedOutfitPresets = [],
        PlugAndPlayItems = [],
        DiaperPlayItems = [],
        ChastityDeviceItems = [],
        PermissionTypes =
        [
            new() { Kind = PermissionKind.Pee, BaseGrantPercent = 70, DurationMinutes = 5, CooldownMinutes = 10 },
            new() { Kind = PermissionKind.Poop, BaseGrantPercent = 50, DurationMinutes = 8, CooldownMinutes = 10 },
            new() { Kind = PermissionKind.SitDown, BaseGrantPercent = 20, DurationMinutes = 3, CooldownMinutes = 20 },
            new() { Kind = PermissionKind.Smoke, BaseGrantPercent = 20, DurationMinutes = 5, CooldownMinutes = 30 },
            new() { Kind = PermissionKind.TeaCoffee, BaseGrantPercent = 60, DurationMinutes = 10, CooldownMinutes = 30 },
        ],
    };
}
