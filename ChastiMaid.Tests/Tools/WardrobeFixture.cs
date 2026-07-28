using ChastiMaid.Models;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Tools;

/// <summary>
///     Loads the same wardrobe/rules content shipped in <c>Content/en/</c>, so statistical
///     simulations can run against realistic data without a LiteDB-backed ProfileRepository.
/// </summary>
public static class WardrobeFixture
{
    public static (List<ClothingItem> Items, List<OutfitRule> Rules) CreateSeedWardrobe()
    {
        var contentDir = Path.Combine(AppContext.BaseDirectory, "Content", "en");
        var items = new WardrobeContentService().Load(Path.Combine(contentDir, "wardrobe.json"));
        var rules = new OutfitRuleContentService().Load(Path.Combine(contentDir, "outfit-rules.json"));
        return (items, rules);
    }
}
