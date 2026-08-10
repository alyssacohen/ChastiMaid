using ChastiMaid.Models;
using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class OutfitBuilderTests
{
    private static ClothingItem MakeItem(int id, string name, int[] layers, string[] slots, double weight = 1.0,
        string[]? tags = null)
    {
        return new ClothingItem
        { Id = id, Name = name, Layers = [..layers], Slots = [..slots], Weight = weight, Tags = [..tags ?? []] };
    }

    private static OutfitRule Requires(string sourceTag, string requiredTag)
    {
        return new OutfitRule { SourceTag = sourceTag, RelatedTag = requiredTag, Relation = OutfitRelation.Requires };
    }

    private static OutfitRule Incompatible(string sourceTag, string otherTag)
    {
        return new OutfitRule
        { SourceTag = sourceTag, RelatedTag = otherTag, Relation = OutfitRelation.Incompatible };
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
        var shirt = MakeItem(1, "shirt", [3], ["torso"], tags: ["shirt"]);
        var jacket = MakeItem(2, "jacket", [3], ["torso"], tags: ["jacket"]);
        var rules = new List<OutfitRule> { Incompatible("shirt", "jacket") };

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
        var girdle = MakeItem(1, "girdle", [0], ["waist"], tags: ["girdle"]);
        var stockings = MakeItem(2, "stockings", [2], ["legs"], tags: ["stockings"]);
        var rules = new List<OutfitRule> { Requires("girdle", "stockings") };

        var builder = new OutfitBuilder([girdle, stockings], rules, 0);
        builder.AddSeedItems([girdle]);
        var outfit = builder.Build();

        var ids = outfit.Items.Select(i => i.Id).ToHashSet();
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void Build_RequiresRule_RequiredItemPresent_WhenItemPickedByRandomFill()
    {
        // No seeds at all: girdle can only end up in the outfit via the random-fill step,
        // which used to bypass ResolveRequiredRules entirely.
        var girdle = MakeItem(1, "girdle", [0], ["waist"], tags: ["girdle"]);
        var stockings = MakeItem(2, "stockings", [2], ["legs"], tags: ["stockings"]);
        var rules = new List<OutfitRule> { Requires("girdle", "stockings") };

        for (var seed = 0; seed < 30; seed++)
        {
            var builder = new OutfitBuilder([girdle, stockings], rules, seed);
            var outfit = builder.Build();

            var ids = outfit.Items.Select(i => i.Id).ToHashSet();
            if (ids.Contains(1))
            {
                Assert.True(ids.Contains(2), $"girdle present without required stockings (seed {seed})");
            }
        }
    }

    [Fact]
    public void Build_RequiresRule_FillerDropped_WhenRequiredHasNoFreeSlot()
    {
        // pants is a seed that permanently occupies the only slot stockings could use,
        // so girdle's requirement can never be satisfied: girdle itself must be dropped
        // if it's ever picked up as a random filler.
        var girdle = MakeItem(1, "girdle", [0], ["waist"], tags: ["girdle"]);
        var stockings = MakeItem(2, "stockings", [2], ["legs"], tags: ["stockings"]);
        var pants = MakeItem(3, "pants", [2], ["legs"]);
        var rules = new List<OutfitRule> { Requires("girdle", "stockings") };

        for (var seed = 0; seed < 30; seed++)
        {
            var builder = new OutfitBuilder([girdle, stockings, pants], rules, seed);
            builder.AddSeedItems([pants]);
            var outfit = builder.Build();

            var ids = outfit.Items.Select(i => i.Id).ToHashSet();
            Assert.Contains(3, ids);
            Assert.False(ids.Contains(1), $"girdle present despite unmet requires (seed {seed})");
        }
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
