using ChastiMaid.Localization;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

/// <summary>
///     In-code, filesystem-independent fallback contract used when a language's seed contract is
///     missing or fails validation at startup. Must never depend on the filesystem. Its texts are translated
///     once, into the language active when the profile is seeded, and saved as ordinary editable data.
/// </summary>
public static class MinimalContentSeeder
{
    public static GameContract Contract(ILocalizer l) => new()
    {
        Settings = new GameSettings(),
        Wardrobe =
        [
            new()
            {
                // translators: default wardrobe item, and the body areas (slots) it covers; editable by the player
                Id = 1, Name = l.T("plain dress"), Layers = [3], Slots = [l.T("chest"), l.T("legs")], Multiplier = 1.0,
                Weight = 1.0, Tags = [],
            },
        ],
        // A single wardrobe item cannot form a meaningful requires/incompatible pair.
        OutfitRules = [],
        Tasks =
        [
            // translators: default chore name; editable by the player
            new() { Id = 1, Name = l.T("Dusting"), Description = "", Value = 10, FrequencyDays = 1, MinCompletionMinutes = 5 },
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
