using ChastiMaid.Models;
using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class OutfitBuilderTests
{
    private static ClothingItem MakeItem(int id, string name, int[] layers, string[] slots, double weight = 1.0)
    {
        return new ClothingItem { Id = id, Name = name, Layers = [..layers], Slots = [..slots], Weight = weight };
    }

    private static OutfitRule Requires(int itemId, int requiredId)
    {
        return new OutfitRule { ItemId = itemId, RelatedItemId = requiredId, Relation = OutfitRelation.Requires };
    }

    private static OutfitRule Incompatible(int itemId, int otherId)
    {
        return new OutfitRule { ItemId = itemId, RelatedItemId = otherId, Relation = OutfitRelation.Incompatible };
    }

    [Fact]
    public void Build_WithNoItems_ReturnsEmptyOutfit()
    {
        var builder = new OutfitBuilder([], [], 42);
        var outfit = builder.Build();

        Assert.Empty(outfit.Items);
    }

    [Fact]
    public void Build_SlotExclusivity_NoDuplicateSlotsOnSameLayer()
    {
        var items = new List<ClothingItem>
        {
            MakeItem(1, "panties", [0], ["crotch"]),
            MakeItem(2, "diaper", [0], ["crotch"]),
            MakeItem(3, "thong", [0], ["crotch"])
        };

        var builder = new OutfitBuilder(items, [], 0);
        var outfit = builder.Build();

        var undiesItems = outfit.Items.Where(i => outfit.LayerOf(i) == 0).ToList();
        var slots = undiesItems.SelectMany(i => i.Slots).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
    }

    [Fact]
    public void Build_IncompatibleRule_NeverBothItemsInOutfit()
    {
        var shirt = MakeItem(1, "shirt", [3], ["torso"]);
        var jacket = MakeItem(2, "jacket", [3], ["torso"]);
        var rules = new List<OutfitRule> { Incompatible(1, 2) };

        // Run 20 times with different seeds to ensure the rule is always respected
        for (var seed = 0; seed < 20; seed++)
        {
            var builder = new OutfitBuilder([shirt, jacket], rules, seed);
            var outfit = builder.Build();
            var ids = outfit.Items.Select(i => i.Id).ToHashSet();
            Assert.False(ids.Contains(1) && ids.Contains(2),
                $"Incompatible items both appeared in outfit (seed {seed})");
        }
    }

    [Fact]
    public void Build_RequiresRule_RequiredItemPresent_WhenSeedAdded()
    {
        var girdle = MakeItem(1, "girdle", [0], ["waist"]);
        var stockings = MakeItem(2, "stockings", [2], ["legs"]);
        var rules = new List<OutfitRule> { Requires(1, 2) };

        var builder = new OutfitBuilder([girdle, stockings], rules, 0);
        builder.AddSeedItems([girdle]);
        var outfit = builder.Build();

        var ids = outfit.Items.Select(i => i.Id).ToHashSet();
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void Build_SeedItem_AlwaysInOutfit()
    {
        var choker = MakeItem(1, "choker", [4], ["neck"]);
        var items = new List<ClothingItem>
        {
            choker,
            MakeItem(2, "heels", [4], ["feet"])
        };

        var builder = new OutfitBuilder(items, [], 999);
        builder.AddSeedItems([choker]);
        var outfit = builder.Build();

        Assert.Contains(outfit.Items, i => i.Id == 1);
    }

    [Fact]
    public void Build_TotalMultiplier_IsProductOfItemMultipliers()
    {
        var item1 = new ClothingItem
            { Id = 1, Layers = [3], Slots = ["torso"], Multiplier = 1.5, Weight = 1 };
        var item2 = new ClothingItem { Id = 2, Layers = [4], Slots = ["neck"], Multiplier = 1.2, Weight = 1 };

        var builder = new OutfitBuilder([item1, item2], [], 0);
        builder.AddSeedItems([item1, item2]);
        var outfit = builder.Build();

        Assert.Equal(1.5 * 1.2, outfit.TotalMultiplier, 5);
    }
}
