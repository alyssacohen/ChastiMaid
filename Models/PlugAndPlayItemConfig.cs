using LiteDB;

namespace ChastiMaid.Models;

/// <summary>An accessory eligible for the Plug &amp; Play mechanic, with its wear-duration range.</summary>
public class PlugAndPlayItemConfig
{
    [BsonId] public int Id { get; set; }
    public int ClothingItemId { get; set; }
    public int MinDurationMinutes { get; set; } = 30;
    public int MaxDurationMinutes { get; set; } = 120;
}
