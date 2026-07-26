using LiteDB;

namespace ChastiMaid.Models;

public class ForcedOutfitPreset
{
    [BsonId] public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public double SessionsPerSeverityPoint { get; set; } = 0.3;
    public List<int> ClothingItemIds { get; set; } = [];
    public bool IsFullOutfit { get; set; }
}
