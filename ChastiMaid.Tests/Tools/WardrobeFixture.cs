using ChastiMaid.Models;

namespace ChastiMaid.Tests.Tools;

/// <summary>
///     Standalone copy of the wardrobe/rules seeded by <c>DataSeeder</c>, so statistical
///     simulations can run against realistic data without a LiteDB-backed ProfileRepository.
/// </summary>
public static class WardrobeFixture
{
    public static (List<ClothingItem> Items, List<OutfitRule> Rules) CreateSeedWardrobe()
    {
        var items = new List<ClothingItem>
        {
            new() { Id = 1, Name = "panties", Layers = [0], Slots = ["crotch"], Multiplier = 1.0, Weight = 1.0 },
            new() { Id = 2, Name = "bra", Layers = [0], Slots = ["torso"], Multiplier = 1.0, Weight = 1.0 },
            new() { Id = 3, Name = "girdle", Layers = [0], Slots = ["waist"], Multiplier = 1.2, Weight = 0.5 },
            new() { Id = 4, Name = "diaper", Layers = [0], Slots = ["crotch"], Multiplier = 1.3, Weight = 0.7 },
            new()
            {
                Id = 5, Name = "body with suspenders", Layers = [0], Slots = ["torso", "waist", "crotch"],
                Multiplier = 1.4, Weight = 0.9,
            },
            new() { Id = 6, Name = "pantyhose", Layers = [2], Slots = ["legs"], Multiplier = 1.1, Weight = 1.2 },
            new()
            {
                Id = 7, Name = "nylon catsuit", Layers = [2], Slots = ["torso", "legs", "arms"], Multiplier = 1.3,
                Weight = 0.7,
            },
            new()
            {
                Id = 8, Name = "black stockings", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.1, Weight = 1.0,
            },
            new()
            {
                Id = 9, Name = "nude stockings", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.1, Weight = 1.0,
            },
            new()
            {
                Id = 10, Name = "sheer black stockings", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.2,
                Weight = 0.8,
            },
            new()
            {
                Id = 11, Name = "chevron socks", Layers = [2, 1], Slots = ["legs"], Multiplier = 1.0, Weight = 1.0,
            },
            new()
            {
                Id = 12, Name = "short socks with bow", Layers = [2, 1], Slots = ["legs"], Multiplier = 0.9,
                Weight = 1.2,
            },
            new()
            {
                Id = 13, Name = "short maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.5, Weight = 1.1,
            },
            new()
            {
                Id = 14, Name = "long maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.5, Weight = 1.0,
            },
            new()
            {
                Id = 15, Name = "latex maid uniform", Layers = [3], Slots = ["torso", "legs", "arms"],
                Multiplier = 1.8, Weight = 0.7,
            },
            new()
            {
                Id = 16, Name = "simple apron", Layers = [3], Slots = ["torso"], Multiplier = 0.8, Weight = 0.8,
            },
            new()
            {
                Id = 17, Name = "maid headband", Layers = [4], Slots = ["head"], Multiplier = 1.0, Weight = 1.2,
            },
            new() { Id = 18, Name = "ribbon choker", Layers = [4], Slots = ["neck"], Multiplier = 1.1, Weight = 0.9 },
            new() { Id = 19, Name = "pearl necklace", Layers = [4], Slots = ["neck"], Multiplier = 1.3, Weight = 0.7 },
            new() { Id = 20, Name = "black heels", Layers = [4], Slots = ["feet"], Multiplier = 1.2, Weight = 1.0 },
            new() { Id = 21, Name = "ballet flats", Layers = [4], Slots = ["feet"], Multiplier = 0.9, Weight = 1.2 },
        };

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

        return (items, rules);
    }
}
