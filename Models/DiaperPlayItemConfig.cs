using LiteDB;

namespace ChastiMaid.Models;

/// <summary>A wardrobe item (from the Underwear layer) marked as counting as a diaper for the Diaper Play mechanic.</summary>
public class DiaperPlayItemConfig
{
    [BsonId] public int Id { get; set; }
    public int ClothingItemId { get; set; }
}
