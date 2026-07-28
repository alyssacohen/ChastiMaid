using ChastiMaid.Models;

namespace ChastiMaid.Services.Content;

/// <summary>
///     In-code, filesystem-independent fallback content used when a language content file is
///     missing or fails to parse at startup. Must never depend on the filesystem.
/// </summary>
public static class MinimalContentSeeder
{
    public static List<ClothingItem> Wardrobe =>
    [
        new() { Id = 1, Name = "plain dress", Layers = [3], Slots = ["torso", "legs"], Multiplier = 1.0, Weight = 1.0, Tags = [] },
    ];

    // A single wardrobe item cannot form a meaningful requires/incompatible pair.
    public static List<OutfitRule> OutfitRules => [];

    public static List<Chore> Tasks =>
    [
        new() { Id = 1, Name = "Dusting", Description = "", Value = 10, FrequencyDays = 1, MinCompletionMinutes = 5 },
    ];
}
