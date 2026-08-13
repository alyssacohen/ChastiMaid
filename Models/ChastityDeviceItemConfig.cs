using LiteDB;

namespace ChastiMaid.Models;

/// <summary>A wardrobe item marked as eligible to be drawn as the persistent chastity device.</summary>
public class ChastityDeviceItemConfig
{
    [BsonId] public int Id { get; set; }
    public int ClothingItemId { get; set; }
}
