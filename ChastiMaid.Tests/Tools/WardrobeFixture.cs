using System.Text.Json;
using ChastiMaid.Models;
using ChastiMaid.Services;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Tools;

/// <summary>
///     Loads the same wardrobe/rules content shipped in <c>Content/en/default.contract.json</c>, so statistical
///     simulations can run against realistic data without a LiteDB-backed ProfileRepository.
/// </summary>
public static class WardrobeFixture
{
    public static (List<ClothingItem> Items, List<OutfitRule> Rules) CreateSeedWardrobe()
    {
        var contract = JsonSerializer.Deserialize<GameContract>(
            File.ReadAllText(DataSeeder.SeedPath("en")), ContentJsonOptions.Options)!;
        return (contract.Wardrobe!, contract.OutfitRules!);
    }
}
