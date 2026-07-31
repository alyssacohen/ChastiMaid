using ChastiMaid.Models;

namespace ChastiMaid.Services;

public static class DiaperPlayEvaluator
{
    /// <summary>True if any of the given outfit item ids is configured as a Diaper Play item.</summary>
    public static bool OutfitHasDiaperItem(IEnumerable<int> outfitItemIds, IEnumerable<DiaperPlayItemConfig> diaperPlayItems)
    {
        var diaperItemIds = diaperPlayItems.Select(c => c.ClothingItemId).ToHashSet();
        return outfitItemIds.Any(diaperItemIds.Contains);
    }
}
