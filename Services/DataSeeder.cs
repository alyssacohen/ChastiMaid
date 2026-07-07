using ChastiMaid.Models;
using ChastiMaid.Repositories;

namespace ChastiMaid.Services;

/// <summary>
///     Seeds the profile database with default data on first run.
/// </summary>
public static class DataSeeder
{
    public static void SeedIfEmpty(ProfileRepository profile)
    {
        if (profile.GetWardrobe().Count > 0)
        {
            return; // already seeded
        }

        var items = new List<ClothingItem>
        {
            new()
            {
                Id = 1, Name = "panties", Layers = [0], Slots = ["crotch"], Multiplier = 1.0,
                Weight = 1.0, Tags = ["lingerie"],
            },
            new()
            {
                Id = 2, Name = "bra", Layers = [0], Slots = ["torso"], Multiplier = 1.0, Weight = 1.0,
                Tags = ["lingerie"],
            },
            new()
            {
                Id = 3, Name = "girdle", Layers = [0], Slots = ["waist"], Multiplier = 1.2, Weight = 0.5,
                Tags = ["retro", "tight"],
            },
            new()
            {
                Id = 4, Name = "diaper", Layers = [0], Slots = ["crotch"], Multiplier = 1.3, Weight = 0.7,
                Tags = ["fetish", "bulky"],
            },
            new()
            {
                Id = 5, Name = "body with suspenders", Layers = [0], Slots = ["torso", "waist", "crotch"],
                Multiplier = 1.4, Weight = 0.9, Tags = ["lingerie", "suspenders"],
            },
            new()
            {
                Id = 6, Name = "pantyhose", Layers = [2], Slots = ["legs"], Multiplier = 1.1, Weight = 1.2,
                Tags = ["sheer"],
            },
            new()
            {
                Id = 7, Name = "nylon catsuit", Layers = [2], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.3, Weight = 0.7, Tags = ["tight", "shiny"],
            },
            new()
            {
                Id = 8, Name = "black stockings", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.1,
                Weight = 1.0, Tags = ["suspenders", "black"],
            },
            new()
            {
                Id = 9, Name = "nude stockings", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.1,
                Weight = 1.0, Tags = ["suspenders", "beige"],
            },
            new()
            {
                Id = 10, Name = "sheer black stockings", Layers = [2, 1], Slots = ["legs"],
                Multiplier = 1.2, Weight = 0.8, Tags = ["suspenders", "sheer", "black"],
            },
            new()
            {
                Id = 11, Name = "chevron socks", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.0,
                Weight = 1.0, Tags = ["patterned", "warm"],
            },
            new()
            {
                Id = 12, Name = "short socks with bow", Layers = [2, 1], Slots = ["legs"],
                Multiplier = 0.9, Weight = 1.2, Tags = ["casual", "cute"],
            },
            new()
            {
                Id = 13, Name = "short maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.5, Weight = 1.1, Tags = ["maid", "dress"],
            },
            new()
            {
                Id = 14, Name = "long maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.5, Weight = 1.0, Tags = ["maid", "dress"],
            },
            new()
            {
                Id = 15, Name = "latex maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.8, Weight = 0.7, Tags = ["maid", "latex"],
            },
            new()
            {
                Id = 16, Name = "simple apron", Layers = [3], Slots = ["torso"], Multiplier = 0.8,
                Weight = 0.8, Tags = ["apron", "casual"],
            },
            new()
            {
                Id = 17, Name = "maid headband", Layers = [4], Slots = ["head"], Multiplier = 1.0,
                Weight = 1.2, Tags = ["maid", "headwear"],
            },
            new()
            {
                Id = 18, Name = "ribbon choker", Layers = [4], Slots = ["neck"], Multiplier = 1.1,
                Weight = 0.9, Tags = ["cute", "maid"],
            },
            new()
            {
                Id = 19, Name = "pearl necklace", Layers = [4], Slots = ["neck"], Multiplier = 1.3,
                Weight = 0.7, Tags = ["elegant"],
            },
            new()
            {
                Id = 20, Name = "black heels", Layers = [4], Slots = ["feet"], Multiplier = 1.2,
                Weight = 1.0, Tags = ["maid", "heels"],
            },
            new()
            {
                Id = 21, Name = "ballet flats", Layers = [4], Slots = ["feet"], Multiplier = 0.9,
                Weight = 1.2, Tags = ["casual"],
            },
        };

        foreach (var item in items)
        {
            profile.SaveClothingItem(item);
        }

        var rules = new List<OutfitRule>
        {
            new() { Id = 1, ItemId = 3, RelatedItemId = 8, Relation = OutfitRelation.Requires },
            new() { Id = 2, ItemId = 3, RelatedItemId = 9, Relation = OutfitRelation.Requires },
            new() { Id = 3, ItemId = 3, RelatedItemId = 10, Relation = OutfitRelation.Requires },
            new() { Id = 4, ItemId = 5, RelatedItemId = 8, Relation = OutfitRelation.Requires },
            new() { Id = 5, ItemId = 5, RelatedItemId = 9, Relation = OutfitRelation.Requires },
            new() { Id = 6, ItemId = 5, RelatedItemId = 10, Relation = OutfitRelation.Requires },
            new() { Id = 7, ItemId = 4, RelatedItemId = 8, Relation = OutfitRelation.Incompatible },
            new() { Id = 8, ItemId = 4, RelatedItemId = 9, Relation = OutfitRelation.Incompatible },
            new() { Id = 9, ItemId = 4, RelatedItemId = 10, Relation = OutfitRelation.Incompatible },
            new() { Id = 10, ItemId = 11, RelatedItemId = 16, Relation = OutfitRelation.Requires },
            new() { Id = 11, ItemId = 12, RelatedItemId = 16, Relation = OutfitRelation.Requires },
        };

        foreach (var rule in rules)
        {
            profile.SaveOutfitRule(rule);
        }

        var chores = new List<Chore>
        {
            new() { Id = 1, Name = "Dusting", Value = 10, FrequencyDays = 1, MinCompletionMinutes = 5 },
            new() { Id = 2, Name = "Sweeping the floor", Value = 15, FrequencyDays = 1, MinCompletionMinutes = 8 },
            new() { Id = 3, Name = "Mopping the floor", Value = 20, FrequencyDays = 7, MinCompletionMinutes = 15 },
            new() { Id = 4, Name = "Making the bed", Value = 5, FrequencyDays = 1, MinCompletionMinutes = 3 },
            new() { Id = 5, Name = "Cooking", Value = 15, FrequencyDays = 1, MinCompletionMinutes = 20 },
            new() { Id = 6, Name = "Cleaning the bathroom", Value = 25, FrequencyDays = 7, MinCompletionMinutes = 20 },
            new() { Id = 7, Name = "Mending socks", Value = 20, FrequencyDays = 7, MinCompletionMinutes = 10 },
        };

        foreach (var chore in chores)
        {
            profile.SaveChore(chore);
        }

        var punishments = new List<PunishmentTemplate>
        {
            new()
            {
                Id = 1,
                Name = "Debt Increase",
                DebtIncrease = 50,
            },
            new()
            {
                Id = 2,
                Name = "Forced Outfit (1 session)",
                OutfitForceDuration = new PunishmentDuration { Sessions = 1 },
                ForceFullOutfit = true,
            },
            new()
            {
                Id = 3,
                Name = "Extra Chore",
                RequiredChoreCount = 1,
            },
        };

        foreach (var punishment in punishments)
        {
            profile.SavePunishmentTemplate(punishment);
        }

        profile.SaveGameSettings(new GameSettings());
    }
}
