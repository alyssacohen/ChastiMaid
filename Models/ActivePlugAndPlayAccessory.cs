using LiteDB;

namespace ChastiMaid.Models;

/// <summary>The single currently-worn Plug &amp; Play accessory, if any. Presence of this singleton
/// record means an accessory is worn; removed once the removal popup is confirmed.</summary>
public class ActivePlugAndPlayAccessory
{
    [BsonId] public int Id { get; set; } = 1; // singleton
    public int ClothingItemId { get; set; }
    public DateTime DonnedAt { get; set; }
    public DateTime RemoveAfterUtc { get; set; }
}
